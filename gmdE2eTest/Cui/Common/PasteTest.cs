using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Common;

// Pasting from the terminal, a bracketed paste, which Terminal.Gui asks the terminal for: the text
// goes into the input the keys go to, as typed text, and a paste where no input is is no keys.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class PasteTest
{
    // Into the commit dialog's first field, which has the keys as it opens, though Terminal.Gui 2.5
    // takes the dialog's inside for the view with the focus then, and gave the paste to it, where it
    // went nowhere. A paste of two lines into the one-line field is its first line; into the message,
    // both lines. Nothing is committed: a paste that came as keys, as without the markers, pressed
    // Enter at its first line's end, which committed the message.
    [TestMethod]
    public async Task TestAPasteGoesIntoTheInputTheKeysGoTo()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        var head = await repo.HeadIdAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");
        gmd.Send("c");
        gmd.WaitFor("╭ Commit");

        gmd.Paste("Pasted subject");
        gmd.WaitFor("[Pasted subject");
        gmd.Paste(" and more\nwhich is the message");
        var dialog = gmd.WaitFor("[Pasted subject and more ");
        Assert.IsFalse(dialog.Contains("which is the message"), "A one-line field takes the first line");

        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.Paste("First line\nSecond line");
        var rows = gmd.WaitFor("Second line").Split('\n');
        var first = Array.FindIndex(rows, r => r.Contains("First line"));
        StringAssert.Contains(rows[first + 1], "Second line", "The message has both lines");

        StringAssert.Contains(gmd.Capture(), "╭ Commit", "The dialog is still up");
        Assert.AreEqual(head, await repo.HeadIdAsync(), "Nothing is committed");
    }

    // In the log, which takes no text, a paste is nothing, rather than its letters as keys: the 'b' in
    // it would ask for a branch, a 'p' push
    [TestMethod]
    public async Task TestAPasteWhereNoInputIsIsNoKeys()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        var before = gmd.WaitFor("Initial");

        gmd.Paste("b and p");

        Assert.AreEqual(before, gmd.WaitForStable(), "Nothing changed");
        Assert.IsTrue(gmd.IsRunning);
    }
}
