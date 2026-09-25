using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// The status line: a message at the bottom of the log view for a few seconds, saying why a key did
// nothing or what a command did. It used to be silence, or a red error box to dismiss for what
// was no error. The key hints are off in these, as in every test but KeyHintTest, so the message
// is drawn over the bottom row of the log, which is also what shows that it never goes missing.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class StatusMessageTest
{
    // 'c' with nothing to commit says so, rather than nothing happening, and the message goes
    // again by itself
    [TestMethod]
    public async Task TestNothingToCommitIsSaid()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("c");

        Assert.AreEqual("Nothing to commit", ScreenText.LastLine(gmd.WaitFor("Nothing to commit")));
        StringAssert.Contains(
            ScreenText.LastLine(gmd.WaitUntilGone("Nothing to commit")),
            "Initial",
            "The log is back"
        );
    }

    // The keys that act on a highlighted branch say so when none is, and merging says why it
    // cannot while there are changes to commit
    [TestMethod]
    public async Task TestAKeyThatCannotActSaysWhy()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("e");
        Assert.AreEqual(
            "Highlight a branch with ← → first, and 'e' merges it into the current branch",
            ScreenText.LastLine(gmd.WaitFor("Highlight a branch"))
        );

        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("e");
        Assert.AreEqual("Commit the changes first, then merge", ScreenText.LastLine(gmd.WaitFor("then merge")));
    }

    // Nothing to push was a red 'Error !' box, which had to be dismissed; it is a line now
    [TestMethod]
    public async Task TestNothingToPushIsNotAnError()
    {
        using var repo = await E2eRepo.CreateBehindOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("▼1");

        gmd.Send("p");

        var screen = gmd.WaitFor("Nothing to push");
        Assert.AreEqual("Nothing to push on 'main'", ScreenText.LastLine(screen));
        Assert.IsFalse(screen.Contains("Error !"), "No error box");
    }

    // A fetch that fails leaves what the log shows of the remote stale, which used to go unsaid:
    // it is only logged, since it runs after every refresh. The first failure is said.
    [TestMethod]
    public async Task TestAFailingFetchIsSaid()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync($"remote set-url origin {Path.Join(repo.Path, "no-such-origin")}");
        using var gmd = TmuxSession.StartGmd(repo);

        StringAssert.StartsWith(ScreenText.LastLine(gmd.WaitFor("Fetch failed")), "Fetch failed: ");
    }

    // Copy with nothing selected says how to select
    [TestMethod]
    public async Task TestCopyWithNothingSelectedSaysHowToSelect()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("C-c");

        Assert.AreEqual(
            "Select rows with Shift-↑↓ first, and Ctrl-C copies them",
            ScreenText.LastLine(gmd.WaitFor("Select rows"))
        );
    }
}
