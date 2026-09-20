// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Invicta.Simulation.Primitives;

/// <summary>
/// The root of one run: it owns the entities and the clock, and advances them a step at a time.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Step(TimeSpan)"/> runs exactly one step, of the length the caller gives, so running until a condition
/// holds or pacing a run against the wall clock is a loop the application writes around it. A model with a fixed
/// step passes the same length every time, which keeps runs reproducible and numerical methods stable.
/// </para>
/// <para>
/// Simulations share nothing, so any number of them can run at the same time, each on its own thread. One
/// simulation is not thread-safe, which matches the sequential model of the step: no two threads may use it at
/// once, although successive calls can come from different threads. The library has no static state, and holds no
/// random number generator, so a model that needs randomness creates one per simulation and passes it to the
/// components that draw from it.
/// </para>
/// </remarks>
[SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "No type lives in the Invicta.Simulation namespace, which is the prefix every project in the "
        + "workspace shares, so the name cannot be confused with one.")]
public sealed class Simulation : IDisposable
{
    private readonly SnapshotCollection<Entity> _entities = [];
    private readonly List<Entity> _entitiesToJoin = [];
    private readonly List<Entity> _entitiesToShutDown = [];
    private readonly List<Entity> _entitiesWithPendingComponents = [];

    private long _nextCreationOrder;

    private bool _isStepping;
    private bool _isApplyingChanges;
    private bool _isFaulted;
    private bool _isDisposed;

    /// <summary>Gets the simulated time elapsed since the start of the run.</summary>
    /// <remarks>
    /// The clock advances by the step's length at the end of <see cref="Step(TimeSpan)"/>, so throughout a step this
    /// equals the <see cref="SimulationTime.Elapsed"/> the step's components receive.
    /// </remarks>
    public TimeSpan ElapsedTime { get; private set; }

    /// <summary>Gets the simulation's entities, in creation order.</summary>
    /// <remarks>
    /// <para>
    /// The property returns the same collection on every access, and its <see cref="IReadOnlyCollection{T}.Count"/>
    /// and every new enumeration show the entities the simulation holds at the moment of the call. During a step
    /// that is not the same as the entities that have been asked for: a creation or a destruction requested during a
    /// step is applied at the end of it, so throughout a step the collection lists the entities as they were when
    /// the step began.
    /// </para>
    /// <para>
    /// Each enumeration sees the entities as they were when it began, so entities can be created and destroyed
    /// inside a <see langword="foreach"/> over the collection.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<Entity> Entities => _entities;

    /// <summary>
    /// Gets a value indicating whether structural changes wait to be applied, which they do while a step is running
    /// and while changes are being applied.
    /// </summary>
    internal bool AreChangesDeferred => _isStepping || _isApplyingChanges;

    /// <summary>Creates an entity in the simulation.</summary>
    /// <returns>The new entity, which holds no components.</returns>
    /// <exception cref="ObjectDisposedException">The simulation has been disposed.</exception>
    /// <remarks>
    /// An entity created during a step joins the simulation at the end of the step; one created between steps joins
    /// at once. Until it joins, <see cref="Entities"/> leaves it out. Either way its components can be added
    /// straight away.
    /// </remarks>
    public Entity CreateEntity()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        Entity entity = new(this, _nextCreationOrder);
        _nextCreationOrder++;

        if (AreChangesDeferred)
        {
            _entitiesToJoin.Add(entity);
        }
        else
        {
            _entities.Add(entity);
        }

