namespace BimArena.Core;

public readonly record struct PlayerInput(double Forward, double Strafe, bool Run, bool Jump, bool Fire, bool Reload);
public enum RunState { Ready, Playing, Paused, Defeated }
public sealed class Enemy(int id, V3 position, int health)
{
    public int Id { get; } = id;
    public Character Body { get; } = new(position);
    public int Health { get; set; } = health;
    public double AttackDelay { get; set; } = 1.5;
    public double Flash { get; set; }
    public V3 Waypoint { get; set; } = position;
}
public sealed class Bolt(V3 position, V3 velocity)
{
    public V3 Position { get; set; } = position;
    public V3 Velocity { get; } = velocity;
    public double Life { get; set; } = 5;
}
public sealed class Pickup(V3 position, bool health)
{
    public V3 Position { get; } = position;
    public bool IsHealth { get; } = health;
    public double Life { get; set; } = 45;
}
public sealed class Impact(V3 position, bool enemy)
{
    public V3 Position { get; } = position;
    public bool Enemy { get; } = enemy;
    public double Life { get; set; } = .18;
}

public sealed class GameSession
{
    public SceneData Scene { get; }
    public CollisionWorld World { get; }
    public Navigation Navigation { get; }
    public Character Player { get; private set; }
    public RunState State { get; private set; } = RunState.Ready;
    public List<Enemy> Enemies { get; } = [];
    public List<Bolt> Bolts { get; } = [];
    public List<Pickup> Pickups { get; } = [];
    public List<Impact> Impacts { get; } = [];
    public double Yaw { get; private set; }
    public double Pitch { get; private set; }
    public int Health { get; private set; } = 100;
    public int Shells { get; private set; } = 8;
    public int Reserve { get; private set; } = 48;
    public int Wave { get; private set; }
    public int Kills { get; private set; }
    public int Score { get; private set; }
    public double Time { get; private set; }
    public double ShotFlash { get; private set; }
    public double DamageFlash { get; private set; }
    public double HitFlash { get; private set; }
    public double ReloadRemaining { get; private set; }
    public double NextWaveIn { get; private set; } = 2.5;
    public string Message { get; private set; } = "Clear each wave. The building is your arena.";
    public V3 Look => new(Math.Cos(Yaw) * Math.Cos(Pitch), Math.Sin(Yaw) * Math.Cos(Pitch), Math.Sin(Pitch));
    public event Action? Shot;
    private readonly V3 spawn;
    private readonly Random random = new(773);
    private double accumulator, weaponDelay, flowDelay;
    private int nextEnemyId;
    private bool jumpHeld;

    public GameSession(SceneData scene)
    {
        Scene = scene; World = new(scene.Triangles);
        spawn = World.FindSpawn(scene.SpawnHint); Player = new(spawn);
        Navigation = new(World, spawn);
        Yaw = Math.Atan2(scene.Forward.Y, scene.Forward.X);
        if (Navigation.Nodes.Count < 8)
            throw new InvalidOperationException("This spot has too little reachable floor for an arena. Choose a larger open floor area.");
    }

    public void Start() { if (State == RunState.Ready || State == RunState.Paused) State = RunState.Playing; accumulator = 0; }
    public void Pause() { if (State == RunState.Playing) State = RunState.Paused; accumulator = 0; }
    public void Restart()
    {
        Player = new(spawn); Health = 100; Shells = 8; Reserve = 48;
        Wave = Kills = Score = 0; Time = weaponDelay = ReloadRemaining = 0;
        ShotFlash = DamageFlash = HitFlash = flowDelay = accumulator = 0;
        NextWaveIn = 2.5; Enemies.Clear(); Bolts.Clear(); Pickups.Clear(); Impacts.Clear();
        Pitch = 0; Yaw = Math.Atan2(Scene.Forward.Y, Scene.Forward.X); jumpHeld = false;
        Message = "Clear each wave. The building is your arena."; State = RunState.Playing;
    }

    public void LookBy(double yaw, double pitch)
    {
        if (State != RunState.Playing) return;
        Yaw = Math.IEEERemainder(Yaw + yaw, Math.Tau);
        Pitch = Math.Clamp(Pitch + pitch, -1.35, 1.35);
    }

    public void Advance(double elapsed, PlayerInput input)
    {
        if (State != RunState.Playing) return;
        accumulator += Math.Clamp(elapsed, 0, .10);
        const double step = 1.0 / 120;
        while (accumulator >= step && State == RunState.Playing)
        { Tick(step, input); accumulator -= step; }
    }

