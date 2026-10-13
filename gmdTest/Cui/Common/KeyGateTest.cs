using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// Keys are dropped while git works, except in a dialog
[TestClass]
public class KeyGateTest
{
    [TestMethod]
    public void TestKeysAreDroppedWhileGitWorks()
    {
        var keys = new KeyGate();
        Assert.IsFalse(keys.IsDropping);

        keys.GitStarted();
        Assert.IsTrue(keys.IsDropping);

        keys.GitDone();
        Assert.IsFalse(keys.IsDropping);
    }

    // A command asks in a dialog before it runs git, with the progress already shown
    [TestMethod]
    public void TestADialogTakesTheKeysWhileGitWorks()
    {
        var keys = new KeyGate();
        keys.GitStarted();

        keys.DialogOpened();
        Assert.IsFalse(keys.IsDropping);

        keys.DialogClosed();
        Assert.IsTrue(keys.IsDropping, "Dropped again, since git still works");
    }

    // A menu opened by a click while a read ran in the background, and closed after it was done,
    // put back what it found, 'drop every key', and the keyboard was dead until git next ran
    [TestMethod]
    public void TestADialogClosedAfterGitIsDoneLeavesTheKeysOn()
    {
        var keys = new KeyGate();
        keys.GitStarted();
        keys.DialogOpened();

        keys.GitDone();
        keys.DialogClosed();

        Assert.IsFalse(keys.IsDropping);
    }

    // The spelling menu over the commit dialog, which the command shows with the progress running:
    // the commit dialog under it still takes the keys once the menu has closed
    [TestMethod]
    public void TestADialogOverADialogLeavesTheKeysToTheOneUnder()
    {
        var keys = new KeyGate();
        keys.GitStarted();
        keys.DialogOpened();
        keys.DialogOpened();

        keys.DialogClosed();

        Assert.IsFalse(keys.IsDropping);
    }
}
