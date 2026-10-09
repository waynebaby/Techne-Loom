using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record AskScopedWorkerEndpoint(string AskId, string Url, DateTimeOffset ExpiresAtUtc);

public sealed class AskScopedHttpWorker : IAsyncDisposable
{
    private const int MaximumConcurrentRequests = 16;
    private const int MaximumJsonRequestBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);
    private readonly AskScopedSubmissionStore _store;
    private readonly AskScopedLaunch _launch;
    private readonly HttpListener _listener;
    private readonly SemaphoreSlim _requestSlots = new(MaximumConcurrentRequests);
    private readonly ConcurrentDictionary<long, Task> _requestTasks = new();
    private readonly ConcurrentDictionary<string, byte> _sessionTokenHashes = new(StringComparer.Ordinal);
    private readonly byte[] _pairingCodeHash;
    private readonly Uri _baseUri;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _completion;
    private long _nextRequestId;
    private int _pairingCodeConsumed;

    private AskScopedHttpWorker(
        AskScopedSubmissionStore store,
        AskScopedLaunch launch,
        HttpListener listener,
        Uri baseUri,
        string pairingCode)
    {
        _store = store;
        _launch = launch;
        _listener = listener;
        _baseUri = baseUri;
        _pairingCodeHash = HashSecret(pairingCode);
        Endpoint = new AskScopedWorkerEndpoint(
            launch.AskId,
            new UriBuilder(baseUri) { Fragment = "pair=" + Uri.EscapeDataString(pairingCode) }.Uri.AbsoluteUri,
            launch.ExpiresAtUtc);
        _completion = RunAsync();
    }

    public AskScopedWorkerEndpoint Endpoint { get; }

    public Task Completion => _completion;

    public static async Task<AskScopedHttpWorker> StartAsync(
        AskScopedSubmissionStore store,
        AskScopedLaunch launch,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(launch);
        await store.GetSnapshotAsync(launch.AskId, launch.MachineCapability, ct).ConfigureAwait(false);

        HttpListener? listener = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var port = GetAvailableLoopbackPort();
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                var baseUri = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute);
                return new AskScopedHttpWorker(store, launch, listener, baseUri, CreateSecret());
            }
            catch (HttpListenerException) when (attempt < 4)
            {
                listener.Close();
            }
            catch
            {
                listener.Close();
                throw;
            }
        }

        listener?.Close();
        throw new InvalidOperationException("A loopback AskUser listener could not be started.");
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
            _listener.Stop();
        }

        try
        {
            await _completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (HttpListenerException) when (_stop.IsCancellationRequested)
        {
        }

        _listener.Close();
        _requestSlots.Dispose();
        _stop.Dispose();
    }

    private async Task RunAsync()
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        var acceptTask = AcceptLoopAsync(lifetime.Token);
        var expiryTask = MonitorExpiryAsync(lifetime.Token);
        var completedTask = await Task.WhenAny(acceptTask, expiryTask).ConfigureAwait(false);
        lifetime.Cancel();
        _listener.Stop();

        try
        {
            await Task.WhenAll(acceptTask, expiryTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (HttpListenerException) when (lifetime.IsCancellationRequested)
        {
        }

        if (completedTask.IsFaulted)
        {
            await completedTask.ConfigureAwait(false);
        }

        var activeRequests = _requestTasks.Values.ToArray();
        if (activeRequests.Length > 0)
        {
            await Task.WhenAll(activeRequests).ConfigureAwait(false);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await _requestSlots.WaitAsync(ct).ConfigureAwait(false);
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                _requestSlots.Release();
                throw;
            }

            var requestId = Interlocked.Increment(ref _nextRequestId);
            var requestTask = HandleAndReleaseAsync(context, ct);
            _requestTasks[requestId] = requestTask;
            _ = requestTask.ContinueWith(
                completedTask => { _requestTasks.TryRemove(requestId, out _); },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleAndReleaseAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            await HandleRequestAsync(context, ct).ConfigureAwait(false);
        }
        finally
        {
            _requestSlots.Release();
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var response = context.Response;
        response.Headers[HttpResponseHeader.CacheControl] = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.Headers["Permissions-Policy"] = "microphone=(self)";
        response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data: blob:; media-src blob:; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

        try
        {
            if (!IsAllowedHost(context.Request))
            {
                throw new AskScopedHttpException(HttpStatusCode.Forbidden, "The request host is not allowed.");
            }

            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            var method = context.Request.HttpMethod;
            if (path.StartsWith("/api/", StringComparison.Ordinal) && !string.IsNullOrEmpty(context.Request.Url?.Query))
            {
                throw new AskScopedHttpException(HttpStatusCode.BadRequest, "API credentials and parameters cannot be supplied in a query string.");
            }

            if (method == "GET" && path == "/")
            {
                await WriteAssetAsync(response, "index.html", "text/html; charset=utf-8", ct).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/app.js")
            {
                await WriteAssetAsync(response, "app.js", "text/javascript; charset=utf-8", ct).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/style.css")
            {
                await WriteAssetAsync(response, "style.css", "text/css; charset=utf-8", ct).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/answer-validator.js")
            {
                await WriteAssetAsync(response, "answer-validator.js", "text/javascript; charset=utf-8", ct).ConfigureAwait(false);
                return;
            }

            if (path == "/api/session" && method == "POST")
            {
                RequireSameOrigin(context.Request, mutation: true);
                var pairing = await ReadJsonAsync<PairingRequest>(context.Request, 4096, ct).ConfigureAwait(false);
                var token = ExchangePairingCode(pairing.PairingCode);
                await WriteJsonAsync(response, HttpStatusCode.OK, new { token }, ct).ConfigureAwait(false);
                return;
            }

            if (!path.StartsWith("/api/", StringComparison.Ordinal))
            {
                throw new AskScopedHttpException(HttpStatusCode.NotFound, "The requested resource was not found.");
            }

            if (!IsAuthorized(context.Request))
            {
                throw new AskScopedHttpException(HttpStatusCode.Unauthorized, "A valid ask session is required.");
            }

            if (method == "GET" && path == "/api/ask")
            {
                var snapshot = await _store.GetSnapshotAsync(_launch.AskId, _launch.MachineCapability, ct).ConfigureAwait(false);
                await WriteJsonAsync(response, HttpStatusCode.OK, snapshot, ct).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/api/status")
            {
                var snapshot = await _store.GetSnapshotAsync(_launch.AskId, _launch.MachineCapability, ct).ConfigureAwait(false);
                await WriteJsonAsync(response, HttpStatusCode.OK, new
                {
                    status = snapshot.Receipt is null ? "pending" : "submitted",
                    generation = snapshot.Generation,
                    receipt = snapshot.Receipt,
                }, ct).ConfigureAwait(false);
                return;
            }

            if (method == "PUT" && path == "/api/draft")
            {
                RequireSameOrigin(context.Request, mutation: true);
                var draft = await ReadJsonAsync<DraftRequest>(context.Request, MaximumJsonRequestBytes, ct).ConfigureAwait(false);
                var snapshot = await _store.SaveDraftAsync(
                    _launch.AskId,
                    _launch.MachineCapability,
                    draft.ExpectedGeneration,
                    draft.Answers,
                    ct).ConfigureAwait(false);
                await WriteJsonAsync(response, HttpStatusCode.OK, snapshot, ct).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path == "/api/submit")
            {
                RequireSameOrigin(context.Request, mutation: true);
                var submission = await ReadJsonAsync<SubmitRequest>(context.Request, MaximumJsonRequestBytes, ct).ConfigureAwait(false);
                var receipt = await _store.SubmitAsync(
                    _launch.AskId,
                    _launch.MachineCapability,
                    submission.ExpectedGeneration,
                    submission.OperationId,
                    submission.Answers,
                    ct).ConfigureAwait(false);
                await WriteJsonAsync(response, HttpStatusCode.OK, receipt, ct).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && path.StartsWith("/api/attachments/", StringComparison.Ordinal))
            {
                RequireSameOrigin(context.Request, mutation: true);
                var questionId = DecodePathSegment(path, "/api/attachments/");
                var fileName = DecodeHeaderValue(context.Request.Headers["X-File-Name"]);
                var mediaType = context.Request.ContentType ?? string.Empty;
                var upload = await _store.StoreAttachmentAsync(
                    _launch.AskId,
                    _launch.MachineCapability,
                    ParseGenerationHeader(context.Request),
                    questionId,
                    fileName,
                    mediaType,
                    context.Request.InputStream,
                    ct).ConfigureAwait(false);
                await WriteJsonAsync(response, HttpStatusCode.Created, upload, ct).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path.StartsWith("/api/attachments/", StringComparison.Ordinal))
            {
                var attachmentId = DecodePathSegment(path, "/api/attachments/");
                var snapshot = await _store.GetSnapshotAsync(_launch.AskId, _launch.MachineCapability, ct).ConfigureAwait(false);
                var metadata = snapshot.Attachments.FirstOrDefault(item => string.Equals(item.AttachmentId, attachmentId, StringComparison.Ordinal))
                    ?? throw new AskScopedHttpException(HttpStatusCode.NotFound, "The attachment was not found.");
                await using var stream = await _store.OpenAttachmentReadStreamAsync(
                    _launch.AskId,
                    _launch.MachineCapability,
                    attachmentId,
                    ct).ConfigureAwait(false);
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = metadata.MediaType;
                response.ContentLength64 = metadata.Length;
                response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(metadata.FileName)}";
                await stream.CopyToAsync(response.OutputStream, ct).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && path == "/api/offline")
            {
                var snapshot = await _store.GetSnapshotAsync(_launch.AskId, _launch.MachineCapability, ct).ConfigureAwait(false);
                var html = CreateOfflineDocument(snapshot);
                response.Headers["Content-Disposition"] = $"attachment; filename=\"ask-offline-{snapshot.AskId}.html\"";
                await WriteBytesAsync(response, HttpStatusCode.OK, Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8", ct).ConfigureAwait(false);
                return;
            }

            throw new AskScopedHttpException(HttpStatusCode.NotFound, "The requested API route was not found.");
        }
        catch (AskScopedHttpException exception)
        {
            await WriteJsonAsync(response, exception.StatusCode, new { error = exception.Message }, ct).ConfigureAwait(false);
        }
        catch (AskScopedValidationException exception)
        {
            await WriteJsonAsync(response, HttpStatusCode.UnprocessableEntity, new { error = exception.Message, diagnostics = exception.Diagnostics }, ct).ConfigureAwait(false);
        }
        catch (AskScopedConflictException exception)
        {
            await WriteJsonAsync(response, HttpStatusCode.Conflict, new { error = exception.Message }, ct).ConfigureAwait(false);
        }
        catch (AskScopedExpiredException)
        {
            await WriteJsonAsync(response, HttpStatusCode.Gone, new { error = "This ask has expired." }, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            await WriteJsonAsync(response, HttpStatusCode.Unauthorized, new { error = "A valid ask session is required." }, ct).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            await WriteJsonAsync(response, HttpStatusCode.Gone, new { error = "This ask is no longer available." }, ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await WriteJsonAsync(response, HttpStatusCode.BadRequest, new { error = "The request body is not valid JSON." }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            await WriteJsonAsync(response, HttpStatusCode.InternalServerError, new { error = "The request could not be processed." }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (HttpListenerException)
            {
            }
        }
    }

    private async Task MonitorExpiryAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var snapshot = await _store.GetSnapshotAsync(_launch.AskId, _launch.MachineCapability, ct).ConfigureAwait(false);
                if (snapshot.Receipt is not null)
                {
                    return;
                }
            }
            catch (AskScopedExpiredException)
            {
                return;
            }
            catch (FileNotFoundException)
            {
                return;
            }
        }
    }

    private bool IsAllowedHost(HttpListenerRequest request)
    {
        var expectedAuthority = $"127.0.0.1:{_baseUri.Port}";
        return request.Url is { Scheme: "http", Host: "127.0.0.1" }
            && request.Url.Port == _baseUri.Port
            && string.Equals(request.Headers["Host"], expectedAuthority, StringComparison.OrdinalIgnoreCase)
            && (request.RemoteEndPoint is null || IPAddress.IsLoopback(request.RemoteEndPoint.Address));
    }

    private void RequireSameOrigin(HttpListenerRequest request, bool mutation)
    {
        var origin = request.Headers["Origin"];
        if (string.IsNullOrWhiteSpace(origin) && !mutation)
        {
            return;
        }

        if (!string.Equals(origin, _baseUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new AskScopedHttpException(HttpStatusCode.Forbidden, "The request origin is not allowed.");
        }
    }

    private string ExchangePairingCode(string? pairingCode)
    {
        if (string.IsNullOrWhiteSpace(pairingCode))
        {
            throw new AskScopedHttpException(HttpStatusCode.Unauthorized, "The pairing code is invalid or has already been used.");
        }

        var suppliedHash = HashSecret(pairingCode);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, _pairingCodeHash)
            || Interlocked.CompareExchange(ref _pairingCodeConsumed, 1, 0) != 0)
        {
            throw new AskScopedHttpException(HttpStatusCode.Unauthorized, "The pairing code is invalid or has already been used.");
        }

        var token = CreateSecret();
        _sessionTokenHashes.TryAdd(Convert.ToHexString(HashSecret(token)), 0);
        return token;
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        var authorization = request.Headers["Authorization"];
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorization[7..].Trim();
        if (token.Length == 0)
        {
            return false;
        }

        return _sessionTokenHashes.ContainsKey(Convert.ToHexString(HashSecret(token)));
    }

    private async Task<T> ReadJsonAsync<T>(HttpListenerRequest request, int maximumBytes, CancellationToken ct)
    {
        if (!string.Equals(request.ContentType?.Split(';', 2)[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new AskScopedHttpException(HttpStatusCode.UnsupportedMediaType, "The request content type must be application/json.");
        }

        var bytes = await ReadBoundedBodyAsync(request, maximumBytes, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions)
            ?? throw new JsonException("The request body was empty.");
    }

    private static async Task<byte[]> ReadBoundedBodyAsync(HttpListenerRequest request, int maximumBytes, CancellationToken ct)
    {
        if (request.ContentLength64 > maximumBytes)
        {
            throw new AskScopedHttpException(HttpStatusCode.RequestEntityTooLarge, "The request body exceeds the configured limit.");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var bytesRead = await request.InputStream.ReadAsync(chunk.AsMemory(), ct).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (buffer.Length + bytesRead > maximumBytes)
            {
                throw new AskScopedHttpException(HttpStatusCode.RequestEntityTooLarge, "The request body exceeds the configured limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private async Task WriteAssetAsync(HttpListenerResponse response, string name, string contentType, CancellationToken ct)
        => await WriteBytesAsync(response, HttpStatusCode.OK, Encoding.UTF8.GetBytes(ReadAsset(name)), contentType, ct).ConfigureAwait(false);

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode statusCode, object value, CancellationToken ct)
        => await WriteBytesAsync(response, statusCode, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), "application/json; charset=utf-8", ct).ConfigureAwait(false);

    private static async Task WriteBytesAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        byte[] bytes,
        string contentType,
        CancellationToken ct)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.LongLength;
        await response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    private static string CreateOfflineDocument(AskScopedSnapshot snapshot)
    {
        var encoderOptions = new JsonSerializerOptions(JsonOptions) { Encoder = JavaScriptEncoder.Default };
        var bootstrap = JsonSerializer.Serialize(
            new OfflineBootstrap(snapshot.AskId, snapshot.Contract, snapshot.DraftAnswers)
            {
                Generation = snapshot.Generation,
                Attachments = snapshot.Attachments,
            },
            encoderOptions);
        var replacements = new (string Token, string Content)[]
        {
            ("{{BOOTSTRAP}}", bootstrap),
            ("{{STYLE}}", ReadAsset("style.css")),
            ("{{VALIDATOR_SCRIPT}}", ReadAsset("answer-validator.js")),
            ("{{APP_SCRIPT}}", ReadAsset("app.js")),
        };
        var template = ReadAsset("offline.html");
        if (replacements.Any(replacement => !template.Contains(replacement.Token, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The offline AskUser document template is incomplete.");
        }

        var document = new StringBuilder(template.Length);
        var sourceIndex = 0;
        while (sourceIndex < template.Length)
        {
            var nextIndex = template.Length;
            (string Token, string Content)? nextReplacement = null;
            foreach (var candidate in replacements)
            {
                var tokenIndex = template.IndexOf(candidate.Token, sourceIndex, StringComparison.Ordinal);
                if (tokenIndex >= 0 && tokenIndex < nextIndex)
                {
                    nextIndex = tokenIndex;
                    nextReplacement = candidate;
                }
            }

            if (nextReplacement is not { } replacement)
            {
                document.Append(template, sourceIndex, template.Length - sourceIndex);
                break;
            }

            document.Append(template, sourceIndex, nextIndex - sourceIndex);
            document.Append(replacement.Content);
            sourceIndex = nextIndex + replacement.Token.Length;
        }

        return document.ToString();
    }

    private static string ReadAsset(string fileName)
    {
        var assembly = typeof(AskScopedHttpWorker).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded AskUser asset '{fileName}' was not found.");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded AskUser asset '{fileName}' could not be opened.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string DecodePathSegment(string path, string prefix)
    {
        var encoded = path[prefix.Length..];
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Contains('/', StringComparison.Ordinal))
        {
            throw new AskScopedHttpException(HttpStatusCode.BadRequest, "The request path is invalid.");
        }

        return Uri.UnescapeDataString(encoded);
    }

    private static string DecodeHeaderValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AskScopedHttpException(HttpStatusCode.BadRequest, "The X-File-Name header is required.");
        }

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            throw new AskScopedHttpException(HttpStatusCode.BadRequest, "The X-File-Name header is invalid.");
        }
    }

    private static long ParseGenerationHeader(HttpListenerRequest request)
    {
        if (!long.TryParse(request.Headers["X-Ask-Generation"], out var generation) || generation < 0)
        {
            throw new AskScopedHttpException(HttpStatusCode.BadRequest, "A valid X-Ask-Generation header is required.");
        }

        return generation;
    }

    private static int GetAvailableLoopbackPort()
    {
        using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
    }

    private static string CreateSecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] HashSecret(string secret)
        => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    private sealed record PairingRequest(string? PairingCode);

    private sealed record DraftRequest(long ExpectedGeneration, Dictionary<string, AskScopedAnswerValue> Answers);

    private sealed record SubmitRequest(long ExpectedGeneration, string OperationId, Dictionary<string, AskScopedAnswerValue> Answers);

    private sealed record OfflineBootstrap(
        int SchemaVersion,
        string AskId,
        Techne.Loom.Abstractions.TaskTracking.Model.UserInputContract Contract,
        IReadOnlyDictionary<string, AskScopedAnswerValue> DraftAnswers)
    {
        public OfflineBootstrap(string askId, Techne.Loom.Abstractions.TaskTracking.Model.UserInputContract contract, IReadOnlyDictionary<string, AskScopedAnswerValue> draftAnswers)
            : this(1, askId, contract, draftAnswers)
        {
        }
        public long Generation { get; init; }

        public IReadOnlyList<AskScopedAttachmentMetadata> Attachments { get; init; } = [];
    }

    private sealed class AskScopedHttpException(HttpStatusCode statusCode, string message) : Exception(message)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}

public static class AskScopedWorkerCommand
{
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

    public static async Task<int> RunFromStandardInputAsync(CancellationToken ct = default)
    {
        var input = await Console.In.ReadLineAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(input))
        {
            await Console.Out.WriteLineAsync("{\"error\":\"A worker bootstrap request is required.\"}").ConfigureAwait(false);
            return 2;
        }

        try
        {
            var request = JsonSerializer.Deserialize<WorkerBootstrapRequest>(input, JsonOptions)
                ?? throw new JsonException("The worker bootstrap request was empty.");
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = request.RootDirectory });
            var launch = new AskScopedLaunch(request.AskId, request.MachineCapability, request.Generation, request.ExpiresAtUtc);
            await using var worker = await AskScopedHttpWorker.StartAsync(store, launch, ct).ConfigureAwait(false);
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(worker.Endpoint, JsonOptions)).ConfigureAwait(false);
            await Console.Out.FlushAsync(ct).ConfigureAwait(false);
            await worker.Completion.ConfigureAwait(false);
            return 0;
        }
        catch (Exception)
        {
            await Console.Out.WriteLineAsync("{\"error\":\"The AskUser worker could not start.\"}").ConfigureAwait(false);
            await Console.Out.FlushAsync(ct).ConfigureAwait(false);
            return 2;
        }
    }

    internal sealed record WorkerBootstrapRequest(
        string AskId,
        string MachineCapability,
        string RootDirectory,
        long Generation,
        DateTimeOffset ExpiresAtUtc);
}

public static class AskScopedWorkerProcessLauncher
{
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

    internal static ProcessStartInfo CreateStartInfo(string executablePath, string? entryAssemblyPath)
    {
        var normalizedExecutablePath = Path.GetFullPath(executablePath);
        var startInfo = new ProcessStartInfo(normalizedExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(normalizedExecutablePath)!,
        };

        if (string.Equals(Path.GetFileNameWithoutExtension(normalizedExecutablePath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entryAssemblyPath) || !File.Exists(entryAssemblyPath))
            {
                throw new InvalidOperationException("The current .NET entry assembly path is unavailable; the AskUser worker cannot be detached.");
            }

            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add(Path.GetFullPath(entryAssemblyPath));
        }

        startInfo.ArgumentList.Add("--ask-user-worker");
        return startInfo;
    }

    internal static string? ResolveEntryAssemblyPath(string? entryAssemblyName, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(entryAssemblyName))
        {
            return null;
        }

        return Path.GetFullPath($"{entryAssemblyName}.dll", baseDirectory);
    }

    public static async Task<AskScopedWorkerEndpoint> StartDetachedAsync(
        AskScopedSubmissionStore store,
        AskScopedLaunch launch,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(launch);
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("The current product process path is unavailable; the AskUser worker cannot be detached.");
        }

        var entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        var entryAssemblyPath = ResolveEntryAssemblyPath(entryAssemblyName, AppContext.BaseDirectory);
        var startInfo = CreateStartInfo(executablePath, entryAssemblyPath);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The AskUser worker process could not be started.");

        var request = new AskScopedWorkerCommand.WorkerBootstrapRequest(
            launch.AskId,
            launch.MachineCapability,
            store.RootDirectory,
            launch.Generation,
            launch.ExpiresAtUtc);
        var json = JsonSerializer.Serialize(request, JsonOptions);
        await process.StandardInput.WriteLineAsync(json).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
        process.StandardInput.Close();

        using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        startupTimeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var readyLine = await process.StandardOutput.ReadLineAsync(startupTimeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(readyLine))
            {
                throw new InvalidOperationException("The AskUser worker exited before reporting readiness.");
            }

            using var readyDocument = JsonDocument.Parse(readyLine);
            if (readyDocument.RootElement.TryGetProperty("error", out _))
            {
                throw new InvalidOperationException("The AskUser worker rejected its startup request.");
            }

            var endpoint = JsonSerializer.Deserialize<AskScopedWorkerEndpoint>(readyLine, JsonOptions)
                ?? throw new InvalidOperationException("The AskUser worker returned an invalid readiness response.");
            var endpointUri = new Uri(endpoint.Url, UriKind.Absolute);
            if (endpoint.AskId != launch.AskId
                || endpoint.ExpiresAtUtc != launch.ExpiresAtUtc
                || endpointUri.Scheme != Uri.UriSchemeHttp
                || !IPAddress.IsLoopback(IPAddress.Parse(endpointUri.Host))
                || string.IsNullOrWhiteSpace(endpointUri.Fragment))
            {
                throw new InvalidOperationException("The AskUser worker returned an unsafe or mismatched endpoint.");
            }

            return endpoint;
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
    }
}
