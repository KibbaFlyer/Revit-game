namespace BimArena.Core;

public sealed class Character(V3 feet)
{
    public const double Radius = .28, Height = 1.78, EyeHeight = 1.62, Skin = .003, StepHeight = .30;
    public V3 Feet { get; private set; } = feet;
    public V3 Eye => Feet + V3.Up * EyeHeight;
    public double VerticalSpeed { get; private set; }
    public bool Grounded { get; private set; } = true;
    private readonly List<Contact> contacts = [];

    public void Update(CollisionWorld world, V3 velocity, bool jump, double dt)
    {
        // Call at a fixed timestep. Each capsule move is further subdivided to prevent tunnelling.
        if (jump && Grounded) { VerticalSpeed = 5.1; Grounded = false; }
        var start = Feet;
        Feet = Sweep(world, Feet, velocity.Horizontal * dt);
        var intended = velocity.Horizontal * dt;
        if (Grounded && intended.LengthSquared > 1e-10 && (Feet - start).Horizontal.Length < intended.Length * .85)
        {
            var raised = Sweep(world, start, V3.Up * StepHeight);
            if (raised.Z - start.Z > StepHeight - .005)
            {
                var advanced = Sweep(world, raised, intended);
                // Sweep the whole capsule down: a centre ray misses the tread while the rounded toe is over its edge.
                var step = Sweep(world, advanced, -V3.Up * StepHeight);
                world.Contacts(step - V3.Up * .015, Radius, Height, contacts);
                if (step.Z > start.Z + .001 && contacts.Any(c => c.Walkable) && world.IsFree(step)
                    && (advanced - raised).Horizontal.Length > (Feet - start).Horizontal.Length + .001)
                    Feet = step;
            }
        }
        VerticalSpeed = Math.Max(-25, VerticalSpeed - 18 * dt);
        var beforeGravity = Feet;
        Feet = Sweep(world, Feet, V3.Up * (VerticalSpeed * dt));
        if (VerticalSpeed > 0 && Feet.Z - beforeGravity.Z < VerticalSpeed * dt * .5) VerticalSpeed = 0;
        var support = world.FindFloor(Feet + V3.Up * .045, .10);
        world.Contacts(Feet - V3.Up * .015, Radius, Height, contacts);
        Grounded = VerticalSpeed <= 0 && (contacts.Any(c => c.Walkable)
            || (support is { } ground && Math.Abs(Feet.Z - ground.Z) < .045));
        if (Grounded && support is { } onFloor)
        {
            if (world.IsFree(onFloor)) Feet = onFloor;
            VerticalSpeed = 0;
        }
    }

    public V3 Sweep(CollisionWorld world, V3 start, V3 delta)
    {
        int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / (Radius * .35)));
        var increment = delta / steps; var position = start;
        for (int i = 0; i < steps; i++)
        {
            var next = position + increment;
            for (int iteration = 0; iteration < 8; iteration++)
            {
                world.Contacts(next, Radius, Height, contacts);
                if (contacts.Count == 0) break;
                // Resolve the deepest contact, then query again: coplanar triangles must not sum impulses.
                var deepest = contacts.MaxBy(c => c.Depth);
                if (increment.Z < 0)
                {
                    // Floor/tread-edge support must not push the body sideways on each gravity step.
                    // Preserve horizontal position and solve separation along the vertical motion axis.
                    Contact support = default;
                    foreach (var contact in contacts)
                        if (contact.Walkable && contact.Depth > support.Depth) support = contact;
                    if (support.Walkable && support.Depth >= deepest.Depth - 1e-5)
                    {
                        next += V3.Up * ((support.Depth + Skin) / support.Normal.Z);
                        continue;
                    }
                }
                next += deepest.Normal * (deepest.Depth + Skin);
            }
            world.Contacts(next, Radius, Height, contacts);
            if (contacts.Any(c => c.Depth > .001)) break;
            // On a steep face discard artificial upward motion from edge normals.
            if (Math.Abs(increment.Z) < 1e-9 && next.Z > position.Z + .04)
            {
                var below = world.Raycast(next + V3.Up * .01, -V3.Up, .12);
                if (below == null || below.Value.Normal.Z < .68) break;
            }
            position = next;
        }
        return position;
    }
}
