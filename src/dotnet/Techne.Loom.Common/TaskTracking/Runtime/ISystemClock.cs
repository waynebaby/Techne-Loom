namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}