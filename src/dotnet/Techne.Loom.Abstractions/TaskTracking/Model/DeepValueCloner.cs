namespace Techne.Loom.Abstractions.TaskTracking.Model;

/// <summary>
/// Deep-clones JSON-like value trees (dictionaries, lists, arrays, and scalars) so cloned
/// workflow state never shares mutable containers with the source instance. Shared by
/// <see cref="CommandInvocation"/> parameter cloning and workflow instance cloning to keep a
/// single implementation of the clone rules.
/// </summary>
public static class DeepValueCloner
{
    public static object? Clone(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => Clone(pair.Value), StringComparer.Ordinal),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => Clone(pair.Value), StringComparer.Ordinal),
            IReadOnlyDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => Clone(pair.Value), StringComparer.Ordinal),
            Array array => CloneArray(array),
            List<object?> list => list.Select(Clone).ToList(),
            IReadOnlyList<object?> list => list.Select(Clone).ToList(),
            _ => value,
        };
    }

    private static Array CloneArray(Array source)
    {
        var lengths = Enumerable.Range(0, source.Rank).Select(source.GetLength).ToArray();
        var lowerBounds = Enumerable.Range(0, source.Rank).Select(source.GetLowerBound).ToArray();
        var clone = Array.CreateInstance(source.GetType().GetElementType()!, lengths, lowerBounds);
        CopyArrayValues(source, clone, new int[source.Rank], 0);
        return clone;
    }

    private static void CopyArrayValues(Array source, Array target, int[] indices, int dimension)
    {
        if (dimension == source.Rank)
        {
            target.SetValue(Clone(source.GetValue(indices)), indices);
            return;
        }

        var lowerBound = source.GetLowerBound(dimension);
        for (var index = lowerBound; index < lowerBound + source.GetLength(dimension); index++)
        {
            indices[dimension] = index;
            CopyArrayValues(source, target, indices, dimension + 1);
        }
    }
}
