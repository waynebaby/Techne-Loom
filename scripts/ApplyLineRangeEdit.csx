using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

var options = ParseArguments(Args.ToArray());
var targetPath = RequirePath(options, "target-file");
var editsManifestPath = RequirePath(options, "edits-file");

var originalBytes = File.ReadAllBytes(targetPath);
var originalContentBytes = HasUtf8Bom(originalBytes) ? originalBytes[3..] : originalBytes;
var original = new UTF8Encoding(false, true).GetString(originalContentBytes);
var newline = DetectNewline(original);
var trailingNewline = GetTrailingNewline(original);
var originalLines = SplitLines(original);
var edits = ReadEdits(editsManifestPath);
ValidateRanges(edits, originalLines, targetPath);

var expectedLines = originalLines.ToList();
foreach (var edit in edits.OrderByDescending(item => item.StartLine))
{
    expectedLines.RemoveRange(edit.StartLine - 1, edit.EndLine - edit.StartLine + 1);
    expectedLines.InsertRange(edit.StartLine - 1, edit.ReplacementLines);
}

var updated = ConvertNewlines(string.Join("\n", expectedLines), newline) + trailingNewline;
var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".range-edit.tmp";

try
{
    File.WriteAllText(temporaryPath, updated, new UTF8Encoding(HasUtf8Bom(originalBytes), false));
    ValidateResult(temporaryPath, expectedLines, trailingNewline, HasUtf8Bom(originalBytes));
    File.Move(temporaryPath, targetPath, overwrite: true);
    ValidateResult(targetPath, expectedLines, trailingNewline, HasUtf8Bom(originalBytes));
    Console.WriteLine($"Applied {edits.Count} line-range edits to '{targetPath}' from one original read.");
}
finally
{
    if (File.Exists(temporaryPath))
    {
        File.Delete(temporaryPath);
    }
}

static Dictionary<string, string> ParseArguments(string[] arguments)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        if (argument == "--")
        {
            continue;
        }

        if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= arguments.Length)
        {
            throw new ArgumentException($"Expected an option followed by a value, got '{argument}'.");
        }

        var name = argument[2..];
        var value = arguments[++index];
        if (name.Length == 0 || value.StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(name, value))
        {
            throw new ArgumentException($"Invalid or duplicated option '--{name}'.");
        }
    }

    return options;
}

static string RequirePath(Dictionary<string, string> options, string name)
{
    if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Missing required option '--{name}'.");
    }

    return Path.GetFullPath(value);
}

static List<RangeEdit> ReadEdits(string manifestPath)
{
    using var document = JsonDocument.Parse(File.ReadAllText(manifestPath, new UTF8Encoding(false, true)));
    if (!document.RootElement.TryGetProperty("edits", out var editArray) || editArray.ValueKind != JsonValueKind.Array || editArray.GetArrayLength() == 0)
    {
        throw new InvalidOperationException("The edits manifest must contain a non-empty 'edits' array.");
    }

    var edits = new List<RangeEdit>();
    foreach (var edit in editArray.EnumerateArray())
    {
        var startLine = RequiredPositiveInt(edit, "start_line");
        var endLine = RequiredPositiveInt(edit, "end_line");
        if (endLine < startLine)
        {
            throw new InvalidOperationException($"Invalid edit range {startLine}-{endLine}.");
        }

        var expectedStartPath = RequiredString(edit, "expected_start_file");
        var expectedEndPath = RequiredString(edit, "expected_end_file");
        var replacementPath = RequiredString(edit, "replacement_file");
        edits.Add(new RangeEdit(
            startLine,
            endLine,
            ReadBoundary(expectedStartPath),
            ReadBoundary(expectedEndPath),
            SplitLines(File.ReadAllText(Path.GetFullPath(replacementPath), new UTF8Encoding(false, true)))));
    }

    return edits;
}

