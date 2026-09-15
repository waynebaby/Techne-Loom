using System.Security.Cryptography;

using System.Text.Json;



namespace Techne.Loom.Common.Runtime;



public static class LoomRuntimeGuideRunner

{

    public static async Task<LoomGuideResult> RunAsync(

        LoomLaunchDescriptor descriptor,

        TimeSpan timeout,

        CancellationToken cancellationToken = default)

    {

        ArgumentNullException.ThrowIfNull(descriptor);

        if (timeout <= TimeSpan.Zero)

        {

            throw new ArgumentOutOfRangeException(nameof(timeout), "Guide timeout must be positive.");

        }



        var launch = LoomRuntimeLaunch.CreateCommand(descriptor, ["--guide"]);

        var process = await new DefaultLoomRuntimeProcessRunner().RunAsync(

            launch.Command,

            launch.Arguments,

            launch.WorkingDirectory,

            timeout,

            environmentVariables: null,

            cancellationToken).ConfigureAwait(false);

        if (!process.Started)

        {

            throw new LoomRuntimeHostStartupException($"Runtime process '{launch.Command}' could not start for the MCP guide operation.");

        }



        if (process.ExitCode != 0)

        {

            throw new LoomRuntimeCommandException($"Runtime '--guide' failed after MCP dispatch with exit code {process.ExitCode}: {FirstNonEmpty(process.StandardError, process.StandardOutput)}");

        }



        try

        {

            using var document = JsonDocument.Parse(process.StandardOutput);

            var root = document.RootElement;

            var version = RequiredString(root, "version");

            var docsRoot = RequiredString(root, "docs_root");

            var guidePath = RequiredString(root, "guide_path");

            var expectedVersion = LoomRuntimeCatalog.NormalizeVersion(descriptor.ResolvedRuntimeVersion);

            if (!string.Equals(LoomRuntimeCatalog.NormalizeVersion(version), expectedVersion, StringComparison.Ordinal)

                || !Path.IsPathFullyQualified(docsRoot)

                || !Path.IsPathFullyQualified(guidePath)

                || !Directory.Exists(docsRoot)

                || !File.Exists(guidePath)

                || !IsPathWithinRoot(docsRoot, guidePath))

            {

                throw new LoomRuntimeGuideValidationException("The MCP guide result did not match the descriptor version or documentation root.");

            }



            var expectedRelativePath = $"guides/{LoomRuntimeCatalog.GetEntryPoint(descriptor.Product)}-guide.md";

            var relativePath = Path.GetRelativePath(docsRoot, guidePath).Replace(Path.DirectorySeparatorChar, '/');

            if (!string.Equals(relativePath, expectedRelativePath, StringComparison.Ordinal))

            {

                throw new LoomRuntimeGuideValidationException($"The MCP guide result returned '{relativePath}', expected '{expectedRelativePath}'.");

            }



            var guideHash = Convert.ToBase64String(SHA512.HashData(await File.ReadAllBytesAsync(guidePath, cancellationToken).ConfigureAwait(false)));

            return new LoomGuideResult(version, docsRoot, guidePath, guideHash);

        }

        catch (LoomRuntimeException)

        {

            throw;

        }

        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or ArgumentException or IOException or UnauthorizedAccessException)

        {

            throw new LoomRuntimeGuideValidationException("The MCP guide result was missing or could not be read.", exception);

        }

    }



    private static string RequiredString(JsonElement parent, string name)

    {

        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))

        {

            throw new KeyNotFoundException(name);

        }



        return value.GetString()!;

    }



    private static bool IsPathWithinRoot(string root, string path)

    {

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);

    }



    private static string FirstNonEmpty(string first, string second)

        => string.IsNullOrWhiteSpace(first) ? second.Trim() : first.Trim();

}