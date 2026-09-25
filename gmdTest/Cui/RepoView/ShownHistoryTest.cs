using gmd.Cui.RepoView;

namespace gmdTest.Cui.RepoView;

// What Backspace goes back to: the branches shown before each show or hide, the last one first
[TestClass]
public class ShownHistoryTest
{
    [TestMethod]
    public void TestUndoGoesBackOneChangeAtATime()
    {
        var history = new ShownHistory();
        history.Add(["main"], ["main", "dev"], "Show", "'dev'", "dev");
        history.Add(["main", "dev"], ["main", "dev", "feature"], "Show", "'feature'", "feature");

        Assert.AreEqual("Show 'feature'", history.Undo()?.ToString());
        var first = history.Undo();
        Assert.AreEqual("Show 'dev'", first?.ToString());
        CollectionAssert.AreEqual(new[] { "main" }, first!.Before.ToArray(), "What was shown before it");
        Assert.IsNull(history.Undo(), "Nothing left to undo");
    }

    // Showing a branch already shown changes nothing, and undoing it would look like a dropped key,
    // so it is not recorded. The order the names come in does not matter.
    [TestMethod]
    public void TestAChangeThatShowsOrHidesNothingIsNotRecorded()
    {
        var history = new ShownHistory();
        history.Add(["main", "dev"], ["dev", "main"], "Show", "'dev'", "dev");

        Assert.IsNull(history.Last);
    }

    // A hide says so, for the hint and for scrolling to the branch it brings back
    [TestMethod]
    public void TestAHideIsTold()
    {
        var history = new ShownHistory();
        history.Add(["main", "dev"], ["main"], "Hide", "'dev'", "dev");

        Assert.IsTrue(history.Last!.IsHide);
        Assert.AreEqual("dev", history.Last.BranchName);
    }

    // The oldest change is dropped once there are a hundred, rather than the history growing
    // without bound in a long session
    [TestMethod]
    public void TestTheOldestChangesAreDroppedPastAHundred()
    {
        var history = new ShownHistory();
        for (int i = 0; i < 105; i++)
            history.Add(["main"], ["main", $"b{i}"], "Show", $"'b{i}'");

        var undone = 0;
        string last = "";
        while (history.Undo() is ShownChange change)
        {
            undone++;
            last = change.What;
        }
        Assert.AreEqual(100, undone);
        Assert.AreEqual("'b5'", last, "The first five were dropped");
    }

    [TestMethod]
    public void TestClearForgetsEverything()
    {
        var history = new ShownHistory();
        history.Add(["main"], ["main", "dev"], "Show", "'dev'", "dev");

        history.Clear();

        Assert.IsNull(history.Undo());
    }
}
