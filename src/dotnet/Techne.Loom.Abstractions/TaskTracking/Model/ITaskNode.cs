using System.Text.Json.Serialization;

namespace Techne.Loom.Abstractions.TaskTracking.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = JsonPolymorphicConsts.TypeDiscriminatorPropertyName)]
[JsonDerivedType(typeof(StateNode), JsonPolymorphicConsts.StateKind)]
[JsonDerivedType(typeof(CommandTransition), JsonPolymorphicConsts.CommandKind)]
[JsonDerivedType(typeof(ExpressionTransition), JsonPolymorphicConsts.ExpressionKind)]
[JsonDerivedType(typeof(ToBeRefinedTransition), JsonPolymorphicConsts.ToBeRefinedKind)]
public interface ITaskNode
{
    string Id { get; }

    string Name { get; }

    string? Description { get; }
}

public static class JsonPolymorphicConsts
{
    public const string TypeDiscriminatorPropertyName = "$kind";
    public const string StateKind = "state";
    public const string CommandKind = "command";
    public const string ExpressionKind = "expr";
    public const string ToBeRefinedKind = "tbr";
}
