// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Invicta.Simulation.Primitives;

/// <summary>
/// One thing in a simulation: a container for the components that give it its state and behaviour. An entity has no
/// behaviour of its own.
/// </summary>
/// <remarks>
/// <para>
/// An entity belongs to the simulation that created it, through <see cref="Simulation.CreateEntity"/>, for its whole
/// life. It holds its components in the order they were added, and finds them by type, by key, or by both.
/// </para>
/// <para>
/// Lookups see the structure as it was when the step began: during a step they include components whose destruction
/// is pending and leave out components added during the step, so a lookup's answer does not depend on where the
/// caller comes in the update order. Each enumeration works on a snapshot taken when it began, so components can be
/// added and destroyed inside a <see langword="foreach"/> over a lookup's results.
/// </para>
/// </remarks>
public sealed class Entity
{
    private readonly SnapshotCollection<ComponentEntry> _components = [];
    private readonly List<ComponentEntry> _componentsToJoin = [];

    private bool _isRegisteredToShutDown;
    private bool _isRegisteredToJoinComponents;

    /// <summary>Initializes a new instance of the <see cref="Entity"/> class.</summary>
    /// <param name="simulation">The simulation the entity belongs to.</param>
    /// <param name="creationOrder">The position of the entity in the order its simulation created entities.</param>
    internal Entity(Simulation simulation, long creationOrder)
    {
        Simulation = simulation;
        CreationOrder = creationOrder;
    }

    /// <summary>Gets the simulation the entity belongs to.</summary>
    public Simulation Simulation { get; }

    /// <summary>Gets a value indicating whether the entity has been destroyed.</summary>
    /// <remarks>
    /// The value changes when the destruction is applied rather than when it is requested, as
    /// <see cref="Component.IsDestroyed"/> describes, and destroying an entity sets it on every component the entity
    /// holds.
    /// </remarks>
    public bool IsDestroyed { get; private set; }

    /// <summary>Gets the position of the entity in the order its simulation created entities.</summary>
    /// <remarks>
    /// Shutting down visits entities in this order, including entities that were created and destroyed within one
    /// step and so never joined the simulation's list.
    /// </remarks>
    internal long CreationOrder { get; }

    /// <summary>Gets a value indicating whether the entity's destruction has been requested.</summary>
    internal bool IsDestructionRequested { get; private set; }

    /// <summary>Adds a component to the entity.</summary>
    /// <typeparam name="T">The type of the component.</typeparam>
    /// <param name="component">The component to add.</param>
    /// <returns>The component that was added, so that the caller can keep the reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The component already belongs to an entity or has been destroyed, or this entity has been destroyed.
    /// </exception>
    /// <remarks>
    /// The component's <see cref="Component.Entity"/> is set at once. It joins the entity's list, and so becomes
    /// visible to lookups, at the end of the step if one is running, and at once between steps. Either way it starts
    /// at the beginning of the next step.
    /// </remarks>
    public T AddComponent<T>(T component)
        where T : Component
    {
        return AddKeyedComponent(key: null, component);
    }

    /// <summary>Adds a component to the entity under a key, such as the role it plays.</summary>
    /// <typeparam name="T">The type of the component.</typeparam>
    /// <param name="key">
    /// The key to add the component under, or <see langword="null"/> to add it as an unkeyed component.
    /// </param>
    /// <param name="component">The component to add.</param>
    /// <returns>The component that was added, so that the caller can keep the reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The component already belongs to an entity or has been destroyed, or this entity has been destroyed.
    /// </exception>
    /// <remarks>
    /// A key can be any object, and is compared with <see cref="object.Equals(object)"/>, so it should be immutable,
    /// such as a string or an enumeration value. Keys are scoped to their entity, and the same key can be used for
    /// several components, which <see cref="GetKeyedComponents{T}(object)"/> then returns together. A component's key
    /// is fixed when it is added.
    /// </remarks>
    public T AddKeyedComponent<T>(object? key, T component)
        where T : Component
    {
        ArgumentNullException.ThrowIfNull(component);

        if (IsDestroyed)
        {
            throw new InvalidOperationException("The entity has been destroyed, so it cannot take a component.");
        }

        // Attaching first leaves the entity unchanged if the component already belongs to one.
        component.AttachToEntity(this);

        ComponentEntry entry = new(key, component);
        if (Simulation.AreChangesDeferred)
        {
            _componentsToJoin.Add(entry);
            RegisterPendingComponents();
        }
        else
        {
            _components.Add(entry);
        }

        return component;
    }

    /// <summary>Registers the entity with its simulation, which joins its components at the end of the step.</summary>
    private void RegisterPendingComponents()
    {
        if (_isRegisteredToJoinComponents)
        {
            return;
        }

        _isRegisteredToJoinComponents = true;
        Simulation.RegisterEntityWithPendingComponents(this);
    }

