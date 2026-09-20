// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Invicta.Simulation.Primitives;

internal sealed class SnapshotCollectionTests
{
    [Test]
    public void Count_AfterAddingAndRemoving_ShowsTheItemsHeldNow()
    {
        SnapshotCollection<string> collection = [];

        collection.Add("first");
        collection.Add("second");
        collection.RemoveAll(static item => item == "first");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection, Has.Count.EqualTo(1));
            string[] expected = ["second"];
            Assert.That(collection, Is.EqualTo(expected));
        }
    }

    [Test]
    public void AddRange_AddsTheItemsInOrder()
    {
        SnapshotCollection<string> collection = ["first"];

        collection.AddRange(["second", "third"]);

        string[] expected = ["first", "second", "third"];

        Assert.That(collection, Is.EqualTo(expected));
    }

    [Test]
    public void Snapshot_CollectionUnchanged_ReturnsTheSameArray()
    {
        SnapshotCollection<string> collection = ["first"];

        string[] snapshot = collection.GetSnapshot();

        Assert.That(collection.GetSnapshot(), Is.SameAs(snapshot));
    }

    [Test]
    public void Snapshot_AfterAdd_ReturnsANewArray()
    {
        SnapshotCollection<string> collection = ["first"];

        string[] snapshot = collection.GetSnapshot();
        collection.Add("second");

        Assert.That(collection.GetSnapshot(), Is.Not.SameAs(snapshot));
    }

    [Test]
    public void Snapshot_AfterRemoveAllThatRemovedNothing_ReturnsTheSameArray()
    {
        SnapshotCollection<string> collection = ["first"];

        string[] snapshot = collection.GetSnapshot();
        collection.RemoveAll(static item => item == "second");

        Assert.That(collection.GetSnapshot(), Is.SameAs(snapshot));
    }

    [Test]
    public void Snapshot_AfterAddRangeOfNothing_ReturnsTheSameArray()
    {
        SnapshotCollection<string> collection = ["first"];

        string[] snapshot = collection.GetSnapshot();
        collection.AddRange([]);

        Assert.That(collection.GetSnapshot(), Is.SameAs(snapshot));
    }

    [Test]
    public void GetEnumerator_ItemAddedWhileEnumerating_IsNotSeenAndDoesNotThrow()
    {
        SnapshotCollection<string> collection = ["first"];

        List<string> enumerated = [];
        foreach (string item in collection)
        {
            enumerated.Add(item);
            collection.Add("second");
        }

        using (Assert.EnterMultipleScope())
        {
            string[] expected = ["first"];
            Assert.That(enumerated, Is.EqualTo(expected));
            Assert.That(collection, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void GetEnumerator_ItemRemovedWhileEnumerating_IsStillSeenAndDoesNotThrow()
    {
        SnapshotCollection<string> collection = ["first", "second"];

        List<string> enumerated = [];
        foreach (string item in collection)
        {
            enumerated.Add(item);
            collection.RemoveAll(static _ => true);
        }

        using (Assert.EnterMultipleScope())
        {
            string[] expected = ["first", "second"];
            Assert.That(enumerated, Is.EqualTo(expected));
            Assert.That(collection, Is.Empty);
        }
    }
}
