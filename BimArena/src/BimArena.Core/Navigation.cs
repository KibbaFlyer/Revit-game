namespace BimArena.Core;

/// <summary>A bounded graph of reachable floor positions, including steps and ramps.</summary>
public sealed class Navigation
{
    public sealed record Node(V3 Position, List<int> Neighbours);
    public List<Node> Nodes { get; } = [];
    public bool WasLimited { get; private set; }
    private int[] distances = [];
    private const double Cell = .65;

    public Navigation(CollisionWorld world, V3 spawn, int maxNodes = 1800)
    {
        Nodes.Add(new(spawn, []));
        var visited = new Dictionary<(int, int, int), int> { [(0, 0, (int)Math.Round(spawn.Z / .2))] = 0 };
        var pending = new Queue<(int Index, int X, int Y)>(); pending.Enqueue((0, 0, 0));
        (int X, int Y)[] directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (pending.Count > 0 && Nodes.Count < maxNodes)
        {
            var current = pending.Dequeue(); var from = Nodes[current.Index].Position;
            foreach (var d in directions)
            {
                int x = current.X + d.X, y = current.Y + d.Y;
                if (x * x + y * y > 45 * 45) continue;
                var target = new V3(spawn.X + x * Cell, spawn.Y + y * Cell, from.Z);
                var floor = world.FindFloor(target + V3.Up * (Character.StepHeight + .08), Character.StepHeight + .55);
                if (floor is not { } position || !world.IsFree(position)) continue;
                var key = (x, y, (int)Math.Round(position.Z / .2));
                if (visited.TryGetValue(key, out var known) && Nodes[current.Index].Neighbours.Contains(known)) continue;
                if (!CanWalk(world, from, position) || !CanWalk(world, position, from)) continue;
                if (!visited.TryGetValue(key, out int next))
                {
                    if (Nodes.Count >= maxNodes) break;
                    next = Nodes.Count; visited[key] = next;
                    Nodes.Add(new(position, [])); pending.Enqueue((next, x, y));
                }
                Nodes[current.Index].Neighbours.Add(next); Nodes[next].Neighbours.Add(current.Index);
            }
        }
        WasLimited = pending.Count > 0;
        UpdateFlow(spawn);
    }

    private static bool CanWalk(CollisionWorld world, V3 from, V3 to)
    {
        var body = new Character(from);
        var velocity = (to - from).Horizontal.Normalized * 2;
        for (int i = 0; i < 20; i++) body.Update(world, velocity, false, Cell / 40);
        return (body.Feet - to).Length < .12;
    }

    public int Nearest(V3 point)
    {
        int best = 0; double distance = double.MaxValue;
        for (int i = 0; i < Nodes.Count; i++)
        {
            var delta = Nodes[i].Position - point;
            var d = delta.Horizontal.LengthSquared + delta.Z * delta.Z * 9;
            if (d < distance) { distance = d; best = i; }
        }
        return best;
    }

    public void UpdateFlow(V3 target)
    {
        distances = Enumerable.Repeat(int.MaxValue, Nodes.Count).ToArray();
        int start = Nearest(target); distances[start] = 0;
        var pending = new Queue<int>(); pending.Enqueue(start);
        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            foreach (int next in Nodes[current].Neighbours)
                if (distances[next] == int.MaxValue) { distances[next] = distances[current] + 1; pending.Enqueue(next); }
        }
    }

    public V3 NextWaypoint(V3 position)
    {
        int current = Nearest(position);
        int next = current;
        foreach (int neighbour in Nodes[current].Neighbours)
            if (distances[neighbour] < distances[next]) next = neighbour;
        return Nodes[next].Position;
    }
}
