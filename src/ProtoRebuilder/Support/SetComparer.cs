using System.Diagnostics.CodeAnalysis;

namespace Knapcode.ProtoRebuilder;

public class SetComparer<T> : IEqualityComparer<IReadOnlySet<T>>
{
    private readonly IComparer<T> _comparer;

    public static SetComparer<T> Instance { get; } = new SetComparer<T>();

    public SetComparer() : this(Comparer<T>.Default)
    {
    }

    public SetComparer(IComparer<T> comparer)
    {
        _comparer = comparer;
    }

    public bool Equals(IReadOnlySet<T>? x, IReadOnlySet<T>? y)
    {
        if (x is null && y is null)
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        if (x.Count != y.Count)
        {
            return false;
        }

        if (x.Count == 0 && y.Count == 0)
        {
            return true;
        }

        return x.SetEquals(y);
    }

    public int GetHashCode([DisallowNull] IReadOnlySet<T> obj)
    {
        var hashCode = new HashCode();
        foreach (var i in obj.OrderBy(x => x, _comparer))
        {
            hashCode.Add(i);
        }

        return hashCode.ToHashCode();
    }
}
