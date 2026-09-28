namespace BimArena.Core;

public readonly record struct RayHit(double Distance, V3 Point, V3 Normal, int TriangleIndex);
public readonly record struct Contact(V3 Normal, double Depth, bool Walkable);

/// <summary>Immutable BVH over the same triangles used for drawing. Queries are two-sided.</summary>
public sealed class CollisionWorld
{
    private sealed record Node(Bounds Bounds, int Start, int Count, Node? Left = null, Node? Right = null);
    private readonly Triangle[] triangles;
    private readonly int[] order;
    private readonly Node root;
    public Bounds Bounds => root.Bounds;
    public IReadOnlyList<Triangle> Triangles => triangles;

    public CollisionWorld(Triangle[] source)
    {
        triangles = source.Where(t => V3.Cross(t.B - t.A, t.C - t.A).LengthSquared > 1e-16).ToArray();
        if (triangles.Length == 0) throw new ArgumentException("The view has no solid geometry to play in.");
        order = Enumerable.Range(0, triangles.Length).ToArray();
        root = Build(0, order.Length);
    }

    private Node Build(int start, int count)
    {
        var box = triangles[order[start]].Bounds;
        for (int i = start + 1; i < start + count; i++) box = box.Union(triangles[order[i]].Bounds);
        if (count <= 8) return new(box, start, count);
        var size = box.Size; int axis = size.X > size.Y ? 0 : 1;
        if (size.Z > size[axis]) axis = 2;
        Array.Sort(order, start, count, Comparer<int>.Create((a, b) => triangles[a].Center[axis].CompareTo(triangles[b].Center[axis])));
        int half = count / 2;
        return new(box, start, count, Build(start, half), Build(start + half, count - half));
    }

    public RayHit? Raycast(V3 origin, V3 direction, double maxDistance)
    {
        if (direction.LengthSquared < 1e-20 || maxDistance <= 0) return null;
        direction = direction.Normalized;
        double closest = maxDistance; int index = -1;
        Visit(root);
        if (index < 0) return null;
        var normal = triangles[index].Normal;
        if (V3.Dot(normal, direction) > 0) normal = -normal;
        return new RayHit(closest, origin + direction * closest, normal, index);

        void Visit(Node node)
        {
            if (!node.Bounds.Ray(origin, direction, closest)) return;
            if (node.Left != null) { Visit(node.Left); Visit(node.Right!); return; }
            for (int i = node.Start; i < node.Start + node.Count; i++)
            {
                int id = order[i];
                if (triangles[id].Ray(origin, direction, out var distance) && distance <= closest)
                { closest = distance; index = id; }
            }
        }
    }

    public bool HasLineOfSight(V3 a, V3 b)
    {
        var delta = b - a;
        return Raycast(a, delta, Math.Max(0, delta.Length - .015)) == null;
    }

    public void Contacts(V3 feet, double radius, double height, List<Contact> result)
    {
        result.Clear();
        V3 a = feet + V3.Up * radius, b = feet + V3.Up * (height - radius);
        var bounds = new Bounds(feet - new V3(radius, radius, 0), feet + new V3(radius, radius, height));
        Visit(root);
        void Visit(Node node)
        {
            if (!node.Bounds.Overlaps(bounds)) return;
            if (node.Left != null) { Visit(node.Left); Visit(node.Right!); return; }
            for (int i = node.Start; i < node.Start + node.Count; i++)
            {
                var triangle = triangles[order[i]];
                var (axis, surface) = triangle.ClosestSegment(a, b);
                var delta = axis - surface; var distance = delta.Length;
                if (distance >= radius - 1e-7) continue;
                var normal = distance > 1e-9 ? delta / distance : triangle.Normal;
                if (distance <= 1e-9 && V3.Dot((a + b) * .5 - triangle.A, normal) < 0) normal = -normal;
                result.Add(new Contact(normal, radius - distance, Math.Abs(triangle.Normal.Z) >= .68 && normal.Z > .1));
            }
        }
    }

    public bool IsFree(V3 feet, double radius = Character.Radius, double height = Character.Height)
    {
        var contacts = new List<Contact>(); Contacts(feet, radius, height, contacts);
        return contacts.All(c => c.Depth < .001);
    }

    public V3? FindFloor(V3 from, double maxDrop)
    {
        var hit = Raycast(from, -V3.Up, maxDrop);
        return hit is { Normal.Z: >= .68 } h ? h.Point + V3.Up * Character.Skin : null;
    }

    public V3 FindSpawn(V3 hint)
    {
        // A floor click is authoritative: search locally only, never silently spawn on another level.
        for (int ring = 0; ring <= 8; ring++)
        {
            int samples = ring == 0 ? 1 : 16;
            for (int i = 0; i < samples; i++)
            {
                double angle = i * Math.Tau / samples;
                var candidate = hint + new V3(Math.Cos(angle), Math.Sin(angle), 0) * (ring * .25);
                var floor = FindFloor(candidate + V3.Up * .4, .85);
                if (floor is { } p && IsFree(p)) return p;
            }
        }
        throw new InvalidOperationException("No standing space near the selected point. Click an open floor surface with at least 1.8 m headroom in the visible 3D geometry.");
    }
}