        return entity;
    }

    /// <summary>Advances the simulation by one step.</summary>
    /// <param name="deltaTime">The length of the step, which must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaTime"/> is zero or negative.</exception>
    /// <exception cref="ObjectDisposedException">The simulation has been disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// The call came from inside a component's hook, or an exception from a hook has left the simulation part way
    /// through a step.
    /// </exception>
    /// <remarks>
    /// A step starts the components that joined since the last step, updates every component, applies the structural
    /// changes the step requested, and then advances the clock. Entities update in creation order and each entity's
    /// components in the order they were added, so a run repeats when it is built and stepped the same way.
    /// </remarks>
    public void Step(TimeSpan deltaTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deltaTime, TimeSpan.Zero);

        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (AreChangesDeferred)
        {
            throw new InvalidOperationException(
                "The simulation is running a step, or applying the changes one asked for, so it cannot step from "
                + "inside a component's hook.");
        }

        if (_isFaulted)
        {
            throw new InvalidOperationException(
                "An exception from a component's hook left the simulation part way through a step, so it cannot "
                + "step again.");
        }

        SimulationTime time = new(ElapsedTime, deltaTime);

        _isStepping = true;
        try
        {
            StartAddedComponents(time);
            UpdateComponents(time);
            ApplyPendingChanges();
        }
        catch
        {
            // Components have advanced by different amounts, so the run cannot carry on from here.
            _isFaulted = true;

            throw;
        }
        finally
        {
            _isStepping = false;
        }

        ElapsedTime += deltaTime;
    }

    /// <summary>Starts the components that joined the simulation since the last step.</summary>
    /// <param name="time">The time of the step.</param>
    private void StartAddedComponents(SimulationTime time)
    {
        foreach (Entity entity in _entities)
        {
            entity.StartAddedComponents(time);
        }
    }

    /// <summary>Advances every component by one step, entity by entity in creation order.</summary>
    /// <param name="time">The time of the step.</param>
    private void UpdateComponents(SimulationTime time)
    {
        foreach (Entity entity in _entities)
        {
            entity.UpdateComponents(time);
        }
    }

    /// <summary>Records that an entity has something to shut down.</summary>
    /// <param name="entity">The entity that is being destroyed, or that holds a component that is.</param>
    /// <remarks>
    /// Between steps there is no step to wait for, so the shutdown runs at once; a request made while a step is
    /// running, or while changes are being applied, waits for the passes that follow.
    /// </remarks>
    internal void RegisterEntityWithPendingShutdown(Entity entity)
    {
        _entitiesToShutDown.Add(entity);

        if (!_isStepping)
        {
            ApplyPendingChanges();
        }
    }

    /// <summary>Records that an entity has components waiting to join it.</summary>
    /// <param name="entity">The entity the components were added to.</param>
    internal void RegisterEntityWithPendingComponents(Entity entity)
    {
        _entitiesWithPendingComponents.Add(entity);
    }

    /// <summary>
    /// Shuts down everything that has been destroyed, in passes, and then joins everything that has been added.
    /// </summary>
    private void ApplyPendingChanges()
    {
        // A request made while the passes are running is picked up by the pass loop that is already running.
        if (_isApplyingChanges)
        {
            return;
        }

        _isApplyingChanges = true;
        try
        {
            while (_entitiesToShutDown.Count > 0)
            {
                ShutDownDestroyedObjects();
            }

            JoinAddedObjects();
        }
        catch
        {
            // Part of the shutdown has run and part has not, so the run cannot carry on from here.
            _isFaulted = true;

            throw;
        }
        finally
        {
            _isApplyingChanges = false;
        }
    }

    /// <summary>
    /// Runs one shutdown pass, which shuts down everything that was pending when the pass began, in creation and
    /// addition order. Anything a hook destroys is left for the next pass.
    /// </summary>
    private void ShutDownDestroyedObjects()
    {
        List<Entity> entities = [.. _entitiesToShutDown];
        _entitiesToShutDown.Clear();

        entities.Sort(static (first, second) => first.CreationOrder.CompareTo(second.CreationOrder));

        List<Component> components = [];
        foreach (Entity entity in entities)
        {
            entity.CollectComponentsToShutDown(components);
        }

        foreach (Component component in components)
        {
            component.InvokeDestroyed();
        }

        foreach (Entity entity in entities)
        {
            entity.RemoveDestroyedComponents();
        }

        _entities.RemoveAll(static entity => entity.IsDestroyed);
        _entitiesToJoin.RemoveAll(static entity => entity.IsDestroyed);
    }

    /// <summary>Joins the entities and components added since the changes were last applied.</summary>
    private void JoinAddedObjects()
    {
        foreach (Entity entity in _entitiesWithPendingComponents)
        {
            entity.JoinAddedComponents();
        }

        _entitiesWithPendingComponents.Clear();

        _entities.AddRange(_entitiesToJoin);
        _entitiesToJoin.Clear();
    }

    /// <summary>Shuts down every component in the simulation, whether or not it started.</summary>
    /// <remarks>
    /// Each component receives <see cref="Component.OnDestroyed"/> exactly once, in the same passes and the same
    /// order as any other destruction, so a component holding a resource releases it. A disposed simulation cannot
    /// step or create entities.
    /// </remarks>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Deferring the destructions puts every entity into one pass, rather than one pass for each of them.
        _isApplyingChanges = true;
        try
        {
            DestroyAllEntities();
        }
        finally
        {
            _isApplyingChanges = false;
        }

        ApplyPendingChanges();
    }

    /// <summary>Destroys every entity the simulation holds, including those waiting to join it.</summary>
    private void DestroyAllEntities()
    {
        foreach (Entity entity in _entities)
        {
            entity.Destroy();
        }

        foreach (Entity entity in _entitiesToJoin)
        {
            entity.Destroy();
        }
    }
}
