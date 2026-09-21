// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

using Invicta.Simulation.Primitives;

namespace Invicta.Simulation.Sample;

internal class Program
{
    private interface IDummyComponent;

    private class DummyComponent : Component, IDummyComponent;

    static void Main(string[] args)
    {
        Primitives.Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        DummyComponent @in = entity.AddComponent<IDummyComponent, DummyComponent>(new DummyComponent());
        IDummyComponent @out = entity.GetComponent<IDummyComponent>();


    }
}
