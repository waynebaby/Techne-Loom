using System.Text;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class TextFilePatchServiceTests
{
    [Fact]
    public async Task ApplyAsync_ReplacesMiddleRangeAndPreservesCrLf()
    {
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-content-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(targetFile, "a\r\nb\r\nc\r\nd\r\n", new UTF8Encoding(false));
        await File.WriteAllTextAsync(patchFile, "x\r\ny\r\n", new UTF8Encoding(false));

        var result = await TextFilePatchService.ApplyAsync(new TextFilePatchRequest(patchFile, targetFile, 2, 3));

        Assert.Equal(2, result.AppliedFromLine);
        Assert.Equal(3, result.AppliedToLine);
        Assert.Equal(2, result.PatchLineCount);
        Assert.Equal(4, result.OriginalLineCount);
        Assert.Equal(4, result.UpdatedLineCount);
        Assert.Equal("a\r\nx\r\ny\r\nd\r\n", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task ApplyAsync_AllowsEmptyPatchContentAsDeletion()
    {
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-delete-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-delete-content-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(targetFile, "a\nb\nc\n", new UTF8Encoding(false));
        await File.WriteAllTextAsync(patchFile, string.Empty, new UTF8Encoding(false));

        var result = await TextFilePatchService.ApplyAsync(new TextFilePatchRequest(patchFile, targetFile, 2, 9));

        Assert.Equal(3, result.AppliedToLine);
        Assert.Equal(0, result.PatchLineCount);
        Assert.Equal("a\n", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task ApplyAsync_FromLineBeyondEnd_ThrowsAndKeepsFileUnchanged()
    {
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-invalid-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-invalid-content-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(targetFile, "a\nb\n", new UTF8Encoding(false));
        await File.WriteAllTextAsync(patchFile, "x\n", new UTF8Encoding(false));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TextFilePatchService.ApplyAsync(new TextFilePatchRequest(patchFile, targetFile, 5, 6)));

        Assert.Contains("exceeds the target file line count", ex.Message);
        Assert.Equal("a\nb\n", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task ApplyAsync_PreservesUntouchedMixedNewlinesOutsidePatchedRange()
    {
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-mixed-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-mixed-content-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(targetFile, "a\r\nb\nc\r\nd\n", new UTF8Encoding(false));
        await File.WriteAllTextAsync(patchFile, "x\ny\n", new UTF8Encoding(false));

        await TextFilePatchService.ApplyAsync(new TextFilePatchRequest(patchFile, targetFile, 2, 3));

        Assert.Equal("a\r\nx\r\ny\r\nd\n", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task ApplyAsync_PreservesUtf8BomOnWrite()
    {
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-bom-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-patch-svc-bom-content-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(targetFile, "a\r\nb\r\n", new UTF8Encoding(true));
        await File.WriteAllTextAsync(patchFile, "x\r\n", new UTF8Encoding(false));

        await TextFilePatchService.ApplyAsync(new TextFilePatchRequest(patchFile, targetFile, 2, 2));

        var bytes = await File.ReadAllBytesAsync(targetFile);
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        Assert.Equal("a\r\nx\r\n", await File.ReadAllTextAsync(targetFile));
    }
}