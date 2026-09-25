using System.Globalization;
using System.Text.RegularExpressions;
using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Demo;

// The scripted session the demo animation is made from, see ./demo. It is written like any other
// end-to-end test, since it drives gmd the same way and needs the same care (never send a key into
// a screen that has not settled), but it asserts nothing and is only recorded when ./demo asks for
// it, by naming the cast file to write. In any other run it is skipped.
//
// The story: a repository where only your own branch and main are shown, two other branches
// brought in (one from the branch menu, one from the marker where it was merged), a look at a
// commit and its diff, a search, and then committing, pushing and updating the other branches.
[TestClass]
public class DemoTest
{
    // Wide enough that the author and time columns are shown in full, see RepoWriter.Columns
    const int Width = 140;
    const int Height = 30;

    static string CastPath => Environment.GetEnvironmentVariable("GMD_DEMO_CAST") ?? "";

    [TestMethod]
    [TestCategory("Demo")]
    public async Task RecordDemo()
    {
        if (CastPath == "")
            Assert.Inconclusive("The demo is only recorded by ./demo");

        using var repo = await DemoRepo.CreateAsync();

        // The commit the demo makes is pinned too, to the demo's 'now'. The key-hint line is on, as
        // it is for a user, rather than off as the other end-to-end tests have it (see TempHome).
        using var gmd = TmuxSession.StartGmd(repo, Width, Height, commitTime: DemoRepo.Now, isKeyHints: true);
        var demo = new DemoRecording(gmd, Width, Height, screen => Rewrite(screen, repo.Path));

        // The log: the current branch and main, and ┣╮ markers where hidden branches come and go
        gmd.WaitFor("Initial project setup");
        demo.Frame(3.5, "log");

        // Shift → opens the branch menu, and 'Active' lists the branches still in use
        gmd.Send("S-Right");
        gmd.WaitFor("Open Branch");
        demo.Frame(1.2, "menu");
        Press(gmd, demo, "Down", 0.6);
        gmd.Send("Right");
        gmd.WaitFor("Show All Active");
        demo.Frame(1.5, "active");

        // Down past 'Show All Active', bugfix/cart-total, feature/dark-mode and feature/login
        for (int i = 0; i < 4; i++)
            Press(gmd, demo, "Down", i < 3 ? 0.35 : 1);
        gmd.Send("Enter");
        gmd.WaitFor("Search by category");
        demo.Frame(3, "search-shown");

        // Down to where feature/checkout was merged, left to highlight main there, and enter
        // shows the branch that came in, which was deleted since and so is drawn in gray
        for (int i = 0; i < 7; i++)
            Press(gmd, demo, "Down", i < 6 ? 0.2 : 0.8);
        Press(gmd, demo, "Left", 0.8);
        gmd.Send("Enter");
        gmd.WaitFor("Add checkout form");
        demo.Frame(3, "checkout-shown");

        // Right off the branches to the commit, which showing the branch moved to, up to a commit
        // on feature/search, and its details and diff
        Press(gmd, demo, "Right", 0.3);
        Press(gmd, demo, "Right", 0.5);
        for (int i = 0; i < 7; i++)
            Press(gmd, demo, "Up", i < 6 ? 0.2 : 0.8);
        gmd.Send("Enter");
        gmd.WaitFor("Children:");
        demo.Frame(2.5, "details");
        gmd.Send("d");
        gmd.WaitFor("Modified: src/search.js");
        demo.Frame(4, "diff");
        gmd.Send("Escape");
        gmd.WaitUntilGone("Modified: src/search.js");
        demo.Frame(0.8);
        gmd.Send("Enter");
        gmd.WaitUntilGone("Children:");
        demo.Frame(0.8);

        // Search: the log narrows to the matching commits as you type
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        demo.Frame(0.8, "filter");
        demo.Type("cart", 0.25);
        demo.Frame(2.5, "filtered");
        gmd.Send("Escape");
        gmd.WaitUntilGone("Filter Commits");
        demo.Frame(1);

        // Commit the work in progress, push it, and update main, which was behind origin. From the
        // top row, so that the commit made there is the current one, drawn bright, as it is pushed.
        Press(gmd, demo, "Home", 0.8);
        gmd.Send("c");
        gmd.WaitFor("Commit 1 change");
        demo.Frame(1, "commit-dialog");
        demo.Type("Add password reset link", 0.08);
        demo.Frame(1);
        gmd.Send("M-o");
        gmd.WaitUntilGone("Commit 1 change");
        gmd.WaitUntilGone("uncommitted changes");
        demo.Frame(2.5, "committed");
        gmd.Send("p");
        gmd.WaitUntilGone("▲");
        demo.Frame(2.5, "pushed");
        gmd.Send("U");
        gmd.WaitUntilGone("▼");
        demo.Frame(3.5, "updated");

        demo.Save(CastPath);
    }

    // A key that only moves a highlight, which draws no text to wait for, so it is waited for by
    // the screen settling
    static void Press(TmuxSession gmd, DemoRecording demo, string key, double seconds)
    {
        gmd.Send(key);
        gmd.WaitForStable();
        demo.Frame(seconds);
    }

    // What would differ from run to run: the temp folder the repository is in, which the commit
    // details show in full, and the time of the uncommitted row, which is today's (gmd runs in
    // UTC, see TmuxSession). Both are replaced with what the demo pretends: the path the
    // application bar shows the end of, and the demo's 'now'.
    static string Rewrite(string screen, string repoPath)
    {
        var today = DateTime.UtcNow;
        var dates = string.Join("|", new[] { today.AddDays(-1), today }.Select(d => d.ToString("yy-MM-dd")));
        var now = DemoRepo.Now.ToString("yy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        screen = screen.Replace(repoPath, $"/{DemoRepo.RelativePath}");
        return Regex.Replace(screen, $@"\b({dates}) \d\d:\d\d\b", now);
    }
}
