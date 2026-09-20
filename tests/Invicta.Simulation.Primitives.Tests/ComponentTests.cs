// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Invicta.Simulation.Primitives;

internal sealed class ComponentTests
{
    [Test]
    public void Entity_ComponentNotAddedToAnEntity_ThrowsInvalidOperationException()
    {
        FuelTank tank = new();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => tank.Entity, Throws.InstanceOf<InvalidOperationException>());
            Assert.That(tank.IsDestroyed, Is.False);
        }
    }

    [Test]
    public void Entity_DestroyedComponent_StillReturnsTheEntityItBelongedTo()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = entity.AddComponent(new FuelTank());

        tank.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tank.Entity, Is.SameAs(entity));
            Assert.That(tank.Entity.Simulation, Is.SameAs(simulation));
        }
    }

    [Test]
    public void Destroy_ComponentNeverAddedToAnEntity_ShutsItDownAtOnce()
    {
        TrackedComponent component = new();

        component.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.IsDestroyed, Is.True);
            Assert.That(component.DestroyedCount, Is.EqualTo(1));
            Assert.That(component.WasDestroyedWhenShutDown, Is.True);
        }
    }

    [Test]
    public void Destroy_ComponentNeverAddedToAnEntity_RunsTheHookWithoutAnEntity()
    {
        TrackedComponent component = new();
        bool hadEntity = true;
        component.DestroyedAction = self => hadEntity = TryReadEntity(self);

        component.Destroy();

        Assert.That(hadEntity, Is.False);
    }

    [Test]
    public void Destroy_ComponentNeverAddedToAnEntityTwice_RunsTheHookOnce()
    {
        TrackedComponent component = new();

        component.Destroy();
        component.Destroy();

        Assert.That(component.DestroyedCount, Is.EqualTo(1));
    }

    [Test]
    public void Destroy_ComponentNeverAddedToAnEntity_CannotBeAddedAfterwards()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent component = new();
        component.Destroy();

        Assert.That(() => entity.AddComponent(component), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Destroy_DestroyedComponent_CannotBeAddedToAnotherEntity()
    {
        using Simulation simulation = new();
        Entity first = simulation.CreateEntity();
        Entity second = simulation.CreateEntity();
        FuelTank tank = first.AddComponent(new FuelTank());
        tank.Destroy();

        Assert.That(() => second.AddComponent(tank), Throws.InstanceOf<InvalidOperationException>());
    }

    /// <summary>
    /// Reports whether a component has an entity, which reading the property is the only way to tell.
    /// </summary>
    private static bool TryReadEntity(Component component)
    {
        try
        {
            return component.Entity is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
