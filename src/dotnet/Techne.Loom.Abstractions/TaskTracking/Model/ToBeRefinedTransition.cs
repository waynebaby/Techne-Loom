namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed record ToBeRefinedTransition : TransitionBase
{
    public string? DesignNotes { get; set; }
}
