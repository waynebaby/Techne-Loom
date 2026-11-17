using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal sealed class WorkflowFragmentCheckCache
{
    private const string Prefix = "loom-fragment-check-";

    private WorkflowFragmentCheckCache(string root) => Root = root;

    internal string Root { get; }

    internal static WorkflowFragmentCheckCache Open(string? path)
    {
        if (path is null)
        {
            return new WorkflowFragmentCheckCache(Directory.CreateTempSubdirectory(Prefix).FullName);
        }

        var directory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(directory.Parent?.FullName, temp, comparison)
            || !directory.Name.StartsWith(Prefix, StringComparison.Ordinal)
            || !directory.Exists || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException("Use a fragment check cache returned by the runtime in the system temporary directory.");
        }

        if (!OperatingSystem.IsWindows()
            && (File.GetUnixFileMode(directory.FullName) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
        {
            throw new InvalidOperationException("The fragment check cache must be private to its owner.");
        }

        return new WorkflowFragmentCheckCache(directory.FullName);
    }

    internal static string? Key(
        string designPath, WorkflowInstance draft, string? contractDigest, string location, ExpressionDefinition expression)
    {
        var references = CSharpExpressionCompiler.ReferenceIdentity;
        if (references is null)
        {
            return null;
        }

        var input = JsonSerializer.Serialize(new
        {
            schema = "loom.fragment-expression-check.v1",
            designPath,
            draft.InstanceId,
            draft.RuntimeBinding,
            draft.RuntimeVersion,
            draft.ExpressionBinding,
            contractDigest,
            location,
            expression,
            compiler = typeof(ExpressionCompilerRouter).Module.ModuleVersionId,
            roslyn = typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation).Module.ModuleVersionId,
            model = typeof(WorkflowInstance).Module.ModuleVersionId,
            references,
            framework = RuntimeInformation.FrameworkDescription,
            rid = RuntimeInformation.RuntimeIdentifier,
        }, WorkflowJsonSerializer.CreateDefaultOptions(indented: false));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    internal bool Contains(string key)
    {
        var entry = new FileInfo(Path.Combine(Root, key));
        try
        {
            return entry.Exists && entry.LinkTarget is null && entry.Length == key.Length
                && string.Equals(File.ReadAllText(entry.FullName), key, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal void Remember(string key)
    {
        var final = Path.Combine(Root, key);
        var temporary = Path.Combine(Root, $"{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, key);
            File.Move(temporary, final, overwrite: true);
        }
        catch (IOException)
        {
            // Cache entries are optional. The caller already has a fresh result.
        }
        catch (UnauthorizedAccessException)
        {
            // Validation still has a fresh result.
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
