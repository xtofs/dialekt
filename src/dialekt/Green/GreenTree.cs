namespace Dialekt.Green;

/// <summary>A finished (sealed) arena together with the handle of its root node.</summary>
public sealed class GreenTree(GreenArena arena, GreenNodeHandle root) : IDisposable
{
    public GreenArena Arena { get; } = arena;
    public GreenNodeHandle RootHandle { get; } = root;
    public GreenNodeReader Root => Arena.GetReader(RootHandle);
    public int FullWidth => Root.FullWidth;

    /// <summary>Number of nodes reachable from the root, counting a shared node once per occurrence (trivia included).</summary>
    public int CountNodes()
    {
        var count = 0;
        var pending = new Stack<GreenNodeHandle>();
        pending.Push(RootHandle);
        while (pending.TryPop(out var handle))
        {
            count++;
            var node = Arena.GetReader(handle);
            for (var i = 0; i < node.ChildCount; i++)
            {
                pending.Push(node.GetChildHandle(i));
            }
        }
        return count;
    }

    /// <summary>
    /// Copies the nodes reachable from the root into a fresh arena. Forking keeps every node of every earlier version,
    /// so a long-lived tree should be compacted from time to time. Handles of the result are unrelated to this tree's.
    /// </summary>
    public GreenTree Compact()
    {
        var target = new GreenArena(Arena.Length / 2);
        var cache = new GreenNodeCache(target);
        var copied = new Dictionary<GreenNodeHandle, GreenNodeHandle>();
        var root = Copy(Arena, cache, copied, RootHandle);
        target.Seal();
        return new GreenTree(target, root);
    }

    private static GreenNodeHandle Copy(GreenArena source, GreenNodeCache target, Dictionary<GreenNodeHandle, GreenNodeHandle> copied, GreenNodeHandle handle)
    {
        if (copied.TryGetValue(handle, out var existing)) return existing;

        var node = source.GetReader(handle);
        var children = new GreenNodeHandle[node.ChildCount];
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = Copy(source, target, copied, node.GetChildHandle(i));
        }
        var result = target.GetOrAdd(node.Kind, node.FullWidth, children, node.Flags);
        copied[handle] = result;
        return result;
    }

    public void Dispose() => Arena.Dispose();
}
