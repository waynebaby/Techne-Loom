using System.Text.Json;
using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class LocalFileUrl
{
    private const string Scheme = "file://";

    public static string FromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = NormalizeSeparators(Path.GetFullPath(path));
        if (OperatingSystem.IsWindows() && IsDrivePath(fullPath))
        {
            return Scheme + "/" + fullPath[..3] + EscapePathSegments(fullPath[3..]);
        }

        if (OperatingSystem.IsWindows() && fullPath.StartsWith("//", StringComparison.Ordinal))
        {
            return Scheme + EscapePathSegments(fullPath[2..]);
        }

        return Scheme + EscapePath(fullPath);
    }

    public static string ToPath(string fileUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileUrl);
        if (!fileUrl.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return fileUrl;
        }

        var path = fileUrl[Scheme.Length..];
        if (OperatingSystem.IsWindows())
        {
            if (path.Length >= 4 && path[0] == '/' && IsDrivePath(path[1..]))
            {
                path = path[1..];
            }
            else if (!path.StartsWith("/", StringComparison.Ordinal) && !IsDrivePath(path))
            {
                path = "\\\\" + path.Replace('/', '\\');
            }

            return Path.GetFullPath(Uri.UnescapeDataString(path).Replace('/', '\\'));
        }

        if (path.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase))
        {
            path = path["localhost".Length..];
        }

        return Uri.UnescapeDataString(path);
    }

    private static string NormalizeSeparators(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static bool IsDrivePath(string path)
        => path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '/';

    private static string EscapePath(string path)
        => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string EscapePathSegments(string path)
        => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}

public sealed class LocalFileUrlJsonConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return value is null ? null : LocalFileUrl.ToPath(value);
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(LocalFileUrl.FromPath(value));
    }
}
