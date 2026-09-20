// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

namespace Invicta.Simulation.Primitives;

/// <summary>A component that overrides no hook, as a component holding only state would.</summary>
internal sealed class BareComponent : Component
{
}

/// <summary>A component with state but no behaviour, for lookups by concrete type.</summary>
internal sealed class FuelTank : Component
{
    public double Litres { get; set; }
}

/// <summary>What something that drives an entity along offers, for lookups by interface.</summary>
internal interface IPropulsion
{
    public double Thrust { get; }
}

/// <summary>A family of components an application would share a base class between.</summary>
internal abstract class Propulsion : Component, IPropulsion
{
    public abstract double Thrust { get; }
}

/// <summary>One kind of propulsion, for lookups by base class.</summary>
internal sealed class JetEngine : Propulsion
{
    public override double Thrust => 100.0;
}

/// <summary>Another kind of propulsion, so that a lookup by the base class can be ambiguous.</summary>
internal sealed class Propeller : Propulsion
{
    public override double Thrust => 10.0;
}
