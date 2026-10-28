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
