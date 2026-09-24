using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// Copying rows of the log view.
//
// The one thing gmd does that leaves the application entirely, and the only tier that can
// check it: what a clipboard holds afterwards is not on any screen and cannot be faked. The
// session has no display and no clipboard tool it can reach (see TmuxSession), so the path
// taken here is the last one in ClipboardService's chain — OSC 52, i.e. asking the terminal
// itself — and tmux keeps what it is sent as a buffer, which is what is read back.
//
// That is also the path that matters most for gmd, since it is the only one that works over
// ssh or from inside a container, where no local tool can reach the user's own clipboard.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class CopyTest
{
    // Shift+Down selects rows, Ctrl+C copies them: the sid and subject of each selected commit
    [TestMethod]
    public async Task TestCopySelectedCommits()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("C-c");

        Assert.AreEqual(
            """
            17d85b Add delta
            4e73d2 Merge branch 'dev' into main
            """,
            gmd.WaitForClipboard()
        );
    }

    // A selection within a single row copies that row as it is drawn, rather than the sid and
    // subject the multi row copy builds — so the graph column comes with it, and so does the '|'
    // that marks the row as selected, which stands where the '●' current commit marker is drawn
    // when it is not. Characterization: that is what gmd does today, not what it ought to do.
    [TestMethod]
    public async Task TestCopyOneSelectedRow()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("C-c");

        Assert.AreEqual(
            "┣  | Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06",
            gmd.WaitForClipboard()
        );
    }
}
