// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

namespace Invicta.Simulation.Primitives;

/// <summary>
/// A component that counts the hooks it receives, records them in a shared log, and runs an action in each, so that a
/// test can make changes from inside a hook.
/// </summary>
internal sealed class TrackedComponent(string name = "component", IList<string>? log = null) : Component
{
    private readonly string _name = name;
    private readonly IList<string>? _log = log;

    public int StartCount { get; private set; }

    public int UpdateCount { get; private set; }

    public int DestroyedCount { get; private set; }

    public SimulationTime StartTime { get; private set; }

    public SimulationTime UpdateTime { get; private set; }

    public bool WasDestroyedWhenShutDown { get; private set; }

    public Action<TrackedComponent>? StartAction { get; set; }

    public Action<TrackedComponent>? UpdateAction { get; set; }

    public Action<TrackedComponent>? DestroyedAction { get; set; }

    protected override void OnStart(SimulationTime time)
    {
        StartCount++;
        StartTime = time;
        Record("start");

        StartAction?.Invoke(this);
    }

    protected override void OnUpdate(SimulationTime time)
    {
        UpdateCount++;
        UpdateTime = time;
        Record("update");

        UpdateAction?.Invoke(this);
    }

    protected override void OnDestroyed()
    {
        DestroyedCount++;
        WasDestroyedWhenShutDown = IsDestroyed;
        Record("destroyed");

        DestroyedAction?.Invoke(this);
    }

    private void Record(string hook)
    {
        _log?.Add($"{_name}.{hook}");
    }
}