    private void Tick(double dt, PlayerInput input)
    {
        Time += dt;
        ShotFlash = Math.Max(0, ShotFlash - dt); DamageFlash = Math.Max(0, DamageFlash - dt); HitFlash = Math.Max(0, HitFlash - dt);
        weaponDelay = Math.Max(0, weaponDelay - dt);
        var forward = new V3(Math.Cos(Yaw), Math.Sin(Yaw), 0);
        var right = new V3(Math.Sin(Yaw), -Math.Cos(Yaw), 0);
        var movement = forward * input.Forward + right * input.Strafe;
        if (movement.Length > 1) movement = movement.Normalized;
        Player.Update(World, movement * (input.Run ? 5.8 : 3.7), input.Jump && !jumpHeld, dt);
        jumpHeld = input.Jump;
        if (Player.Feet.Z < World.Bounds.Min.Z - 8) Damage(100);
        if (State == RunState.Defeated) return;
        if (ReloadRemaining > 0)
        {
            ReloadRemaining = Math.Max(0, ReloadRemaining - dt);
            if (ReloadRemaining == 0) { int count = Math.Min(8 - Shells, Reserve); Shells += count; Reserve -= count; }
        }
        if ((input.Reload || (input.Fire && Shells == 0)) && Shells < 8 && Reserve > 0 && ReloadRemaining == 0) ReloadRemaining = 1.35;
        if (input.Fire && weaponDelay == 0 && ReloadRemaining == 0 && Shells > 0) Fire();
        flowDelay -= dt;
        if (flowDelay <= 0)
        {
            Navigation.UpdateFlow(Player.Feet);
            foreach (var enemy in Enemies) enemy.Waypoint = Navigation.NextWaypoint(enemy.Body.Feet);
            flowDelay = .45;
        }
        foreach (var enemy in Enemies)
        {
            enemy.Flash = Math.Max(0, enemy.Flash - dt);
            var center = enemy.Body.Feet + V3.Up;
            var delta = Player.Eye - center; double distance = delta.Length;
            bool visible = distance < 22 && World.HasLineOfSight(center, Player.Eye);
            var destination = visible && Math.Abs(Player.Feet.Z - enemy.Body.Feet.Z) < .25 ? Player.Feet : enemy.Waypoint;
            var move = (destination - enemy.Body.Feet).Horizontal;
            double speed = Math.Min(2.4, 1.25 + Wave * .09);
            if (distance < 1.2) move = V3.Zero;
            // Light separation prevents enemies stacking into a single target.
            foreach (var other in Enemies)
            {
                if (ReferenceEquals(other, enemy)) continue;
                var apart = (enemy.Body.Feet - other.Body.Feet).Horizontal;
                if (apart.Length is > .001 and < .7) move += apart.Normalized * .6;
            }
            var velocity = move.Length > .08 ? move.Normalized * speed : V3.Zero;
            enemy.Body.Update(World, velocity, false, dt);
            enemy.AttackDelay -= dt;
            if (visible && enemy.AttackDelay <= 0)
            {
                if (distance < 1.6) Damage(9);
                else Bolts.Add(new(center, delta.Normalized * (7 + Math.Min(Wave, 6) * .4)));
                enemy.AttackDelay = Math.Max(.85, 2.2 - Wave * .08) + random.NextDouble() * .5;
            }
        }
        for (int i = Bolts.Count - 1; i >= 0; i--)
        {
            var bolt = Bolts[i]; var travel = bolt.Velocity * dt; double length = travel.Length;
            var wall = World.Raycast(bolt.Position, travel, length);
            var hitPlayer = RayCapsule(bolt.Position, travel.Normalized, Player.Feet, Character.Radius, Character.Height);
            if (hitPlayer is >= 0 && hitPlayer <= length && (wall == null || hitPlayer < wall.Value.Distance))
            { Damage(12); Bolts.RemoveAt(i); }
            else if (wall != null || (bolt.Life -= dt) <= 0) Bolts.RemoveAt(i);
            else bolt.Position += travel;
        }
        for (int i = Pickups.Count - 1; i >= 0; i--)
        {
            var pickup = Pickups[i];
            bool useful = pickup.IsHealth ? Health < 100 : Reserve < 96;
            if (useful && (Player.Feet - pickup.Position).Length < .9 && World.HasLineOfSight(Player.Eye, pickup.Position + V3.Up * .3))
            {
                if (pickup.IsHealth) Health = Math.Min(100, Health + 25); else Reserve = Math.Min(96, Reserve + 16);
                Message = pickup.IsHealth ? "+25 health" : "+16 shells"; Pickups.RemoveAt(i);
            }
            else if ((pickup.Life -= dt) <= 0) Pickups.RemoveAt(i);
        }
        for (int i = Impacts.Count - 1; i >= 0; i--) if ((Impacts[i].Life -= dt) <= 0) Impacts.RemoveAt(i);
        if (Enemies.Count == 0)
        {
            NextWaveIn -= dt;
            if (NextWaveIn <= 0) SpawnWave();
        }
    }

