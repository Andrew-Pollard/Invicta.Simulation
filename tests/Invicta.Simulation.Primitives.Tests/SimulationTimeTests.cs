// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Invicta.Simulation.Primitives;

internal sealed class SimulationTimeTests
{
    [Test]
    public void Constructor_SetsElapsedAndDelta()
    {
        SimulationTime time = new(TimeSpan.FromHours(2), TimeSpan.FromMinutes(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(time.Elapsed, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(time.Delta, Is.EqualTo(TimeSpan.FromMinutes(5)));
        }
    }

    [Test]
    public void Equals_SameElapsedAndDelta_IsTrue()
    {
        SimulationTime time = new(TimeSpan.FromHours(2), TimeSpan.FromMinutes(5));
        SimulationTime same = new(TimeSpan.FromHours(2), TimeSpan.FromMinutes(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(same, Is.EqualTo(time));
            Assert.That(same.GetHashCode(), Is.EqualTo(time.GetHashCode()));
        }
    }

    [Test]
    public void Equals_DifferentDelta_IsFalse()
    {
        SimulationTime time = new(TimeSpan.FromHours(2), TimeSpan.FromMinutes(5));
        SimulationTime other = new(TimeSpan.FromHours(2), TimeSpan.FromMinutes(10));

        Assert.That(other, Is.Not.EqualTo(time));
    }
}
