// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

namespace Invicta.Simulation.Primitives;

public abstract class Component
{
    public bool IsDestroyed { get; }

    public Entity Entity { get; }

    public void Destroy()
    {
        throw new NotImplementedException();
    }

    protected virtual void OnStart(SimulationTime time)
    {
        throw new NotImplementedException();
    }

    protected virtual void OnUpdate(SimulationTime time)
    {
        throw new NotImplementedException();
    }

    protected virtual void OnDestroy()
    {
        throw new NotImplementedException();
    }
}
