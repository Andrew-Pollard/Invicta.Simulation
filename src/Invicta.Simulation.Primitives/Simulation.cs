// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

namespace Invicta.Simulation.Primitives;

public sealed class Simulation : IDisposable
{
    public bool IsDisposed { get; }

    public TimeSpan ElapsedTime { get; }

    public Entity CreateEntity()
    {
        throw new NotImplementedException();
    }

    public IReadOnlyCollection<Entity> GetAllEntities()
    {
        throw new NotImplementedException();
    }

    public void Step(TimeSpan deltaTime)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