    /// <summary>Gets the entity's only unkeyed component of a type.</summary>
    /// <typeparam name="T">The type to look up, which can be a base class or an interface.</typeparam>
    /// <returns>The only unkeyed component assignable to <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The entity has no such component, or it has more than one.
    /// </exception>
    public T GetComponent<T>()
        where T : class
    {
        return GetKeyedComponent<T>(key: null);
    }

    /// <summary>Gets the entity's only unkeyed component of a type, if it has one.</summary>
    /// <typeparam name="T">The type to look up, which can be a base class or an interface.</typeparam>
    /// <param name="component">
    /// When this method returns, the only unkeyed component assignable to <typeparamref name="T"/>, or
    /// <see langword="null"/> if the entity has none.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entity has such a component; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">The entity has more than one such component.</exception>
    /// <remarks>
    /// Several matches throw rather than return <see langword="false"/>, because "two engines" is not "no engine".
    /// </remarks>
    public bool TryGetComponent<T>([NotNullWhen(true)] out T? component)
        where T : class
    {
        return TryGetKeyedComponent(key: null, out component);
    }

    /// <summary>Gets the entity's unkeyed components of a type, in the order they were added.</summary>
    /// <typeparam name="T">The type to look up, which can be a base class or an interface.</typeparam>
    /// <returns>Every unkeyed component assignable to <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// The result is evaluated when it is enumerated. <c>GetComponents&lt;Component&gt;()</c> returns every unkeyed
    /// component the entity holds.
    /// </remarks>
    public IEnumerable<T> GetComponents<T>()
        where T : class
    {
        return GetKeyedComponents<T>(key: null);
    }

    /// <summary>Gets the entity's only component of a type that was added under a key.</summary>
    /// <typeparam name="T">The type to look up, which can be a base class or an interface.</typeparam>
    /// <param name="key">
    /// The key the component was added under, or <see langword="null"/> to look up unkeyed components.
    /// </param>
    /// <returns>The only component assignable to <typeparamref name="T"/> that was added under the key.</returns>
    /// <exception cref="InvalidOperationException">
    /// The entity has no such component, or it has more than one.
    /// </exception>
    public T GetKeyedComponent<T>(object? key)
        where T : class
    {
        if (!TryGetKeyedComponent(key, out T? component))
        {
            throw new InvalidOperationException($"The entity has no {DescribeLookup<T>(key)}.");
        }

        return component;
    }

    /// <summary>Gets the entity's only component of a type that was added under a key, if it has one.</summary>
    /// <typeparam name="T">The type to look up, which can be a base class or an interface.</typeparam>
    /// <param name="key">
    /// The key the component was added under, or <see langword="null"/> to look up unkeyed components.
    /// </param>
    /// <param name="component">
    /// When this method returns, the only component assignable to <typeparamref name="T"/> that was added under the
    /// key, or <see langword="null"/> if the entity has none.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entity has such a component; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">The entity has more than one such component.</exception>
    public bool TryGetKeyedComponent<T>(object? key, [NotNullWhen(true)] out T? component)
        where T : class
    {
        T? match = null;
        bool hasSeveralMatches = false;

        foreach (T candidate in GetKeyedComponents<T>(key))
        {
            if (match is null)
            {
                match = candidate;
            }
            else
            {
                hasSeveralMatches = true;
            }
        }

        if (hasSeveralMatches)
        {
            throw CreateAmbiguousLookupException<T>(key);
        }

        component = match;

        return component is not null;
    }

    /// <summary>
    /// Gets the entity's components of a type that were added under a key, in the order they were added.
    /// </summary>
    /// <typeparam name="T">The type to look up, which can be a base class or an interface.</typeparam>
    /// <param name="key">
    /// The key the components were added under, or <see langword="null"/> to look up unkeyed components.
    /// </param>
    /// <returns>Every component assignable to <typeparamref name="T"/> that was added under the key.</returns>
    /// <remarks>The result is evaluated when it is enumerated.</remarks>
    public IEnumerable<T> GetKeyedComponents<T>(object? key)
        where T : class
    {
        foreach (ComponentEntry entry in _components)
        {
            if (entry.HasKey(key) && entry.Component is T match)
            {
                yield return match;
            }
        }
    }

    /// <summary>Describes what a lookup was for, for an exception message.</summary>
    /// <typeparam name="T">The type that was looked up.</typeparam>
    /// <param name="key">The key that was looked up, or <see langword="null"/> for an unkeyed lookup.</param>
    /// <returns>A description of the lookup, such as <c>component of type Engine under the key "left"</c>.</returns>
    private static string DescribeLookup<T>(object? key)
        where T : class
    {
        return key is null
            ? $"component of type {typeof(T)}"
            : $"component of type {typeof(T)} under the key \"{key}\"";
    }

