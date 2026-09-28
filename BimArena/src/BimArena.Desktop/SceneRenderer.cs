using BimArena.Core;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BimArena.Desktop;

internal sealed class SceneRenderer
{
    public Viewport3D View { get; } = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly PerspectiveCamera camera = new() { UpDirection = new(0, 0, 1), FieldOfView = 88, NearPlaneDistance = .055, FarPlaneDistance = 2500 };
    private readonly Model3DGroup actors = new();
    private readonly Dictionary<int, (GeometryModel3D Model, TranslateTransform3D Transform)> enemies = [];
    private readonly List<GeometryModel3D> transient = [];
    private static readonly Material enemyMaterial = Material(0xFFAC47E8);
    private static readonly Material hotMaterial = Material(0xFFFFECBE, true);
    private static readonly Material boltMaterial = Material(0xFFFF6947, true);
    private static readonly Material healthMaterial = Material(0xFF54ECC4, true);
    private static readonly Material ammoMaterial = Material(0xFFFFBF62, true);
    private static readonly MeshGeometry3D bot = RobotMesh();
    private static readonly MeshGeometry3D orb = Octahedron(.10);
    private static readonly MeshGeometry3D pickup = Octahedron(.22);

    public SceneRenderer(SceneData scene)
    {
        View.Camera = camera;
        var world = new Model3DGroup();
        // Limit individual WPF meshes to 18k vertices and batch by material colour.
        // Geometry is opaque: glass is tinted but remains visibly solid and collidable.
        foreach (var group in scene.Triangles.GroupBy(t => t.Color | 0xFF000000))
        {
            var material = Material(group.Key);
            foreach (var chunk in group.Chunk(6000))
            {
                var mesh = new MeshGeometry3D();
                foreach (var t in chunk)
                {
                    int i = mesh.Positions.Count;
                    mesh.Positions.Add(Point(t.A)); mesh.Positions.Add(Point(t.B)); mesh.Positions.Add(Point(t.C));
                    mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
                    var normal = Vector(t.Normal);
                    mesh.Normals.Add(normal); mesh.Normals.Add(normal); mesh.Normals.Add(normal);
                }
                mesh.Freeze();
                var model = new GeometryModel3D(mesh, material) { BackMaterial = material }; model.Freeze(); world.Children.Add(model);
            }
        }
        world.Freeze();
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Color.FromRgb(145, 151, 168)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(210, 202, 182), new(-.5, .7, -1)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(76, 94, 122), new(.4, -.3, .2)));
        lights.Freeze();
        View.Children.Add(new ModelVisual3D { Content = lights });
        View.Children.Add(new ModelVisual3D { Content = world });
        View.Children.Add(new ModelVisual3D { Content = actors });
        RenderOptions.SetEdgeMode(View, EdgeMode.Aliased);
    }

    public void Update(GameSession game)
    {
        camera.Position = Point(game.Player.Eye);
        camera.LookDirection = Vector(game.Look);
        foreach (int id in enemies.Keys.Where(id => game.Enemies.All(e => e.Id != id)).ToArray())
        { actors.Children.Remove(enemies[id].Model); enemies.Remove(id); }
        foreach (var e in game.Enemies)
        {
            if (!enemies.TryGetValue(e.Id, out var visual))
            {
                var transform = new TranslateTransform3D();
                var model = new GeometryModel3D(bot, enemyMaterial) { BackMaterial = enemyMaterial, Transform = transform };
                visual = (model, transform); enemies.Add(e.Id, visual); actors.Children.Add(model);
            }
            var feet = e.Body.Feet;
            visual.Transform.OffsetX = feet.X; visual.Transform.OffsetY = feet.Y;
            visual.Transform.OffsetZ = feet.Z + Math.Sin(game.Time * 5 + e.Id) * .035;
            visual.Model.Material = visual.Model.BackMaterial = e.Flash > 0 ? hotMaterial : enemyMaterial;
        }
        foreach (var model in transient) actors.Children.Remove(model);
        transient.Clear();
        foreach (var b in game.Bolts) Add(orb, boltMaterial, b.Position);
        foreach (var p in game.Pickups) Add(pickup, p.IsHealth ? healthMaterial : ammoMaterial, p.Position + V3.Up * (.35 + .06 * Math.Sin(game.Time * 4)));
        foreach (var i in game.Impacts) Add(orb, i.Enemy ? hotMaterial : ammoMaterial, i.Position);
    }

    private void Add(MeshGeometry3D mesh, Material material, V3 position)
    {
        var model = new GeometryModel3D(mesh, material) { BackMaterial = material, Transform = new TranslateTransform3D(position.X, position.Y, position.Z) };
        transient.Add(model); actors.Children.Add(model);
    }
    private static Point3D Point(V3 v) => new(v.X, v.Y, v.Z);
    private static Vector3D Vector(V3 v) => new(v.X, v.Y, v.Z);
    private static Material Material(uint argb, bool glow = false)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)); brush.Freeze();
        Material material = glow ? new EmissiveMaterial(brush) : new DiffuseMaterial(brush); material.Freeze(); return material;
    }

    private static MeshGeometry3D RobotMesh()
    {
        var builder = new List<Triangle>();
        DemoScene.Box(builder, new(-.30, -.25, .55), new(.30, .25, 1.30), 0);
        DemoScene.Box(builder, new(-.22, -.22, 1.32), new(.22, .22, 1.65), 0);
        DemoScene.Box(builder, new(-.43, -.17, .70), new(-.32, .17, 1.22), 0);
        DemoScene.Box(builder, new(.32, -.17, .70), new(.43, .17, 1.22), 0);
        DemoScene.Box(builder, new(-.25, -.18, .05), new(-.07, .18, .53), 0);
        DemoScene.Box(builder, new(.07, -.18, .05), new(.25, .18, .53), 0);
        return Mesh(builder);
    }
    private static MeshGeometry3D Octahedron(double size)
    {
        var top = V3.Up * size; var bottom = -top;
        V3[] ring = [new(size, 0, 0), new(0, size, 0), new(-size, 0, 0), new(0, -size, 0)];
        var triangles = new List<Triangle>();
        for (int i = 0; i < 4; i++) { var j = (i + 1) % 4; triangles.Add(new(top, ring[i], ring[j])); triangles.Add(new(bottom, ring[j], ring[i])); }
        return Mesh(triangles);
    }
    private static MeshGeometry3D Mesh(IEnumerable<Triangle> triangles)
    {
        var mesh = new MeshGeometry3D();
        foreach (var t in triangles)
        {
            int i = mesh.Positions.Count; mesh.Positions.Add(Point(t.A)); mesh.Positions.Add(Point(t.B)); mesh.Positions.Add(Point(t.C));
            mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
        }
        mesh.Freeze(); return mesh;
    }
}
