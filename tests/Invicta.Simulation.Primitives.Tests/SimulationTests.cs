// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using NUnit.Framework;

namespace Invicta.Simulation.Primitives;

internal sealed class SimulationTests
{
    private static readonly TimeSpan s_step = TimeSpan.FromSeconds(1);

    [Test]
    public void ElapsedTime_NewSimulation_IsZero()
    {
        using Simulation simulation = new();

        Assert.That(simulation.ElapsedTime, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Entities_NewSimulation_IsEmpty()
    {
        using Simulation simulation = new();

        Assert.That(simulation.Entities, Is.Empty);
    }

    [Test]
    public void ElapsedTime_AfterSteps_IsTheSumOfTheirLengths()
    {
        using Simulation simulation = new();

        simulation.Step(TimeSpan.FromSeconds(1));
        simulation.Step(TimeSpan.FromSeconds(2));
        simulation.Step(TimeSpan.FromSeconds(4));

        Assert.That(simulation.ElapsedTime, Is.EqualTo(TimeSpan.FromSeconds(7)));
    }

    [Test]
    public void ElapsedTime_DuringAStep_IsTheTimeAtTheStartOfTheStep()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());

        List<TimeSpan> readings = [];
        component.UpdateAction = _ => readings.Add(simulation.ElapsedTime);

        simulation.Step(s_step);
        simulation.Step(s_step);

        TimeSpan[] expected = [TimeSpan.Zero, s_step];

        Assert.That(readings, Is.EqualTo(expected));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-3600)]
    public void Step_LengthIsNotPositive_ThrowsArgumentOutOfRangeException(int seconds)
    {
        using Simulation simulation = new();

        Assert.That(
            () => simulation.Step(TimeSpan.FromSeconds(seconds)),
            Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("deltaTime"));
    }

    [Test]
    public void Step_AfterDispose_ThrowsObjectDisposedException()
    {
        Simulation simulation = new();
        simulation.Dispose();

        Assert.That(() => simulation.Step(s_step), Throws.TypeOf<ObjectDisposedException>());
    }

    [Test]
    public void CreateEntity_AfterDispose_ThrowsObjectDisposedException()
    {
        Simulation simulation = new();
        simulation.Dispose();

        Assert.That(simulation.CreateEntity, Throws.TypeOf<ObjectDisposedException>());
    }

    [Test]
    public void Step_CalledFromInsideAHook_ThrowsInvalidOperationException()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());
        component.UpdateAction = _ => simulation.Step(s_step);

        Assert.That(() => simulation.Step(s_step), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Step_CalledFromInsideAShutdownHookBetweenSteps_ThrowsInvalidOperationException()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());
        component.DestroyedAction = _ => simulation.Step(s_step);

        Assert.That(component.Destroy, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Step_AfterAHookThrew_ThrowsInvalidOperationException()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());
        component.UpdateAction = static _ => throw new NotSupportedException("the model failed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => simulation.Step(s_step), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => simulation.Step(s_step), Throws.InstanceOf<InvalidOperationException>());
        }
    }

    [Test]
    public void Step_AfterAShutdownHookThrewBetweenSteps_ThrowsInvalidOperationException()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());
        component.DestroyedAction = static _ => throw new NotSupportedException("the model failed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(component.Destroy, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => simulation.Step(s_step), Throws.InstanceOf<InvalidOperationException>());
        }
    }

    [Test]
    public void CreateEntity_ReturnsAnEntityThatBelongsToTheSimulation()
    {
        using Simulation simulation = new();

        Entity entity = simulation.CreateEntity();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.Simulation, Is.SameAs(simulation));
            Assert.That(entity.IsDestroyed, Is.False);
            Entity[] expected = [entity];
            Assert.That(simulation.Entities, Is.EqualTo(expected));
        }
    }

    [Test]
    public void Entities_EveryAccess_ReturnsTheSameCollection()
    {
        using Simulation simulation = new();

        IReadOnlyCollection<Entity> entities = simulation.Entities;
        simulation.CreateEntity();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(simulation.Entities, Is.SameAs(entities));
            Assert.That(entities, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Entities_SeveralEntities_AreInCreationOrder()
    {
        using Simulation simulation = new();

        Entity first = simulation.CreateEntity();
        Entity second = simulation.CreateEntity();
        Entity third = simulation.CreateEntity();

        Entity[] expected = [first, second, third];

        Assert.That(simulation.Entities, Is.EqualTo(expected));
    }

    [Test]
    public void Entities_EntityCreatedDuringAStep_ContainsItOnlyAfterTheStep()
    {
        using Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());

        int countDuringStep = 0;
        component.UpdateAction = self =>
        {
            self.UpdateAction = null;
            simulation.CreateEntity();
            countDuringStep = simulation.Entities.Count;
        };

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(countDuringStep, Is.EqualTo(1));
            Assert.That(simulation.Entities, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void Entities_EntityDestroyedDuringAStep_ContainsItUntilTheEndOfTheStep()
    {
        using Simulation simulation = new();
        Entity entity = simulation.CreateEntity();
        TrackedComponent component = entity.AddComponent(new TrackedComponent());

        int countDuringStep = 0;
        component.UpdateAction = _ =>
        {
            entity.Destroy();
            countDuringStep = simulation.Entities.Count;
        };

        simulation.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(countDuringStep, Is.EqualTo(1));
            Assert.That(simulation.Entities, Is.Empty);
        }
    }

    [Test]
    public void Entities_EntityCreatedWhileEnumerating_IsNotSeenAndDoesNotThrow()
    {
        using Simulation simulation = new();
        simulation.CreateEntity();

        List<Entity> enumerated = [];
        foreach (Entity entity in simulation.Entities)
        {
            enumerated.Add(entity);
            simulation.CreateEntity();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(enumerated, Has.Count.EqualTo(1));
            Assert.That(simulation.Entities, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void Entities_EntitiesDestroyedWhileEnumerating_AreStillSeenAndDoNotThrow()
    {
        using Simulation simulation = new();
        simulation.CreateEntity();
        simulation.CreateEntity();

        List<Entity> enumerated = [];
        foreach (Entity entity in simulation.Entities)
        {
            enumerated.Add(entity);
            entity.Destroy();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(enumerated, Has.Count.EqualTo(2));
            Assert.That(simulation.Entities, Is.Empty);
        }
    }

    [Test]
    public void Dispose_ShutsDownEveryComponentWhetherOrNotItStarted()
    {
        TrackedComponent started;
        TrackedComponent neverStarted;

        using (Simulation simulation = new())
        {
            Entity entity = simulation.CreateEntity();
            started = entity.AddComponent(new TrackedComponent());
            simulation.Step(s_step);
            neverStarted = entity.AddComponent(new TrackedComponent());
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started.DestroyedCount, Is.EqualTo(1));
            Assert.That(started.IsDestroyed, Is.True);
            Assert.That(neverStarted.StartCount, Is.Zero);
            Assert.That(neverStarted.DestroyedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Dispose_ShutsComponentsDownInCreationAndAdditionOrder()
    {
        List<string> log = [];

        using (Simulation simulation = new())
        {
            Entity first = simulation.CreateEntity();
            first.AddComponent(new TrackedComponent("first.a", log));
            first.AddComponent(new TrackedComponent("first.b", log));

            Entity second = simulation.CreateEntity();
            second.AddComponent(new TrackedComponent("second.a", log));

            simulation.Step(s_step);
            log.Clear();
        }

        string[] expected = ["first.a.destroyed", "first.b.destroyed", "second.a.destroyed"];

        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void Dispose_CalledTwice_ShutsEachComponentDownOnce()
    {
        Simulation simulation = new();
        TrackedComponent component = simulation.CreateEntity().AddComponent(new TrackedComponent());

        simulation.Dispose();
        simulation.Dispose();

        Assert.That(component.DestroyedCount, Is.EqualTo(1));
    }

    [Test]
    public void Dispose_ComponentAlreadyDestroyed_DoesNotShutItDownAgain()
    {
        TrackedComponent component;

        using (Simulation simulation = new())
        {
            component = simulation.CreateEntity().AddComponent(new TrackedComponent());
            component.Destroy();
        }

        Assert.That(component.DestroyedCount, Is.EqualTo(1));
    }

    [Test]
    public void Step_TwoSimulations_AreIndependent()
    {
        using Simulation first = new();
        using Simulation second = new();

        TrackedComponent inFirst = first.CreateEntity().AddComponent(new TrackedComponent());
        TrackedComponent inSecond = second.CreateEntity().AddComponent(new TrackedComponent());

        first.Step(s_step);
        first.Step(s_step);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inFirst.UpdateCount, Is.EqualTo(2));
            Assert.That(inSecond.UpdateCount, Is.Zero);
            Assert.That(second.ElapsedTime, Is.EqualTo(TimeSpan.Zero));
        }
    }

    [Test]
    public void Step_TheSameModelBuiltTwice_ProducesTheSameRun()
    {
        List<string> firstRun = RunScenario();
        List<string> secondRun = RunScenario();

        Assert.That(secondRun, Is.EqualTo(firstRun));
    }

    [Test]
    public void Step_ManySimulationsRunInParallel_EachProducesTheSameRun()
    {
        List<string> expected = RunScenario();

        ConcurrentBag<List<string>> runs = [];
        Parallel.For(0, 8, _ => runs.Add(RunScenario()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(runs, Has.Count.EqualTo(8));
            Assert.That(runs, Is.All.EqualTo(expected));
        }
    }

    /// <summary>Runs a model that starts, updates, creates and destroys, and returns the hooks it ran.</summary>
    private static List<string> RunScenario()
    {
        List<string> log = [];

        using Simulation simulation = new();

        Entity depot = simulation.CreateEntity();
        depot.AddComponent(new TrackedComponent("depot", log));

        Entity car = simulation.CreateEntity();
        car.AddComponent(new TrackedComponent("tank", log));

        TrackedComponent engine = car.AddComponent(new TrackedComponent("engine", log));
        engine.UpdateAction = self =>
        {
            if (simulation.ElapsedTime >= TimeSpan.FromSeconds(2))
            {
                self.Entity.Destroy();
            }
            else
            {
                simulation.CreateEntity().AddComponent(new TrackedComponent("spare", log));
            }
        };

        for (int step = 0; step < 4; step++)
        {
            simulation.Step(s_step);
        }

        return log;
    }
}
