using System.Text;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record TextFilePatchRequest(
    string PatchContentFile,
    string PatchTarget,
    int FromLine,
    int ToLine);

public sealed record TextFilePatchResult(
    string PatchTarget,
    int AppliedFromLine,
    int AppliedToLine,
    int PatchLineCount,
    int OriginalLineCount,
    int UpdatedLineCount);

public static class TextFilePatchService
{
    public static async Task<TextFilePatchResult> ApplyAsync(TextFilePatchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PatchContentFile))
        {
            throw new InvalidOperationException("Patch content file path is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PatchTarget))
        {
            throw new InvalidOperationException("Patch target file path is required.");
        }

        if (request.FromLine <= 0)
        {
            throw new InvalidOperationException("--from-line must be a positive 1-based line number.");
        }

        if (request.ToLine <= 0)
        {
            throw new InvalidOperationException("--to-line must be a positive 1-based line number.");
        }

        if (request.FromLine > request.ToLine)
        {
            throw new InvalidOperationException("--from-line cannot be greater than --to-line.");
        }

        var targetPath = Path.GetFullPath(request.PatchTarget);
        var patchContentPath = Path.GetFullPath(request.PatchContentFile);

        if (!File.Exists(targetPath))
        {
            throw new InvalidOperationException($"Patch target file '{targetPath}' does not exist.");
        }

        if (!File.Exists(patchContentPath))
        {
            throw new InvalidOperationException($"Patch content file '{patchContentPath}' does not exist.");
        }

        var targetFile = await LoadTextFileAsync(targetPath, cancellationToken).ConfigureAwait(false);
        var patchFile = await LoadTextFileAsync(patchContentPath, cancellationToken).ConfigureAwait(false);

        var targetLines = ParseLines(targetFile.Text);
        var patchLines = ParseLines(patchFile.Text);

        if (request.FromLine > targetLines.Count)
        {
            throw new InvalidOperationException($"--from-line {request.FromLine} exceeds the target file line count {targetLines.Count}.");
        }

        var appliedToLine = Math.Min(request.ToLine, targetLines.Count);
        var hasSuffix = appliedToLine < targetLines.Count;
        var preserveTrailingNewLine = !hasSuffix && targetLines[appliedToLine - 1].LineEnding.Length > 0;
        var replacementText = BuildReplacementText(patchLines, targetFile.NewLine, hasSuffix, preserveTrailingNewLine);
        var updatedText = targetFile.Text[..targetLines[request.FromLine - 1].FullStart]
            + replacementText
            + targetFile.Text[targetLines[appliedToLine - 1].FullEndExclusive..];
        var updatedLineCount = targetLines.Count - (appliedToLine - request.FromLine + 1) + patchLines.Count;

        if (updatedLineCount == 0)
        {
            updatedText = string.Empty;
        }

        await WriteAtomicallyAsync(targetPath, updatedText, targetFile.Encoding, targetFile.EmitBom, cancellationToken).ConfigureAwait(false);

        return new TextFilePatchResult(
            targetPath,
            request.FromLine,
            appliedToLine,
            patchLines.Count,
            targetLines.Count,
                updatedLineCount);
    }

    private static async Task<TextFileContent> LoadTextFileAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var (encoding, bomLength, emitBom) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
        return new TextFileContent(
            text,
            CreateWriterEncoding(encoding, emitBom),
            emitBom,
            DetectNewLine(text));
    }

    private static IReadOnlyList<TextLine> ParseLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = new List<TextLine>();
        var index = 0;
        while (index < text.Length)
        {
            var lineStart = index;
            while (index < text.Length && text[index] != '\r' && text[index] != '\n')
            {
                index++;
            }

            var content = text[lineStart..index];
            var lineEnding = string.Empty;
            if (index < text.Length)
            {
                if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    lineEnding = "\r\n";
                    index += 2;
                }
                else
                {
                    lineEnding = text[index].ToString();
                    index++;
                }
            }

            lines.Add(new TextLine(lineStart, content, lineEnding, lineStart + content.Length + lineEnding.Length));
        }

        return lines;
    }

    private static string BuildReplacementText(
        IReadOnlyList<TextLine> patchLines,
        string targetNewLine,
        bool hasSuffix,
        bool preserveTrailingNewLine)
    {
        if (patchLines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < patchLines.Count; index++)
        {
            builder.Append(patchLines[index].Content);
            if (index < patchLines.Count - 1 || hasSuffix || preserveTrailingNewLine)
            {
                builder.Append(targetNewLine);
            }
        }

        return builder.ToString();
    }

    private static string DetectNewLine(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
            }

            if (text[index] == '\n')
            {
                return "\n";
            }
        }

        return Environment.NewLine;
    }

    private static (Encoding Encoding, int BomLength, bool EmitBom) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                return (new UTF32Encoding(true, true), 4, true);
            }

            if (bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
            {
                return (new UTF32Encoding(false, true), 4, true);
            }
        }

        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true), 3, true);
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return (new UnicodeEncoding(true, true), 2, true);
            }

            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return (new UnicodeEncoding(false, true), 2, true);
            }
        }

        return (new UTF8Encoding(false), 0, false);
    }

    private static Encoding CreateWriterEncoding(Encoding encoding, bool emitBom)
    {
        return encoding.WebName switch
        {
            "utf-8" => new UTF8Encoding(emitBom),
            "utf-16" => new UnicodeEncoding(false, emitBom),
            "utf-16BE" => new UnicodeEncoding(true, emitBom),
            "utf-32" => new UTF32Encoding(false, emitBom),
            "utf-32BE" => new UTF32Encoding(true, emitBom),
            _ => encoding,
        };
    }

    private static async Task WriteAtomicallyAsync(string targetPath, string content, Encoding encoding, bool emitBom, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Patch target file '{targetPath}' must have a parent directory.");
        }

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(stream, CreateWriterEncoding(encoding, emitBom)))
            {
                await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed record TextFileContent(
        string Text,
        Encoding Encoding,
        bool EmitBom,
        string NewLine);

    private sealed record TextLine(int FullStart, string Content, string LineEnding, int FullEndExclusive);
}