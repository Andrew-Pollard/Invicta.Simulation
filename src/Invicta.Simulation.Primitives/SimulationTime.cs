// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

namespace Invicta.Simulation.Primitives;

public readonly record struct SimulationTime
{
    public SimulationTime(TimeSpan elapsed, TimeSpan delta)
    {
        Elapsed = elapsed;
        Delta = delta;
    }

    public TimeSpan Elapsed { get; }
    public TimeSpan Delta { get; }
}
