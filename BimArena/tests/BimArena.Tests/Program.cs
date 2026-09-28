using System.Diagnostics;
using BimArena.Core;

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Check(bool condition, string detail) { if (!condition) throw new Exception(detail); }
void Near(double actual, double expected, double tolerance = .02) => Check(Math.Abs(actual - expected) <= tolerance, $"Expected {expected} ± {tolerance}, got {actual}");
List<Triangle> Floor()
{
    var triangles = new List<Triangle>(); DemoScene.Box(triangles, new(-10, -10, -.3), new(10, 10, 0)); return triangles;
}
void Walk(Character body, CollisionWorld world, V3 velocity, double seconds)
{
    for (int i = 0; i < (int)(seconds * 120); i++) body.Update(world, velocity, false, 1.0 / 120);
}

Test("Ray hits triangles from both sides", () =>
{
    var triangle = new Triangle(new(0,0,0), new(2,0,0), new(0,2,0));
    Check(triangle.Ray(new(.2,.2,3), -V3.Up, out var distance), "Front missed"); Near(distance, 3);
    Check(triangle.Ray(new(.2,.2,-3), V3.Up, out distance), "Back missed"); Near(distance, 3);
    Check(!triangle.Ray(new(3,3,3), -V3.Up, out _), "Outside triangle falsely hit");
});
Test("BVH returns nearest surface and rejects parallel rays", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(2,-2,0), new(2.01,2,3));
    var world = new CollisionWorld(triangles.ToArray());
    Near(world.Raycast(new(0,0,1), new(1,0,0), 10)!.Value.Distance, 2);
    Check(world.Raycast(new(0,0,1), new(0,1,0), 10) == null, "Parallel ray hit");
    Check(world.Raycast(new(0,0,1), new(1,0,0), 1) == null, "Range was ignored");
});
Test("BVH agrees with brute force across deterministic random rays", () =>
{
    var scene = DemoScene.Create(); var world = new CollisionWorld(scene.Triangles); var rng = new Random(32);
    for (int i = 0; i < 500; i++)
    {
        var origin = new V3(rng.NextDouble()*24-12, rng.NextDouble()*20-10, rng.NextDouble()*6-1);
        var direction = new V3(rng.NextDouble()-.5, rng.NextDouble()-.5, rng.NextDouble()-.5).Normalized;
        double best = double.PositiveInfinity;
        foreach (var t in scene.Triangles) if (t.Ray(origin, direction, out double distance) && distance <= 100) best = Math.Min(best, distance);
        var hit = world.Raycast(origin, direction, 100);
        Check(hit.HasValue == double.IsFinite(best), $"Ray {i} disagrees");
        if (hit is { } h) Near(h.Distance, best, 1e-7);
    }
});
Test("Capsule detects a triangle edge at middle height", () =>
{
    var world = new CollisionWorld([new(new(.1,-.1,.8), new(.1,.1,.8), new(.1,0,1))]);
    Check(!world.IsFree(new(0,0,0)), "Capsule axis/edge contact missed");
});
Test("Floor supports an idle player without drift", () =>
{
    var world = new CollisionWorld(Floor().ToArray()); var body = new Character(new(0,0,.003));
    Walk(body, world, V3.Zero, 10); Near(body.Feet.Z, .003, .005); Check(body.Grounded, "Player not grounded");
});
Test("Sprint cannot tunnel through a 10 mm wall", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(0,-5,0), new(.01,5,3));
    var world = new CollisionWorld(triangles.ToArray()); var body = new Character(new(-3,0,.003));
    Walk(body, world, new(5.8,0,0), 3);
    Check(body.Feet.X <= -.275 && body.Feet.X > -.4, $"Wall crossed: {body.Feet}");
});
Test("Diagonal movement slides along walls", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(0,-5,0), new(.05,5,3));
    var world = new CollisionWorld(triangles.ToArray()); var body = new Character(new(-1,-2,.003));
    Walk(body, world, new(2,2,0), 1.5);
    Check(body.Feet.X < -.27 && body.Feet.Y > .8, $"Failed to slide: {body.Feet}");
});
Test("Real doorway stays passable", () =>
{
    var world = new CollisionWorld(DemoScene.Create().Triangles); var body = new Character(new(-2,0,.003));
    Walk(body, world, new(2,0,0), 2); Check(body.Feet.X > 1.8, $"Door blocked: {body.Feet}");
});
Test("Body cannot fit through a 400 mm gap", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(0,-4,0), new(.1,-.2,3)); DemoScene.Box(triangles, new(0,.2,0), new(.1,4,3));
    var world = new CollisionWorld(triangles.ToArray()); var body = new Character(new(-2,0,.003));
    Walk(body, world, new(4,0,0), 2); Check(body.Feet.X < 0, "Player squeezed through narrow gap");
});
Test("Player climbs ordinary 180 mm stair treads", () =>
{
    var triangles = Floor();
    for (int i = 0; i < 4; i++) DemoScene.Box(triangles, new(i*.5,-2,0), new((i+1)*.5,2,(i+1)*.18));
    var world = new CollisionWorld(triangles.ToArray()); var body = new Character(new(-1,0,.003));
    Walk(body, world, new(1,0,0), 2.8);
    var raised = body.Sweep(world, body.Feet, V3.Up * .3);
    var advanced = body.Sweep(world, raised, new(1.0/120,0,0));
    var landed = body.Sweep(world, advanced, -V3.Up * .3);
    var slide = body.Sweep(world, body.Feet, new(1.0/120,0,0));
    Check(body.Feet.X > 1.6 && body.Feet.Z > .69, $"Stair ascent failed: {body.Feet}; grounded={body.Grounded}; slide={slide}; up={raised}; across={advanced}; down={landed}");
});
Test("A tall obstacle cannot be auto-stepped", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(0,-2,0), new(2,2,.9));
    var world = new CollisionWorld(triangles.ToArray()); var body = new Character(new(-2,0,.003));
    Walk(body, world, new(3,0,0), 2); Check(body.Feet.X < 0 && body.Feet.Z < .1, $"Climbed obstacle: {body.Feet}");
});
Test("Low ceiling blocks jumping", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(-5,-5,2), new(5,5,2.2));
    var world = new CollisionWorld(triangles.ToArray()); var body = new Character(new(0,0,.003));
    double maximum = 0;
    for (int i = 0; i < 240; i++) { body.Update(world, V3.Zero, i == 0, 1.0/120); maximum = Math.Max(maximum, body.Feet.Z); }
    Check(maximum < .225, $"Ceiling crossed: {maximum}"); Check(body.Grounded, "Did not land");
});
Test("Spawn uses the clicked floor and rejects insufficient headroom", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(-5,-5,1.5), new(5,5,1.6));
    var world = new CollisionWorld(triangles.ToArray()); bool rejected = false;
    try { world.FindSpawn(V3.Zero); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Spawn placed inside ceiling or changed floors");
});
Test("Furniture blocks shots and movement", () =>
{
    var world = new CollisionWorld(DemoScene.Create().Triangles);
    Check(!world.HasLineOfSight(new(2,3,.7), new(6,3,.7)), "Shot passed through furniture");
    Check(!world.IsFree(new(4,3,.003)), "Player fits inside furniture");
});
Test("Navigation reaches the other room through the doorway", () =>
{
    var world = new CollisionWorld(DemoScene.Create().Triangles); var nav = new Navigation(world, new(-6,0,.003));
    Check(nav.Nodes.Any(n => n.Position.X > 4 && Math.Abs(n.Position.Y) < 1), "Other room was not reachable");
    Check(nav.Nodes.All(n => world.IsFree(n.Position)), "Graph contains embedded spawn nodes");
});
Test("Enemy behind a wall cannot be shot", () =>
{
    var triangles = Floor(); DemoScene.Box(triangles, new(0,-10,0), new(.1,10,3));
    var game = new GameSession(new("wall test", triangles.ToArray(), new(-3,0,0), new(1,0,0)));
    var enemy = new Enemy(1, new(3,0,.003), 70); game.Enemies.Add(enemy); game.Start();
    game.Advance(.02, new(0,0,false,false,true,false));
    Check(enemy.Health == 70, "Enemy took damage through wall"); Check(game.Shells == 7, "Shot was not fired");
});
Test("Hitscan kills visible enemy and awards score", () =>
{
    var game = new GameSession(DemoScene.Create()); game.Enemies.Add(new(1,new(-3,0,.003),70)); game.Start();
    game.Advance(.02, new(0,0,false,false,true,false));
    Check(game.Kills == 1 && game.Score > 0 && game.Enemies.Count == 0, "Visible target survived a close shotgun blast");
});
Test("Pause freezes simulation, frame spikes are bounded, and restart resets run", () =>
{
    var game = new GameSession(DemoScene.Create()); game.Start(); game.Pause();
    var input = new PlayerInput(1,0,false,false,true,false); game.Advance(10,input);
    Near(game.Time,0,1e-8); Check(game.Shells == 8,"Paused shot fired");
    game.Start(); game.Advance(10,input); Check(game.Time <= .101,"Unbounded frame catch-up");
    game.Restart(); Check(game.Health == 100 && game.Shells == 8 && game.Kills == 0 && game.Time == 0,"Reset incomplete");
});
Test("Waves spawn enemies on reachable floor outside the player", () =>
{
    var game = new GameSession(DemoScene.Create()); game.Start();
    for (int i=0;i<300;i++) game.Advance(.01,default);
    Check(game.Wave == 1 && game.Enemies.Count > 0,"No wave spawned");
    Check(game.Enemies.All(e => game.World.IsFree(e.Body.Feet) && (e.Body.Feet-game.Player.Feet).Length > 1),"Invalid enemy spawn");
});
Test("Sphere ray handles origins inside and behind the target", () =>
{
    Near(GameSession.RaySphere(V3.Zero,new(1,0,0),V3.Zero,1)!.Value,0);
    Check(GameSession.RaySphere(V3.Zero,new(1,0,0),new(-3,0,0),1)==null,"Target behind ray hit");
});
Test("Capsule hitbox includes the visible head but rejects shots above it", () =>
{
    Check(GameSession.RayCapsule(new(-3,0,1.62),new(1,0,0),V3.Zero,.4,1.7) != null, "Head-height aim missed");
    Check(GameSession.RayCapsule(new(-3,0,1.9),new(1,0,0),V3.Zero,.4,1.7) == null, "Shot above head falsely hit");
    Near(GameSession.RayCapsule(new(0,0,3),-V3.Up,V3.Zero,.4,1.7)!.Value,1.3);
});
Test("Stepping cannot lift the player through low headroom", () =>
{
    var triangles = Floor();
    DemoScene.Box(triangles,new(0,-3,0),new(3,3,.18));
    DemoScene.Box(triangles,new(-4,-3,1.88),new(4,3,2.1));
    var world = new CollisionWorld(triangles.ToArray()); var body = new Character(new(-2,0,.003));
    Walk(body,world,new(2,0,0),3);
    Check(body.Feet.X < 0 && body.Feet.Z + Character.Height <= 1.89, $"Clipped through low ceiling: {body.Feet}");
});
Test("Sloped floor support does not drift sideways at rest", () =>
{
    Triangle[] ramp = [new(new(-10,-10,-2),new(10,-10,2),new(10,10,2)),new(new(-10,-10,-2),new(10,10,2),new(-10,10,-2))];
    var world = new CollisionWorld(ramp); var body = new Character(new(0,0,.02));
    Walk(body,world,V3.Zero,5);
    Near(body.Feet.X,0,.03); Near(body.Feet.Y,0,.03); Check(body.Grounded,"Ramp support lost");
});

int failures = 0; var timer = Stopwatch.StartNew();
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS  {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL  {name}\n      {ex.Message}"); }
}
Console.WriteLine($"\n{tests.Count - failures}/{tests.Count} passed in {timer.Elapsed.TotalSeconds:F2}s");
return failures == 0 ? 0 : 1;
