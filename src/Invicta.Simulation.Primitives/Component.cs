// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Invicta.Simulation.Primitives;

/// <summary>
/// One aspect of an entity: its state and the behaviour that changes it. Applications derive from this class and
/// override the lifecycle hooks they need.
/// </summary>
/// <remarks>
/// <para>
/// A component belongs to one entity for its whole life. It is added with <see cref="Entity.AddComponent{T}(T)"/> or
/// <see cref="Entity.AddKeyedComponent{T}(object, T)"/>, starts at the beginning of the next step, updates once per
/// step, and is shut down when it, its entity or its simulation is destroyed. It passes through those states once,
/// so a destroyed component cannot be added again.
/// </para>
/// <para>
/// Because <see cref="OnDestroyed"/> is the only clean-up the library promises to call, a component can acquire what
/// it needs in its constructor, keeping its fields <see langword="readonly"/>, and release it there.
/// </para>
/// </remarks>
public abstract class Component
{
    private Entity? _entity;

    private ComponentState _state;

    /// <summary>Gets the entity the component belongs to.</summary>
    /// <exception cref="InvalidOperationException">The component has not been added to an entity.</exception>
    /// <remarks>
    /// A destroyed component still reports the entity it belonged to. One that was destroyed before it was ever added
    /// to an entity has none, so an <see cref="OnDestroyed"/> override that can run that early must not read this
    /// property.
    /// </remarks>
    public Entity Entity
    {
        get
        {
            return _entity ?? throw new InvalidOperationException(
                "The component has not been added to an entity, so it has no entity to read.");
        }
    }

    /// <summary>Gets a value indicating whether the component has been destroyed.</summary>
    /// <remarks>
    /// The value changes when the destruction is applied rather than when it is requested: during a step that is at
    /// the end of the step, just before <see cref="OnDestroyed"/> runs, and between steps it is at once. It never
    /// goes back, because destruction is final.
    /// </remarks>
    public bool IsDestroyed => _state == ComponentState.Destroyed;

    /// <summary>Gets a value indicating whether the component's destruction has been requested.</summary>
    internal bool IsDestructionRequested { get; private set; }

    /// <summary>Gets a value indicating whether the component belongs to an entity but has not started.</summary>
    internal bool IsAwaitingStart => _state == ComponentState.Added;

    /// <summary>Destroys the component, shutting it down and taking it out of its entity.</summary>
    /// <remarks>
    /// <para>
    /// During a step the destruction is applied at the end of it: until then the component still updates, lookups
    /// still find it, and <see cref="IsDestroyed"/> is still <see langword="false"/>. Between steps it is applied at
    /// once. Either way <see cref="OnDestroyed"/> runs exactly once, and the component can never be added to an
    /// entity again.
    /// </para>
    /// <para>
    /// Destroying a component twice, or one whose entity is being destroyed, does nothing, because the entity's
    /// components are being shut down anyway. A component that was never added to an entity is shut down at once,
    /// since there is no step to wait for.
    /// </para>
    /// </remarks>
    public void Destroy()
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestructionRequested = true;

        if (_entity is null)
        {
            // Nothing owns the component, so it runs the hook itself rather than waiting for a step that never comes.
            InvokeDestroyed();

            return;
        }

        if (!_entity.IsDestructionRequested)
        {
            _entity.RegisterPendingShutdown();
        }
    }

    /// <summary>Sets the entity the component belongs to, which fixes its owner for the rest of its life.</summary>
    /// <param name="entity">The entity the component is being added to.</param>
    /// <exception cref="InvalidOperationException">
    /// The component already belongs to an entity, or it has been destroyed.
    /// </exception>
    internal void AttachToEntity(Entity entity)
    {
        if (_state != ComponentState.Constructed)
        {
            throw new InvalidOperationException(IsDestroyed
                ? "The component has been destroyed, so it cannot be added to an entity."
                : "The component already belongs to an entity, so it cannot be added to another.");
        }

        _entity = entity;
        _state = ComponentState.Added;
    }

    /// <summary>Starts the component, at the beginning of the first step after it joined its entity.</summary>
    /// <param name="time">The time of the step the component starts in.</param>
    internal void InvokeStart(SimulationTime time)
    {
        Debug.Assert(IsAwaitingStart, "Only a component that has joined an entity and not started can start.");

        // The state changes first, so that a component whose OnStart throws is not started again, and is still
        // shut down exactly once.
        _state = ComponentState.Started;

        OnStart(time);
    }

    /// <summary>Advances the component by one step.</summary>
    /// <param name="time">The time of the step.</param>
    internal void InvokeUpdate(SimulationTime time)
    {
        Debug.Assert(_state == ComponentState.Started, "Only a started component updates.");

        OnUpdate(time);
    }

    /// <summary>Shuts the component down, which is final.</summary>
    internal void InvokeDestroyed()
    {
        Debug.Assert(!IsDestroyed, "A destroyed component is shut down exactly once.");

        // The state changes first, so that the hook sees the destruction it is reacting to.
        _state = ComponentState.Destroyed;

        OnDestroyed();
    }

    /// <summary>Called at the beginning of the first step after the component joined its entity.</summary>
    /// <param name="time">The simulated time at the start of the step, and the length of the step.</param>
    /// <remarks>
    /// Every component that joined with this one is in place, so an override can look its siblings up. A component
    /// that acquires something here must cope with never having started in its <see cref="OnDestroyed"/> override.
    /// </remarks>
    protected virtual void OnStart(SimulationTime time)
    {
    }

    /// <summary>Called once per step, to advance the component by the length of the step.</summary>
    /// <param name="time">The simulated time at the start of the step, and the length of the step.</param>
    protected virtual void OnUpdate(SimulationTime time)
    {
    }

    /// <summary>Called once, when the component, its entity or its simulation is destroyed.</summary>
    /// <remarks>
    /// The hook runs whether or not the component started, and whether or not it ever joined an entity, so an
    /// override releases what the constructor acquired and tolerates the rest being unset. Nothing should use a
    /// component after it has run.
    /// </remarks>
    protected virtual void OnDestroyed()
    {
    }

    /// <summary>The states a component passes through, in order.</summary>
    private enum ComponentState
    {
        /// <summary>The component has been constructed, and does not belong to an entity.</summary>
        Constructed,

        /// <summary>The component belongs to an entity, and has not started.</summary>
        Added,

        /// <summary>The component has started, and updates once per step.</summary>
        Started,

        /// <summary>The component has been shut down, and cannot be added to an entity again.</summary>
        Destroyed,
    }
}
