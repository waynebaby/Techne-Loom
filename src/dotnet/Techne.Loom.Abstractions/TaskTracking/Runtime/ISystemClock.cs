

namespace Techne.Loom.Common.TaskTracking.Runtime;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
