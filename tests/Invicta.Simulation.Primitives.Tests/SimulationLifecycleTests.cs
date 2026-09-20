// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Invicta.Simulation.Primitives;

/// <summary>Covers the hooks each component receives, when they run, and in what order.</summary>
internal sealed class SimulationLifecycleTests
{
    private static readonly TimeSpan s_step = TimeSpan.FromSeconds(1);

    [Test]
    public void OnStart_FirstStep_RunsOnceBeforeTheFirstUpdate()
    {
        List<string> log = [];
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent("engine", log));

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.StartCount, Is.EqualTo(1));
            string[] expected = ["engine.start", "engine.update"];
            Assert.That(log, Is.EqualTo(expected));
        }
    }

    [Test]
    public void OnStart_ManySteps_RunsOnlyOnce()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());

        simulation.Step(s_step);
        simulation.Step(s_step);
        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.StartCount, Is.EqualTo(1));
            Assert.That(component.UpdateCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void OnStart_ComponentAddedBetweenSteps_RunsBeforeEveryUpdateOfTheNextStep()
    {
        List<string> log = [];
        using Simulation simulation = new();
        simulation.CreateEntity().AddComponent(new TrackedComponent("first", log));

        simulation.Step(s_step);
        log.Clear();

        simulation.CreateEntity().AddComponent(new TrackedComponent("second", log));
        simulation.Step(s_step);

        string[] expected = ["second.start", "first.update", "second.update"];

        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void OnStart_SeveralEntities_RunsInCreationAndAdditionOrder()
    {
        List<string> log = [];
        using Simulation simulation = new();
        Entity first = simulation.CreateEntity();
        Entity second = simulation.CreateEntity();

        second.AddComponent(new TrackedComponent("second.a", log));
        first.AddComponent(new TrackedComponent("first.a", log));
        first.AddComponent(new TrackedComponent("first.b", log));

        simulation.Step(s_step);

        string[] expected = ["first.a.start", "first.b.start", "second.a.start"];

        Assert.That(
            log.Where(static entry => entry.EndsWith(".start", StringComparison.Ordinal)),
            Is.EqualTo(expected));
    }

    [Test]
    public void OnStart_ComponentDestroyedByAnEarlierStart_StillStartsAndUpdatesInThatStep()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent first = entity.AddComponent(new TrackedComponent());
        TrackedComponent second = entity.AddComponent(new TrackedComponent());
        first.StartAction = _ => second.Destroy();

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.StartCount, Is.EqualTo(1), "the destruction only applies at the end of the step");
            Assert.That(second.UpdateCount, Is.EqualTo(1));
            Assert.That(second.DestroyedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void OnStart_SiblingsAddedWithIt_CanBeLookedUp()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent engine = entity.AddComponent(new TrackedComponent());

        FuelTank? sibling = null;
        engine.StartAction = self => sibling = self.Entity.GetComponent<FuelTank>();

        FuelTank tank = entity.AddComponent(new FuelTank());
        simulation.Step(s_step);

        Assert.That(sibling, Is.SameAs(tank));
    }

    [Test]
    public void OnStart_ComponentStartingInALaterStep_ReceivesThatStepsTime()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        simulation.Step(TimeSpan.FromSeconds(1));
        TrackedComponent late = entity.AddComponent(new TrackedComponent());
        simulation.Step(TimeSpan.FromSeconds(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                late.StartTime,
                Is.EqualTo(new SimulationTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))));
            Assert.That(late.UpdateTime, Is.EqualTo(late.StartTime));
        }
    }

    [Test]
    public void OnUpdate_EachStep_ReceivesTheElapsedTimeAndTheStepsLength()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());

        List<SimulationTime> times = [];
        component.UpdateAction = self => times.Add(self.UpdateTime);

        simulation.Step(TimeSpan.FromSeconds(1));
        simulation.Step(TimeSpan.FromSeconds(3));

        Assert.That(times, Is.EqualTo(
        [
            new SimulationTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            new SimulationTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)),
        ]));
    }

    [Test]
    public void OnUpdate_SeveralEntities_RunsInCreationAndAdditionOrder()
    {
        List<string> log = [];
        using Simulation simulation = new();
        Entity first = simulation.CreateEntity();
        first.AddComponent(new TrackedComponent("first.a", log));
        first.AddComponent(new TrackedComponent("first.b", log));

        Entity second = simulation.CreateEntity();
        second.AddComponent(new TrackedComponent("second.a", log));

        simulation.Step(s_step);
        log.Clear();
        simulation.Step(s_step);

        string[] expected = ["first.a.update", "first.b.update", "second.a.update"];

        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void OnUpdate_ComponentWithNoOverriddenHooks_IsUpdatedWithoutThrowing()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        BareComponent component = entity.AddComponent(new BareComponent());

        simulation.Step(s_step);
        component.Destroy();

        Assert.That(component.IsDestroyed, Is.True);
    }

    [Test]
    public void OnDestroyed_ComponentDestroyedBetweenSteps_RunsAtOnce()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());
        simulation.Step(s_step);

        component.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.DestroyedCount, Is.EqualTo(1));
            Assert.That(component.IsDestroyed, Is.True);
            Assert.That(component.WasDestroyedWhenShutDown, Is.True);
        }
    }

    [Test]
    public void OnDestroyed_ComponentNeverStarted_StillRuns()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());

        component.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.StartCount, Is.Zero);
            Assert.That(component.DestroyedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void OnDestroyed_ComponentWhoseStartThrew_RunsWhenTheSimulationIsDisposed()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());
        component.StartAction = static _ => throw new NotSupportedException("the model failed");

        Assert.That(() => simulation.Step(s_step), Throws.TypeOf<NotSupportedException>());
        simulation.Dispose();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.StartCount, Is.EqualTo(1));
            Assert.That(component.UpdateCount, Is.Zero);
            Assert.That(component.DestroyedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void OnDestroyed_EntityDestroyed_RunsOnceForEachOfItsComponents()
    {
        List<string> log = [];
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent first = entity.AddComponent(new TrackedComponent("first", log));
        TrackedComponent second = entity.AddComponent(new TrackedComponent("second", log));
        simulation.Step(s_step);
        log.Clear();

        entity.Destroy();

        using (Assert.EnterMultipleScope())
        {
            string[] expected = ["first.destroyed", "second.destroyed"];
            Assert.That(log, Is.EqualTo(expected));
            Assert.That(first.IsDestroyed, Is.True);
            Assert.That(second.IsDestroyed, Is.True);
            Assert.That(entity.IsDestroyed, Is.True);
        }
    }

    [Test]
    public void OnDestroyed_ComponentDestroyedTwice_RunsOnce()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());

        component.Destroy();
        component.Destroy();

        Assert.That(component.DestroyedCount, Is.EqualTo(1));
    }

    [Test]
    public void OnDestroyed_ComponentAndItsEntityDestroyedInOneStep_RunsOnce()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent component = entity.AddComponent(new TrackedComponent());
        simulation.Step(s_step);

        component.UpdateAction = self =>
        {
            self.Destroy();
            entity.Destroy();
        };

        simulation.Step(s_step);

        Assert.That(component.DestroyedCount, Is.EqualTo(1));
    }

    [Test]
    public void OnDestroyed_ComponentOfADestroyedEntity_CanStillReachItsEntity()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent component = entity.AddComponent(new TrackedComponent());

        Entity? entityDuringShutdown = null;
        bool wasInEntities = false;
        component.DestroyedAction = self =>
        {
            entityDuringShutdown = self.Entity;
            wasInEntities = simulation.Entities.Contains(entity);
        };

        entity.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entityDuringShutdown, Is.SameAs(entity));
            Assert.That(wasInEntities, Is.True, "the entity leaves the simulation once its components have gone");
            Assert.That(simulation.Entities, Is.Empty);
        }
    }

    [Test]
    public void Destroy_EntityDestroyedTwice_DoesNothingTheSecondTime()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent component = entity.AddComponent(new TrackedComponent());

        entity.Destroy();
        entity.Destroy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.DestroyedCount, Is.EqualTo(1));
            Assert.That(simulation.Entities, Is.Empty);
        }
    }
}
