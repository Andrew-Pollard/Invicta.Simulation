// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Invicta.Simulation.Primitives;

public sealed class Entity
{
    public bool IsDestroyed { get; }

    public Simulation Simulation { get; }

    public TComponent AddComponent<TComponent>(TComponent component)
    where TComponent : Component
    {
        throw new NotImplementedException();
    }

    public TImplementation AddComponent<TComponent, TImplementation>(TImplementation component)
        where TComponent : class
        where TImplementation : Component, TComponent
    {
        throw new NotImplementedException();
    }

    public TComponent AddKeyedComponent<TComponent>(object? key, TComponent component)
    where TComponent : Component
    {
        throw new NotImplementedException();
    }

    public TComponent AddKeyedComponent<TComponent, TImplementation>(object? key, TImplementation component)
        where TComponent : class
        where TImplementation : Component, TComponent
    {
        throw new NotImplementedException();
    }

    public TComponent GetComponent<TComponent>()
        where TComponent : class
    {
        throw new NotImplementedException();
    }

    public TComponent GetKeyedComponent<TComponent>(object? key)
        where TComponent : class
    {
        throw new NotImplementedException();
    }

    public bool TryGetComponent<TComponent>([NotNullWhen(true)] out TComponent? component)
        where TComponent : class
    {
        throw new NotImplementedException();
    }

    public bool TryGetKeyedComponent<TComponent>(object? key, [NotNullWhen(true)] out TComponent? component)
    where TComponent : class
    {
        throw new NotImplementedException();
    }

    public IReadOnlyCollection<TComponent> GetComponents<TComponent>()
    where TComponent : class
    {
        throw new NotImplementedException();
    }

    public IReadOnlyCollection<TComponent> GetKeyedComponents<TComponent>(object? key)
        where TComponent : class
    {
        throw new NotImplementedException();
    }

    public void Destroy()
    {
        throw new NotImplementedException();
    }
}
