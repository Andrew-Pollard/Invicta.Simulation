// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

namespace Invicta.Simulation.Primitives;

/// <summary>The span of simulated time that one update covers.</summary>
/// <param name="Elapsed">The simulated time elapsed at the start of the step.</param>
/// <param name="Delta">The length of the step, often written Δt.</param>
/// <remarks>
/// <para>
/// A <see cref="Component.OnUpdate"/> call advances its component from <see cref="Elapsed"/> to
/// <c>Elapsed + Delta</c>. <see cref="Component.OnStart"/> receives the time of the step the component starts in,
/// which is the value every <see cref="Component.OnUpdate"/> in that step receives.
/// </para>
/// <para>
/// <see cref="Delta"/> is the length of one step and not of every step, because the caller chooses each step's
/// length. A component that converts a rate into a per-step value, such as an hourly failure rate into a
/// probability, computes it in <see cref="Component.OnUpdate"/> from <see cref="Delta"/>, or caches it against the
/// <see cref="Delta"/> it was computed for.
/// </para>
/// </remarks>
public readonly record struct SimulationTime(TimeSpan Elapsed, TimeSpan Delta);
