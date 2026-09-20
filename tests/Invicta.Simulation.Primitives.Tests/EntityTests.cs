// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Invicta.Simulation.Primitives;

internal sealed class EntityTests
{
    private static readonly TimeSpan s_step = TimeSpan.FromSeconds(1);

    [Test]
    public void AddComponent_ReturnsTheComponentAndSetsItsEntity()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = new();

        FuelTank added = entity.AddComponent(tank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.SameAs(tank));
            Assert.That(tank.Entity, Is.SameAs(entity));
            Assert.That(tank.IsDestroyed, Is.False);
        }
    }

    [Test]
    public void AddComponent_Null_ThrowsArgumentNullException()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        Assert.That(
            () => entity.AddComponent<FuelTank>(null!),
            Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("component"));
    }

    [Test]
    public void AddComponent_ComponentAlreadyOnThisEntity_ThrowsAndAddsNothing()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = entity.AddComponent(new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => entity.AddComponent(tank), Throws.InstanceOf<InvalidOperationException>());
            Assert.That(entity.GetComponents<Component>(), Is.EqualTo([tank]));
        }
    }

    [Test]
    public void AddComponent_ComponentAlreadyOnAnotherEntity_ThrowsAndLeavesBothEntitiesUnchanged()
    {
        using Simulation simulation = new();
        Entity first = simulation.CreateEntity();
        Entity second = simulation.CreateEntity();
        FuelTank tank = first.AddComponent(new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => second.AddComponent(tank), Throws.InstanceOf<InvalidOperationException>());
            Assert.That(second.GetComponents<Component>(), Is.Empty);
            Assert.That(tank.Entity, Is.SameAs(first));
        }
    }

    [Test]
    public void AddComponent_DestroyedComponent_Throws()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = new();
        tank.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => entity.AddComponent(tank), Throws.InstanceOf<InvalidOperationException>());
            Assert.That(entity.GetComponents<Component>(), Is.Empty);
        }
    }

    [Test]
    public void AddComponent_DestroyedEntity_Throws()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        entity.Destroy();

        Assert.That(() => entity.AddComponent(new FuelTank()), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void GetComponent_OneMatch_ReturnsIt()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        JetEngine engine = entity.AddComponent(new JetEngine());
        entity.AddComponent(new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.GetComponent<JetEngine>(), Is.SameAs(engine));
            Assert.That(entity.GetComponent<Propulsion>(), Is.SameAs(engine), "a base class matches");
            Assert.That(entity.GetComponent<IPropulsion>(), Is.SameAs(engine), "an interface matches");
        }
    }

    [Test]
    public void GetComponent_NoMatch_ThrowsInvalidOperationException()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        Assert.That(
            entity.GetComponent<FuelTank>,
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains(nameof(FuelTank)));
    }

    [Test]
    public void GetComponent_SeveralMatches_ThrowsNamingEveryMatch()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        entity.AddComponent(new JetEngine());
        entity.AddComponent(new Propeller());

        Assert.That(
            entity.GetComponent<Propulsion>,
            Throws.InstanceOf<InvalidOperationException>()
                .With.Message.Contains(nameof(JetEngine))
                .And.Message.Contains(nameof(Propeller)));
    }

    [Test]
    public void TryGetComponent_OneMatch_ReturnsTrueAndTheComponent()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = entity.AddComponent(new FuelTank());

        bool found = entity.TryGetComponent(out FuelTank? match);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(match, Is.SameAs(tank));
        }
    }

    [Test]
    public void TryGetComponent_NoMatch_ReturnsFalseAndNull()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        bool found = entity.TryGetComponent(out FuelTank? match);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.False);
            Assert.That(match, Is.Null);
        }
    }

    [Test]
    public void TryGetComponent_SeveralMatches_ThrowsRatherThanReportingNoMatch()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        entity.AddComponent(new JetEngine());
        entity.AddComponent(new Propeller());

        Assert.That(
            () => entity.TryGetComponent(out Propulsion? _),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void GetComponents_SeveralMatches_ReturnsThemInAdditionOrder()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        JetEngine jet = entity.AddComponent(new JetEngine());
        entity.AddComponent(new FuelTank());
        Propeller propeller = entity.AddComponent(new Propeller());

        Assert.That(entity.GetComponents<Propulsion>(), Is.EqualTo(new Propulsion[] { jet, propeller }));
    }

    [Test]
    public void GetComponents_NoMatch_IsEmpty()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        Assert.That(entity.GetComponents<Propulsion>(), Is.Empty);
    }

    [Test]
    public void GetComponents_OfComponent_ReturnsEveryUnkeyedComponent()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = entity.AddComponent(new FuelTank());
        JetEngine engine = entity.AddComponent(new JetEngine());
        entity.AddKeyedComponent("spare", new FuelTank());

        Assert.That(entity.GetComponents<Component>(), Is.EqualTo(new Component[] { tank, engine }));
    }

    [Test]
    public void GetComponents_ComponentAddedAfterTheCall_IsSeenWhenTheResultIsEnumerated()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        entity.AddComponent(new FuelTank());

        IEnumerable<FuelTank> tanks = entity.GetComponents<FuelTank>();
        entity.AddComponent(new FuelTank());

        Assert.That(tanks.Count(), Is.EqualTo(2));
    }

    [Test]
    public void GetComponents_ComponentsDestroyedWhileEnumerating_AreStillSeenAndDoNotThrow()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        entity.AddComponent(new TrackedComponent());
        entity.AddComponent(new TrackedComponent());

        List<TrackedComponent> enumerated = [];
        foreach (TrackedComponent component in entity.GetComponents<TrackedComponent>())
        {
            enumerated.Add(component);
            component.Destroy();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(enumerated, Has.Count.EqualTo(2));
            Assert.That(entity.GetComponents<TrackedComponent>(), Is.Empty);
        }
    }

    [Test]
    public void AddKeyedComponent_NullKey_AddsAnUnkeyedComponent()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        FuelTank tank = entity.AddKeyedComponent(key: null, new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.GetComponent<FuelTank>(), Is.SameAs(tank));
            Assert.That(entity.GetKeyedComponent<FuelTank>(key: null), Is.SameAs(tank));
            Assert.That(entity.TryGetKeyedComponent(key: null, out FuelTank? _), Is.True);
            Assert.That(entity.GetKeyedComponents<FuelTank>(key: null), Is.EqualTo([tank]));
        }
    }

    [Test]
    public void GetComponent_KeyedComponents_IgnoresThem()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        entity.AddKeyedComponent("left", new FuelTank());
        FuelTank unkeyed = entity.AddComponent(new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.GetComponent<FuelTank>(), Is.SameAs(unkeyed));
            Assert.That(entity.GetComponents<FuelTank>(), Is.EqualTo([unkeyed]));
        }
    }

    [Test]
    public void GetKeyedComponent_KeyEqualButNotTheSameInstance_FindsTheComponent()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = entity.AddKeyedComponent("left", new FuelTank());

        object equalKey = new string(['l', 'e', 'f', 't']);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(equalKey, Is.Not.SameAs("left"));
            Assert.That(entity.GetKeyedComponent<FuelTank>(equalKey), Is.SameAs(tank));
        }
    }

    [Test]
    public void GetKeyedComponent_AnotherKey_ThrowsAndTryGetReportsNoMatch()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        entity.AddKeyedComponent("left", new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                () => entity.GetKeyedComponent<FuelTank>("right"),
                Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("right"));
            Assert.That(entity.TryGetKeyedComponent("right", out FuelTank? _), Is.False);
            Assert.That(entity.TryGetComponent(out FuelTank? _), Is.False, "a keyed component is not unkeyed");
        }
    }

    [Test]
    public void GetKeyedComponents_SeveralComponentsUnderOneKey_ReturnsThemInAdditionOrder()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank first = entity.AddKeyedComponent("front", new FuelTank());
        FuelTank second = entity.AddKeyedComponent("front", new FuelTank());
        entity.AddKeyedComponent("rear", new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.GetKeyedComponents<FuelTank>("front"), Is.EqualTo([first, second]));
            Assert.That(
                () => entity.GetKeyedComponent<FuelTank>("front"),
                Throws.InstanceOf<InvalidOperationException>());
        }
    }

    [Test]
    public void GetKeyedComponent_SameKeyOnAnotherEntity_FindsOnlyItsOwnComponent()
    {
        using Simulation simulation = new();
        Entity first = simulation.CreateEntity();
        Entity second = simulation.CreateEntity();
        FuelTank inFirst = first.AddKeyedComponent("left", new FuelTank());
        FuelTank inSecond = second.AddKeyedComponent("left", new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.GetKeyedComponent<FuelTank>("left"), Is.SameAs(inFirst));
            Assert.That(second.GetKeyedComponent<FuelTank>("left"), Is.SameAs(inSecond));
        }
    }

    [Test]
    public void GetKeyedComponent_KeyOfAnotherType_IsComparedByEquals()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = entity.AddKeyedComponent(StringComparison.Ordinal, new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.GetKeyedComponent<FuelTank>(StringComparison.Ordinal), Is.SameAs(tank));
            Assert.That(entity.TryGetKeyedComponent(StringComparison.OrdinalIgnoreCase, out FuelTank? _), Is.False);
        }
    }

    [Test]
    public void Destroy_Entity_ShutsDownItsComponentsAndLeavesTheSimulation()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent component = entity.AddComponent(new TrackedComponent());
        simulation.Step(s_step);

        entity.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.IsDestroyed, Is.True);
            Assert.That(component.IsDestroyed, Is.True);
            Assert.That(component.DestroyedCount, Is.EqualTo(1));
            Assert.That(simulation.Entities, Is.Empty);
            Assert.That(entity.Simulation, Is.SameAs(simulation), "a destroyed entity still knows its simulation");
        }
    }

    [Test]
    public void Destroy_KeyedComponent_TakesItsKeyWithIt()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank tank = entity.AddKeyedComponent("left", new FuelTank());

        tank.Destroy();
        FuelTank replacement = entity.AddKeyedComponent("left", new FuelTank());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.GetKeyedComponent<FuelTank>("left"), Is.SameAs(replacement));
            Assert.That(entity.GetKeyedComponents<Component>("left"), Has.Exactly(1).Items);
        }
    }

    [Test]
    public void Destroy_ComponentOfADestroyedEntity_DoesNothing()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent component = entity.AddComponent(new TrackedComponent());
        entity.Destroy();

        component.Destroy();

        Assert.That(component.DestroyedCount, Is.EqualTo(1));
    }
}
