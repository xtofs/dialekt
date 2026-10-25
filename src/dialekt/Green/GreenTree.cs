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

    public void Dispose() => Arena.Dispose();
}