    /// <summary>Creates the exception for a lookup that matched more than one component.</summary>
    /// <typeparam name="T">The type that was looked up.</typeparam>
    /// <param name="key">The key that was looked up, or <see langword="null"/> for an unkeyed lookup.</param>
    /// <returns>An exception naming every component that matched.</returns>
    private InvalidOperationException CreateAmbiguousLookupException<T>(object? key)
        where T : class
    {
        string matches = string.Join(", ", GetKeyedComponents<T>(key).Select(match => match.GetType().ToString()));

        return new InvalidOperationException(
            $"The entity has more than one {DescribeLookup<T>(key)}: {matches}. Look the components up by a " +
            "narrower type, or add them under keys.");
    }

    /// <summary>Destroys the entity and every component it holds.</summary>
    /// <remarks>
    /// During a step the destruction is applied at the end of it, and between steps it is applied at once, as
    /// <see cref="Component.Destroy"/> describes. Each of the entity's components receives
    /// <see cref="Component.OnDestroyed"/> exactly once, and the entity leaves its simulation once they have gone.
    /// Destroying an entity twice does nothing.
    /// </remarks>
    public void Destroy()
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestructionRequested = true;

        RegisterPendingShutdown();
    }

    /// <summary>Registers the entity with its simulation as having something to shut down.</summary>
    internal void RegisterPendingShutdown()
    {
        if (_isRegisteredToShutDown)
        {
            return;
        }

        _isRegisteredToShutDown = true;
        Simulation.RegisterEntityWithPendingShutdown(this);
    }

    /// <summary>Starts the components that have joined the entity since the last step.</summary>
    /// <param name="time">The time of the step.</param>
    internal void StartAddedComponents(SimulationTime time)
    {
        foreach (ComponentEntry entry in _components)
        {
            if (entry.Component.IsAwaitingStart)
            {
                entry.Component.InvokeStart(time);
            }
        }
    }

    /// <summary>Advances the entity's components by one step, in the order they were added.</summary>
    /// <param name="time">The time of the step.</param>
    internal void UpdateComponents(SimulationTime time)
    {
        foreach (ComponentEntry entry in _components)
        {
            entry.Component.InvokeUpdate(time);
        }
    }

    /// <summary>
    /// Applies the entity's own destruction, if it was requested, and collects the components this shutdown pass is
    /// to shut down: every component the entity holds if it is being destroyed, and those that are marked otherwise.
    /// </summary>
    /// <param name="components">The pass's components, which the collected components are added to.</param>
    internal void CollectComponentsToShutDown(List<Component> components)
    {
        // Clearing the registration first leaves anything a hook destroys during this pass for the next one.
        _isRegisteredToShutDown = false;

        if (IsDestructionRequested)
        {
            IsDestroyed = true;
        }

        foreach (ComponentEntry entry in _components)
        {
            CollectComponentToShutDown(entry.Component, components);
        }

        foreach (ComponentEntry entry in _componentsToJoin)
        {
            CollectComponentToShutDown(entry.Component, components);
        }
    }

    /// <summary>Collects one component, if it is being shut down and has not been already.</summary>
    /// <param name="component">The component to consider.</param>
    /// <param name="components">The pass's components, which the component is added to.</param>
    private void CollectComponentToShutDown(Component component, List<Component> components)
    {
        if (!component.IsDestroyed && (IsDestroyed || component.IsDestructionRequested))
        {
            components.Add(component);
        }
    }

    /// <summary>Takes the components shut down in this pass out of the entity, together with their keys.</summary>
    internal void RemoveDestroyedComponents()
    {
        _components.RemoveAll(static entry => entry.Component.IsDestroyed);
        _componentsToJoin.RemoveAll(static entry => entry.Component.IsDestroyed);
    }

    /// <summary>Joins the components added since the changes were last applied to the entity's list.</summary>
    internal void JoinAddedComponents()
    {
        _isRegisteredToJoinComponents = false;

        _components.AddRange(_componentsToJoin);
        _componentsToJoin.Clear();
    }

    /// <summary>One component and the key it was added under.</summary>
    /// <param name="Key">The key the component was added under, or <see langword="null"/> if it has none.</param>
    /// <param name="Component">The component.</param>
    private readonly record struct ComponentEntry(object? Key, Component Component)
    {
        /// <summary>Returns a value indicating whether the component was added under a key.</summary>
        /// <param name="key">The key to compare with, or <see langword="null"/> for an unkeyed component.</param>
        /// <returns><see langword="true"/> if the keys match; otherwise, <see langword="false"/>.</returns>
        public bool HasKey(object? key)
        {
            return Key is null ? key is null : Key.Equals(key);
        }
    }
}
