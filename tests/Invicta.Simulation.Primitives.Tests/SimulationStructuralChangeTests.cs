// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Invicta.Simulation.Primitives;

/// <summary>
/// Covers when creations and destructions take effect: at once between steps, and at the end of the step during one.
/// </summary>
internal sealed class SimulationStructuralChangeTests
{
    private static readonly TimeSpan s_step = TimeSpan.FromSeconds(1);

    [Test]
    public void AddComponent_BetweenSteps_IsFoundAtOnce()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();

        FuelTank tank = entity.AddComponent(new FuelTank());

        Assert.That(entity.GetComponent<FuelTank>(), Is.SameAs(tank));
    }

    [Test]
    public void AddComponent_DuringAStep_IsFoundOnlyAfterTheStep()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent driver = entity.AddComponent(new TrackedComponent());

        bool foundDuringStep = false;
        driver.UpdateAction = self =>
        {
            self.UpdateAction = null;
            entity.AddComponent(new FuelTank());
            foundDuringStep = entity.TryGetComponent(out FuelTank? _);
        };

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(foundDuringStep, Is.False);
            Assert.That(entity.TryGetComponent(out FuelTank? _), Is.True);
        }
    }

    [Test]
    public void AddComponent_DuringAStep_StartsInTheNextStepAndUpdatesFromThen()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent driver = entity.AddComponent(new TrackedComponent());
        TrackedComponent late = new();

        driver.UpdateAction = self =>
        {
            self.UpdateAction = null;
            entity.AddComponent(late);
        };

        simulation.Step(s_step);
        int startsAfterFirstStep = late.StartCount;
        int updatesAfterFirstStep = late.UpdateCount;

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(startsAfterFirstStep, Is.Zero);
            Assert.That(updatesAfterFirstStep, Is.Zero);
            Assert.That(late.StartCount, Is.EqualTo(1));
            Assert.That(late.UpdateCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void CreateEntity_DuringAStep_StartsItsComponentsInTheNextStep()
    {
        using Simulation simulation = new();
        TrackedComponent driver = simulation.CreateEntity().AddComponent(new TrackedComponent());
        TrackedComponent late = new();

        driver.UpdateAction = self =>
        {
            self.UpdateAction = null;
            simulation.CreateEntity().AddComponent(late);
        };

        simulation.Step(s_step);
        int startsAfterFirstStep = late.StartCount;

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(startsAfterFirstStep, Is.Zero);
            Assert.That(late.StartCount, Is.EqualTo(1));
            Assert.That(simulation.Entities, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void Destroy_ComponentDuringAStep_StillUpdatesAndIsStillFoundUntilTheEndOfTheStep()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent driver = entity.AddComponent(new TrackedComponent());
        TrackedComponent victim = entity.AddComponent(new TrackedComponent());
        simulation.Step(s_step);

        bool foundAfterDestroy = false;
        bool destroyedFlagDuringStep = true;
        driver.UpdateAction = _ =>
        {
            victim.Destroy();
            foundAfterDestroy = entity.GetComponents<TrackedComponent>().Contains(victim);
            destroyedFlagDuringStep = victim.IsDestroyed;
        };

        simulation.Step(s_step);
        int updatesAfterDestruction = victim.UpdateCount;

        driver.UpdateAction = null;
        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(foundAfterDestroy, Is.True);
            Assert.That(destroyedFlagDuringStep, Is.False);
            Assert.That(updatesAfterDestruction, Is.EqualTo(2), "it updates in the step that destroyed it");
            Assert.That(victim.UpdateCount, Is.EqualTo(2), "and not in any step after it");
            Assert.That(victim.IsDestroyed, Is.True);
            Assert.That(victim.DestroyedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Destroy_ComponentAddedInTheSameStep_NeverStartsOrUpdatesAndIsStillShutDown()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent driver = entity.AddComponent(new TrackedComponent());
        TrackedComponent added = new();

        driver.UpdateAction = self =>
        {
            self.UpdateAction = null;
            entity.AddComponent(added);
            added.Destroy();
        };

        simulation.Step(s_step);
        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added.StartCount, Is.Zero);
            Assert.That(added.UpdateCount, Is.Zero);
            Assert.That(added.DestroyedCount, Is.EqualTo(1));
            TrackedComponent[] expected = [driver];
            Assert.That(entity.GetComponents<TrackedComponent>(), Is.EqualTo(expected));
        }
    }

    [Test]
    public void Destroy_EntityCreatedInTheSameStep_NeverJoinsTheSimulationAndIsStillShutDown()
    {
        using Simulation simulation = new();
        TrackedComponent driver = simulation.CreateEntity().AddComponent(new TrackedComponent());
        TrackedComponent added = new();

        driver.UpdateAction = self =>
        {
            self.UpdateAction = null;

            Entity created = simulation.CreateEntity();
            created.AddComponent(added);
            created.Destroy();
        };

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(simulation.Entities, Has.Count.EqualTo(1));
            Assert.That(added.StartCount, Is.Zero);
            Assert.That(added.DestroyedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Destroy_SeveralEntitiesInOneStep_ShutsThemDownInCreationAndAdditionOrder()
    {
        List<string> log = [];
        using Simulation simulation = new();
        TrackedComponent driver = simulation.CreateEntity().AddComponent(new TrackedComponent("driver", log));

        Entity first = simulation.CreateEntity();
        first.AddComponent(new TrackedComponent("first.a", log));
        first.AddComponent(new TrackedComponent("first.b", log));

        Entity second = simulation.CreateEntity();
        second.AddComponent(new TrackedComponent("second.a", log));

        simulation.Step(s_step);
        log.Clear();

        driver.UpdateAction = _ =>
        {
            second.Destroy();
            first.Destroy();
        };

        simulation.Step(s_step);

        string[] expected = ["first.a.destroyed", "first.b.destroyed", "second.a.destroyed"];

        Assert.That(
            log.Where(static entry => entry.EndsWith(".destroyed", StringComparison.Ordinal)),
            Is.EqualTo(expected));
    }

    [Test]
    public void OnDestroyed_DestroyingAnotherComponent_ShutsItDownInTheNextPass()
    {
        List<string> log = [];
        using Simulation simulation = new();
        TrackedComponent early = simulation.CreateEntity().AddComponent(new TrackedComponent("early", log));
        TrackedComponent late = simulation.CreateEntity().AddComponent(new TrackedComponent("late", log));
        simulation.Step(s_step);
        log.Clear();

        late.DestroyedAction = _ => early.Destroy();
        late.Destroy();

        string[] expected = ["late.destroyed", "early.destroyed"];

        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void OnDestroyed_CascadeOfDestructions_FinishesInTheStepThatStartedIt()
    {
        List<string> log = [];
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent driver = entity.AddComponent(new TrackedComponent("driver", log));
        TrackedComponent first = entity.AddComponent(new TrackedComponent("first", log));
        TrackedComponent second = entity.AddComponent(new TrackedComponent("second", log));
        TrackedComponent third = entity.AddComponent(new TrackedComponent("third", log));
        simulation.Step(s_step);
        log.Clear();

        first.DestroyedAction = _ => second.Destroy();
        second.DestroyedAction = _ => third.Destroy();
        driver.UpdateAction = _ => first.Destroy();

        simulation.Step(s_step);

        string[] expected = ["first.destroyed", "second.destroyed", "third.destroyed"];

        Assert.That(
            log.Where(static entry => entry.EndsWith(".destroyed", StringComparison.Ordinal)),
            Is.EqualTo(expected));
    }

    [Test]
    public void OnDestroyed_ComponentAddedDuringTheStep_IsNotVisibleToTheHook()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent driver = entity.AddComponent(new TrackedComponent());
        TrackedComponent victim = entity.AddComponent(new TrackedComponent());

        bool foundDuringShutdown = true;
        victim.DestroyedAction = _ => foundDuringShutdown = entity.TryGetComponent(out FuelTank? _);
        driver.UpdateAction = self =>
        {
            self.UpdateAction = null;
            entity.AddComponent(new FuelTank());
            victim.Destroy();
        };

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(foundDuringShutdown, Is.False);
            Assert.That(entity.TryGetComponent(out FuelTank? _), Is.True);
        }
    }

    [Test]
    public void OnDestroyed_CreatingAnEntity_JoinsAfterThePassesAndStartsInTheNextStep()
    {
        using Simulation simulation = new();
        TrackedComponent victim = simulation.CreateEntity().AddComponent(new TrackedComponent());
        TrackedComponent late = new();

        victim.DestroyedAction = _ => simulation.CreateEntity().AddComponent(late);

        victim.Destroy();
        int startsAfterShutdown = late.StartCount;

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(startsAfterShutdown, Is.Zero);
            Assert.That(simulation.Entities, Has.Count.EqualTo(2));
            Assert.That(late.StartCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Destroy_ReplacingAComponentDuringAStep_LeavesExactlyOneMatch()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        FuelTank oldTank = entity.AddComponent(new FuelTank());
        TrackedComponent driver = entity.AddComponent(new TrackedComponent());
        FuelTank newTank = new();

        FuelTank? matchDuringStep = null;
        driver.UpdateAction = self =>
        {
            self.UpdateAction = null;
            oldTank.Destroy();
            entity.AddComponent(newTank);
            matchDuringStep = entity.GetComponent<FuelTank>();
        };

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(matchDuringStep, Is.SameAs(oldTank));
            Assert.That(entity.GetComponent<FuelTank>(), Is.SameAs(newTank));
        }
    }

    [Test]
    public void Destroy_EntityDuringAStep_ItsComponentsGoOnUpdatingUntilTheEndOfTheStep()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent driver = entity.AddComponent(new TrackedComponent());
        TrackedComponent other = entity.AddComponent(new TrackedComponent());
        simulation.Step(s_step);

        driver.UpdateAction = _ => entity.Destroy();
        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(other.UpdateCount, Is.EqualTo(2));
            Assert.That(other.DestroyedCount, Is.EqualTo(1));
            Assert.That(simulation.Entities, Is.Empty);
        }
    }
}