    private void Fire()
    {
        Shells--; weaponDelay = .58; ShotFlash = .13;
        var right = new V3(Math.Sin(Yaw), -Math.Cos(Yaw), 0);
        var up = V3.Cross(right, Look).Normalized;
        for (int pellet = 0; pellet < 7; pellet++)
        {
            var direction = pellet == 0 ? Look : (Look + right * ((random.NextDouble() - .5) * .085) + up * ((random.NextDouble() - .5) * .085)).Normalized;
            var wall = World.Raycast(Player.Eye, direction, 90);
            double nearest = wall?.Distance ?? 90; Enemy? victim = null;
            foreach (var enemy in Enemies)
            {
                double? distance = RayCapsule(Player.Eye, direction, enemy.Body.Feet, .4, 1.7);
                if (distance is { } d && d < nearest) { nearest = d; victim = enemy; }
            }
            if (victim != null) { victim.Health -= 15; victim.Flash = .12; HitFlash = .16; }
            if (wall != null || victim != null) Impacts.Add(new(Player.Eye + direction * nearest, victim != null));
        }
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = Enemies[i]; if (enemy.Health > 0) continue;
            Kills++; Score += 100 + Wave * 10;
            // Every kill replenishes ammo; alternate health drops keep survival sustainable.
            Reserve = Math.Min(96, Reserve + 2);
            Pickups.Add(new(enemy.Body.Feet, Kills % 3 == 0)); Enemies.RemoveAt(i);
        }
        Shot?.Invoke();
    }

    private void Damage(int amount)
    {
        Health = Math.Max(0, Health - amount); DamageFlash = .3;
        if (Health == 0) { State = RunState.Defeated; Message = "Run ended. Press Enter to redeploy."; }
    }

    private void SpawnWave()
    {
        int nextWave = Wave + 1;
        var candidates = Navigation.Nodes.Select(n => n.Position)
            .Where(p => (p - Player.Feet).Length > 4 && (p - Player.Feet).Length < 28)
            .OrderBy(_ => random.Next()).ToList();
        if (candidates.Count == 0) candidates = Navigation.Nodes.Select(n => n.Position).Where(p => (p - Player.Feet).Length > 2).OrderBy(_ => random.Next()).ToList();
        foreach (var position in candidates)
        {
            if (Enemies.Count >= Math.Min(3 + nextWave, 14)) break;
            if (Enemies.Any(e => (e.Body.Feet - position).Length < 1.3)) continue;
            Enemies.Add(new(nextEnemyId++, position, 65 + Math.Min(nextWave, 10) * 5));
        }
        if (Enemies.Count == 0) { NextWaveIn = 3; Message = "Move back into the starting area to begin the next wave."; return; }
        Wave = nextWave; NextWaveIn = 4; Reserve = Math.Min(96, Reserve + 8);
        Message = $"WAVE {Wave:00}  /  {Enemies.Count} hostiles";
    }

    public static double? RaySphere(V3 origin, V3 direction, V3 center, double radius)
    {
        var offset = origin - center; var b = V3.Dot(offset, direction); var c = offset.LengthSquared - radius * radius;
        if (c <= 0) return 0;
        var disc = b * b - c;
        if (disc < 0) return null;
        var distance = -b - Math.Sqrt(disc);
        return distance >= 0 ? distance : null;
    }

    public static double? RayCapsule(V3 origin, V3 direction, V3 feet, double radius, double height)
    {
        var local = origin - feet;
        double low = radius, high = height - radius;
        var nearestOnAxis = new V3(0, 0, Math.Clamp(local.Z, low, high));
        if ((local - nearestOnAxis).LengthSquared <= radius * radius) return 0;
        double nearest = double.PositiveInfinity;
        foreach (double z in new[] { low, high })
            if (RaySphere(local, direction, V3.Up * z, radius) is { } cap) nearest = Math.Min(nearest, cap);
        double a = direction.Horizontal.LengthSquared;
        double b = local.X * direction.X + local.Y * direction.Y;
        double c = local.Horizontal.LengthSquared - radius * radius;
        double determinant = b * b - a * c;
        if (a > 1e-12 && determinant >= 0)
        {
            foreach (double t in new[] { (-b - Math.Sqrt(determinant)) / a, (-b + Math.Sqrt(determinant)) / a })
                if (t >= 0 && local.Z + direction.Z * t >= low && local.Z + direction.Z * t <= high)
                    nearest = Math.Min(nearest, t);
        }
        return double.IsFinite(nearest) ? nearest : null;
    }
}
