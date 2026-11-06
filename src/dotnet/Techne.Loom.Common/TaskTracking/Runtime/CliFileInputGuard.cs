using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class CliFileInputGuard
{
    public static IReadOnlyDictionary<string, string> RequireExistingFiles(
        params (string OptionName, string? Path)[] requirements)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (optionName, path) in requirements)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            resolved[optionName] = RequireExistingFile(path, optionName);
        }

        return resolved;
    }

    public static string RequireExistingFile(string path, string optionName)
    {
        if (string.IsNullOrWhiteSpace(optionName))
        {
            throw new ArgumentException("A file option name is required.", nameof(optionName));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"File option '{optionName}' requires a path to an existing file.");
        }

        string fullPath;
        try
        {
            fullPath = System.IO.Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException($"File option '{optionName}' received an invalid file path.", exception);
        }

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"File option '{optionName}' requires an existing file, but '{fullPath}' was not found.");
        }

        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"File option '{optionName}' requires a file, but '{fullPath}' is a directory.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException($"File option '{optionName}' requires a readable file, but '{fullPath}' could not be opened.", exception);
        }

        return fullPath;
    }

    public static void RequireDifferentPaths(string firstPath, string firstOptionName, string secondPath, string secondOptionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstOptionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondOptionName);

        var firstFullPath = System.IO.Path.GetFullPath(firstPath);
        var secondFullPath = System.IO.Path.GetFullPath(secondPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(firstFullPath, secondFullPath, comparison))
        {
            throw CreateSameFileException(firstOptionName, secondOptionName);
        }

        if (!File.Exists(secondFullPath))
        {
            return;
        }

        try
        {
            using var first = new FileStream(firstFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var second = new FileStream(secondFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (GetFileIdentity(first.SafeFileHandle) == GetFileIdentity(second.SafeFileHandle))
            {
                throw CreateSameFileException(firstOptionName, secondOptionName);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                $"File options '{firstOptionName}' and '{secondOptionName}' could not be verified as distinct files.",
                exception);
        }
    }

    public static void RejectInlineContentOptions(
        IReadOnlyList<string> args,
        string operation,
        params string[] optionNames)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(optionNames);

        var rejected = optionNames.ToHashSet(StringComparer.Ordinal);
        foreach (var argument in args)
        {
            if (!rejected.Contains(argument))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"{operation} accepts file paths only. '{argument}' is inline content and is not supported. Prepare every input file on disk before starting the command.");
        }
    }
    private static InvalidOperationException CreateSameFileException(string firstOptionName, string secondOptionName)
        => new($"File options '{firstOptionName}' and '{secondOptionName}' must not refer to the same file to avoid overwriting an input file.");

    private readonly record struct FileIdentity(ulong Device, ulong FileIndexHigh, ulong FileIndexLow);

    private static FileIdentity GetFileIdentity(SafeFileHandle fileHandle)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetFileInformationByHandle(fileHandle, out var information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return new FileIdentity(information.VolumeSerialNumber, information.FileIndexHigh, information.FileIndexLow);
        }

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Physical file identity checks are supported only on Windows, Linux, and macOS.");
        }

        var buffer = Marshal.AllocHGlobal(256);
        try
        {
            if (FStat(fileHandle.DangerousGetHandle().ToInt32(), buffer) != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var device = OperatingSystem.IsMacOS()
                ? unchecked((uint)Marshal.ReadInt32(buffer, 0))
                : unchecked((ulong)Marshal.ReadInt64(buffer, 0));
            var fileIndex = unchecked((ulong)Marshal.ReadInt64(buffer, 8));
            return new FileIdentity(device, 0, fileIndex);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStat(int fileDescriptor, IntPtr buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public NativeFileTime CreationTime;
        public NativeFileTime LastAccessTime;
        public NativeFileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;
    }
}
