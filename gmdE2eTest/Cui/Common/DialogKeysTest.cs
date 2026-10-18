using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Common;

// The arrow keys and Tab in the dialogs. Terminal.Gui 2.x moves the focus with the arrow keys too,
// wherever the view that has it does not use them, which crashed gmd in the Config dialog: its list
// of Colors closed as the focus left it, within 2.x's change of the focus (ConfigDlgTest). The keys
// go to each dialog here, one at a time, and it stays up, as gmd does.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class DialogKeysTest
{
    static readonly string[] Keys = ["Up", "Down", "Left", "Right", "Tab", "BTab", "Down", "Right", "Tab", "Left"];

    // The steps open the dialog: a key, 'click:<text>' a click on the text, 'wait:<text>' a wait for
    // it. The title is the dialog's, which is to stay up.
    [TestMethod]
    [DataRow("changes", "╭ Commit", "c")]
    [DataRow("plain", "╭ Create Branch", "b")]
    [DataRow("plain", "╭ Add Tag", "t")]
    [DataRow("plain", "Filter Commits", "f")]
    [DataRow("plain", "Quit gmd?", "Escape")]
    [DataRow("plain", "╭ Tags", "m", "wait:Commit ...", "click:Tag  ", "wait:Tags ...", "click:Tags ...")]
    [DataRow("worktree", "Kind    Branch", "w")]
    [DataRow("worktree", "╭ Create Worktree", "w", "wait:Kind    Branch", "click:Add ...")]
    [DataRow(
        "worktree",
        "╭ Create Worktree",
        "w",
        "wait:Kind    Branch",
        "click:Add ...",
        "wait:╭ Create",
        "click:▼",
        "wait:│main"
    )]
    public async Task TestTheArrowKeysAndTabLeaveTheDialogUp(string fixture, string title, params string[] steps)
    {
        using var repo = fixture switch
        {
            "changes" => await E2eRepo.CreateWithChangesAsync(),
            "worktree" => await E2eRepo.CreateWithWorktreeAsync(),
            _ => await E2eRepo.CreateAsync(),
        };
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        foreach (var s in steps)
        {
            if (s.StartsWith("wait:"))
            {
                gmd.WaitFor(s[5..]);
                continue;
            }
            if (s.StartsWith("click:"))
            {
                var (x, y) = TmuxSession.PositionOf(gmd.WaitForStable(), s[6..]);
                gmd.Click(x, y);
            }
            else
            {
                gmd.Send(s);
            }
            gmd.WaitForStable();
        }
        gmd.WaitFor(title);

        foreach (var key in Keys)
        {
            gmd.Send(key);
            var screen = gmd.WaitForStable();
            Assert.IsTrue(gmd.IsRunning, $"gmd ended at {key}");
            StringAssert.Contains(screen, title, $"The dialog is up after {key}");
        }
    }
}