static void ValidateRanges(IReadOnlyList<RangeEdit> edits, IReadOnlyList<string> originalLines, string targetPath)
{
    var ordered = edits.OrderBy(edit => edit.StartLine).ToArray();
    for (var index = 0; index < ordered.Length; index++)
    {
        var edit = ordered[index];
        if (edit.StartLine > originalLines.Count || edit.EndLine > originalLines.Count)
        {
            throw new InvalidOperationException($"Edit range {edit.StartLine}-{edit.EndLine} exceeds '{targetPath}' ({originalLines.Count} lines).");
        }

        if (!BoundaryEquals(originalLines[edit.StartLine - 1], edit.ExpectedStart)
            || !BoundaryEquals(originalLines[edit.EndLine - 1], edit.ExpectedEnd))
        {
            throw new InvalidOperationException($"Edit range {edit.StartLine}-{edit.EndLine} has mismatched boundary content.");
        }

        if (index > 0 && ordered[index - 1].EndLine >= edit.StartLine)
        {
            throw new InvalidOperationException($"Edit ranges {ordered[index - 1].StartLine}-{ordered[index - 1].EndLine} and {edit.StartLine}-{edit.EndLine} overlap.");
        }
    }
}

static int RequiredPositiveInt(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var result) || result < 1)
    {
        throw new InvalidOperationException($"Edit property '{propertyName}' must be a positive integer.");
    }

    return result;
}

static string RequiredString(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
    {
        throw new InvalidOperationException($"Edit property '{propertyName}' must be a non-empty path.");
    }

    return Path.GetFullPath(value.GetString()!);
}

static string ReadBoundary(string path)
{
    var content = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
    var lines = SplitLines(content);
    if (lines.Count == 0 && Normalize(content).Length == 0)
    {
        return string.Empty;
    }

    if (lines.Count != 1)
    {
        throw new InvalidOperationException($"Boundary file '{path}' must contain exactly one logical line.");
    }

    return lines[0];
}

static List<string> SplitLines(string text)
{
    var normalized = Normalize(text);
    if (normalized.Length == 0)
    {
        return [];
    }

    if (normalized.EndsWith('\n'))
    {
        normalized = normalized[..^1];
    }

    return normalized.Split('\n').ToList();
}

static string Normalize(string text)
    => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

static string DetectNewline(string text)
    => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : text.Contains('\r') ? "\r" : "\n";

static string GetTrailingNewline(string text)
{
    if (text.EndsWith("\r\n", StringComparison.Ordinal)) return "\r\n";
    if (text.EndsWith('\n')) return "\n";
    if (text.EndsWith('\r')) return "\r";
    return string.Empty;
}

static string ConvertNewlines(string text, string newline)
    => newline switch
    {
        "\r\n" => text.Replace("\n", "\r\n", StringComparison.Ordinal),
        "\r" => text.Replace("\n", "\r", StringComparison.Ordinal),
        _ => text,
    };

static bool BoundaryEquals(string actual, string expected)
    => string.Equals(actual.Trim().ToLowerInvariant(), expected.Trim().ToLowerInvariant(), StringComparison.Ordinal);

static bool HasUtf8Bom(byte[] bytes)
    => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

static void ValidateResult(string path, IReadOnlyList<string> expectedLines, string expectedTrailingNewline, bool expectedBom)
{
    var bytes = File.ReadAllBytes(path);
    var text = File.ReadAllText(path, new UTF8Encoding(false, true));
    if (!SplitLines(text).SequenceEqual(expectedLines, StringComparer.Ordinal)
        || !string.Equals(GetTrailingNewline(text), expectedTrailingNewline, StringComparison.Ordinal)
        || HasUtf8Bom(bytes) != expectedBom)
    {
        throw new InvalidOperationException($"Post-write validation failed for '{path}'.");
    }
}

record RangeEdit(
    int StartLine,
    int EndLine,
    string ExpectedStart,
    string ExpectedEnd,
    IReadOnlyList<string> ReplacementLines);