namespace BimArena.Core;

public static class DemoScene
{
    public static SceneData Create()
    {
        var t = new List<Triangle>();
        Box(t, new(-12, -10, -.3), new(12, 10, 0), 0xFF3B4850);
        Box(t, new(-12, -10, 0), new(-11.7, 10, 3.4), 0xFFB5B8AF);
        Box(t, new(11.7, -10, 0), new(12, 10, 3.4), 0xFFB5B8AF);
        Box(t, new(-12, -10, 0), new(12, -9.7, 3.4), 0xFF9DAAA9);
        Box(t, new(-12, 9.7, 0), new(12, 10, 3.4), 0xFF9DAAA9);
        // A full-height partition with an actual two-metre doorway, plus furniture and steps.
        Box(t, new(-.2, -10, 0), new(.2, -1, 3.4), 0xFFCCBEA0);
        Box(t, new(-.2, 1, 0), new(.2, 10, 3.4), 0xFFCCBEA0);
        Box(t, new(-.2, -1, 2.3), new(.2, 1, 3.4), 0xFFCCBEA0);
        Box(t, new(3, 2, 0), new(5, 4, 1), 0xFF608C84);
        Box(t, new(-7, -5, 0), new(-5, -3, 1.1), 0xFF927359);
        Box(t, new(7, -3, 0), new(8, -2, 3.4), 0xFF899B9F);
        for (int i = 0; i < 4; i++) Box(t, new(3 + i * .5, -8, 0), new(3.5 + i * .5, -6, (i + 1) * .18), 0xFF7A8D92);
        return new("Training facility", t.ToArray(), new(-6, 0, 0), new(1, 0, 0));
    }

    public static void Box(List<Triangle> t, V3 min, V3 max, uint color = 0xFF9CA7A7)
    {
        V3[] p = [new(min.X,min.Y,min.Z), new(max.X,min.Y,min.Z), new(max.X,max.Y,min.Z), new(min.X,max.Y,min.Z),
                  new(min.X,min.Y,max.Z), new(max.X,min.Y,max.Z), new(max.X,max.Y,max.Z), new(min.X,max.Y,max.Z)];
        int[] indices = [0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7];
        for (int i = 0; i < indices.Length; i += 3) t.Add(new(p[indices[i]], p[indices[i + 1]], p[indices[i + 2]], color));
    }
}
