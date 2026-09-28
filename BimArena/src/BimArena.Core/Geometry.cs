namespace BimArena.Core;

// Local metres, right-handed coordinates, Z up. Revit's double precision is preserved.
public readonly record struct V3(double X, double Y, double Z)
{
    public static readonly V3 Zero = new(0, 0, 0);
    public static readonly V3 Up = new(0, 0, 1);
    public double LengthSquared => Dot(this, this);
    public double Length => Math.Sqrt(LengthSquared);
    public V3 Normalized => Length > 1e-12 ? this / Length : Zero;
    public V3 Horizontal => new(X, Y, 0);
    public double this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;
    public override string ToString() => FormattableString.Invariant($"({X:F3}, {Y:F3}, {Z:F3})");
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator -(V3 a) => new(-a.X, -a.Y, -a.Z);
    public static V3 operator *(V3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static V3 operator /(V3 a, double s) => a * (1 / s);
    public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static V3 Cross(V3 a, V3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static V3 Min(V3 a, V3 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
    public static V3 Max(V3 a, V3 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
}

public readonly record struct Bounds(V3 Min, V3 Max)
{
    public V3 Center => (Min + Max) * .5;
    public V3 Size => Max - Min;
    public Bounds Union(Bounds b) => new(V3.Min(Min, b.Min), V3.Max(Max, b.Max));
    public bool Overlaps(Bounds b) => Min.X <= b.Max.X && Max.X >= b.Min.X && Min.Y <= b.Max.Y && Max.Y >= b.Min.Y && Min.Z <= b.Max.Z && Max.Z >= b.Min.Z;
    public bool Ray(V3 origin, V3 direction, double distance)
    {
        double near = 0, far = distance;
        for (int a = 0; a < 3; a++)
        {
            if (Math.Abs(direction[a]) < 1e-12)
            {
                if (origin[a] < Min[a] || origin[a] > Max[a]) return false;
                continue;
            }
            var t0 = (Min[a] - origin[a]) / direction[a];
            var t1 = (Max[a] - origin[a]) / direction[a];
            if (t0 > t1) (t0, t1) = (t1, t0);
            near = Math.Max(near, t0); far = Math.Min(far, t1);
            if (near > far) return false;
        }
        return true;
    }
}

public readonly record struct Triangle(V3 A, V3 B, V3 C, uint Color = 0xFF9CA7A7)
{
    public V3 Normal => V3.Cross(B - A, C - A).Normalized;
    public Bounds Bounds => new(V3.Min(A, V3.Min(B, C)), V3.Max(A, V3.Max(B, C)));
    public V3 Center => (A + B + C) / 3;

    public bool Ray(V3 o, V3 d, out double distance)
    {
        distance = 0;
        var edge1 = B - A; var edge2 = C - A;
        var p = V3.Cross(d, edge2); var determinant = V3.Dot(edge1, p);
        if (Math.Abs(determinant) < 1e-12) return false;
        var inv = 1 / determinant; var t = o - A;
        var u = V3.Dot(t, p) * inv;
        if (u < -1e-9 || u > 1 + 1e-9) return false;
        var q = V3.Cross(t, edge1); var v = V3.Dot(d, q) * inv;
        if (v < -1e-9 || u + v > 1 + 1e-9) return false;
        distance = V3.Dot(edge2, q) * inv;
        return distance >= 0;
    }

    public V3 ClosestPoint(V3 p)
    {
        var ab = B - A; var ac = C - A; var ap = p - A;
        var d1 = V3.Dot(ab, ap); var d2 = V3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return A;
        var bp = p - B; var d3 = V3.Dot(ab, bp); var d4 = V3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return B;
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return A + ab * (d1 / (d1 - d3));
        var cp = p - C; var d5 = V3.Dot(ab, cp); var d6 = V3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return C;
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return A + ac * (d2 / (d2 - d6));
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return B + (C - B) * ((d4 - d3) / (d4 - d3 + d5 - d6));
        var denom = 1 / (va + vb + vc);
        return A + ab * (vb * denom) + ac * (vc * denom);
    }

    // Exact closest points between the capsule's axis segment and a triangle.
    public (V3 Axis, V3 Surface) ClosestSegment(V3 p, V3 q)
    {
        var direction = q - p;
        if (Ray(p, direction, out var hit) && hit <= 1) { var at = p + direction * hit; return (at, at); }
        var best = (Axis: p, Surface: ClosestPoint(p));
        double bestSq = (best.Axis - best.Surface).LengthSquared;
        Consider(q, ClosestPoint(q));
        Edge(A, B); Edge(B, C); Edge(C, A);
        return best;

        void Consider(V3 x, V3 y)
        {
            var sq = (x - y).LengthSquared;
            if (sq < bestSq) { bestSq = sq; best = (x, y); }
        }
        void Edge(V3 a, V3 b)
        {
            var (x, y) = ClosestSegments(p, q, a, b); Consider(x, y);
        }
    }

    private static (V3, V3) ClosestSegments(V3 p, V3 q, V3 a, V3 b)
    {
        var u = q - p; var v = b - a; var w = p - a;
        double uu = V3.Dot(u, u), uv = V3.Dot(u, v), vv = V3.Dot(v, v), uw = V3.Dot(u, w), vw = V3.Dot(v, w);
        double s, t;
        if (uu < 1e-12) { s = 0; t = vv < 1e-12 ? 0 : Math.Clamp(vw / vv, 0, 1); }
        else if (vv < 1e-12) { t = 0; s = Math.Clamp(-uw / uu, 0, 1); }
        else
        {
            var denom = uu * vv - uv * uv;
            s = denom > 1e-12 ? Math.Clamp((uv * vw - uw * vv) / denom, 0, 1) : 0;
            t = (uv * s + vw) / vv;
            if (t < 0) { t = 0; s = Math.Clamp(-uw / uu, 0, 1); }
            else if (t > 1) { t = 1; s = Math.Clamp((uv - uw) / uu, 0, 1); }
        }
        return (p + u * s, a + v * t);
    }
}

public sealed record SceneData(string Name, Triangle[] Triangles, V3 SpawnHint, V3 Forward);
