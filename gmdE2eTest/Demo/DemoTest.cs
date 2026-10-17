using System.Globalization;
using System.Text.RegularExpressions;
using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Demo;

// The scripted session the demo animation is made from, see scripts/demo. It is written like any other
// end-to-end test, since it drives gmd the same way and needs the same care (never send a key into
// a screen that has not settled), but it asserts nothing and is only recorded when scripts/demo asks for
// it, by naming the cast file to write. In any other run it is skipped.
//
// The story: a repository where only your own branch and main are shown, two other branches
// brought in (one from the branch menu, one from the marker where it was merged), a look at a
// commit and its diff, a search, then committing, pushing and updating the other branches, and
// last merging main, which stops on a conflict that the resolver combines both changes of.
// A caption under the screen says what each part shows, the keys it names between backticks, which
// are drawn as key caps, and each is a scene, numbered (DemoRecording.Caption).
[TestClass]
public class DemoTest
{
    // Wide enough that the author and time columns are shown in full, see RepoWriter.Columns
    const int Width = 140;
    const int Height = 30;

    static string CastPath => Environment.GetEnvironmentVariable("GMD_DEMO_CAST") ?? "";

    // The folder the README's pictures are recorded in, see RecordReadmePictures
    static string PicturesPath => Environment.GetEnvironmentVariable("GMD_DEMO_PICTURES") ?? "";

    // gmd's log of the demo's repository as the README shows it, all of its rows in sight, and its
    // subjects and branch names, as 'git log --oneline' has them, and not the ids, the authors and
    // the times, which a wider view adds, and the animation shows
    const int PictureWidth = 80;
    const int PictureHeight = 15;

    [TestMethod]
    [TestCategory("Demo")]
    public async Task RecordDemo()
    {
        if (CastPath == "")
            Assert.Inconclusive("The demo is only recorded by scripts/demo");

        using var repo = await DemoRepo.CreateAsync();

        // The commit the demo makes is pinned too, to the demo's 'now'. The key-hint line is on, as
        // it is for a user, rather than off as the other end-to-end tests have it (see TempHome).
        using var gmd = TmuxSession.StartGmd(repo, Width, Height, commitTime: DemoRepo.Now, isKeyHints: true);
        var demo = new DemoRecording(gmd, Width, Height, screen => Rewrite(screen, repo.Path));

        // The log: the current branch and main, and ┣╮ markers where hidden branches come and go
        gmd.WaitFor("Initial project setup");
        demo.Caption(
            "Only your branch and main are shown: the short marks on main are where hidden branches come and go"
        );
        demo.Frame(3.5, "log");

        // Shift-→ opens the Show Branch menu, and 'Active' lists the branches still in use
        gmd.Send("S-Right");
        gmd.WaitFor("Show Branch (type to find)");
        demo.Caption("`⇧→` shows another branch: type to find it, or pick it from a list");
        demo.Frame(1.2, "menu");
        Press(gmd, demo, "Down", 0.6);
        gmd.Send("Right");
        gmd.WaitFor("Show All Active");
        demo.Caption("Active lists the branches still worked on");
        demo.Frame(1.5, "active");

        // Down past 'Show All Active', bugfix/cart-total, feature/dark-mode and feature/login
        for (int i = 0; i < 4; i++)
            Press(gmd, demo, "Down", i < 3 ? 0.35 : 1);
        gmd.Send("Enter");
        gmd.WaitFor("Search by category");
        demo.Caption("feature/search is shown, in a column and a color of its own");
        demo.Frame(3, "search-shown");

        // Down to where feature/checkout was merged, left to highlight main there, and enter
        // shows the branch that came in, which was deleted since and so is drawn in gray
        demo.Caption("`Enter` where a branch was merged shows it, even one deleted long ago, drawn in gray");
        for (int i = 0; i < 7; i++)
            Press(gmd, demo, "Down", i < 6 ? 0.2 : 0.8);
        Press(gmd, demo, "Left", 0.8);
        gmd.Send("Enter");
        gmd.WaitFor("Add checkout form");
        demo.Frame(3, "checkout-shown");

        // Right off the branches to the commit, which showing the branch moved to, up to a commit
        // on feature/search, and its details and diff
        demo.Caption("`Enter` shows the details of a commit, and `d` its diff");
        Press(gmd, demo, "Right", 0.3);
        Press(gmd, demo, "Right", 0.5);
        for (int i = 0; i < 7; i++)
            Press(gmd, demo, "Up", i < 6 ? 0.2 : 0.8);
        gmd.Send("Enter");
        gmd.WaitFor("Parents:");
        demo.Frame(2.5, "details");
        // The file's header in the diff, which the details' list of files has no bar of
        gmd.Send("d");
        gmd.WaitFor("▌ M src/search.js");
        demo.Caption("The diff, side by side");
        demo.Frame(4, "diff");
        gmd.Send("Escape");
        gmd.WaitUntilGone("▌ M src/search.js");
        demo.Caption("");
        demo.Frame(0.8);
        gmd.Send("Enter");
        gmd.WaitUntilGone("Parents:");
        demo.Frame(0.8);

        // Search: the log narrows to the matching commits as you type
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        demo.Caption("`f` searches: the log narrows to the matching commits as you type");
        demo.Frame(0.8, "filter");
        demo.Type("cart", 0.25);
        demo.Frame(2.5, "filtered");
        gmd.Send("Escape");
        gmd.WaitUntilGone("Filter Commits");
        demo.Caption("");
        demo.Frame(1);

        // Commit the work in progress, push it, and update main, which was behind origin. From the
        // top row, so that the commit made there is the current one, drawn bright, as it is pushed.
        Press(gmd, demo, "Home", 0.8);
        gmd.Send("c");
        gmd.WaitFor("Commit 1 change");
        demo.Caption("`c` commits the changes, with the files to commit ticked");
        demo.Frame(1, "commit-dialog");
        demo.Type("Add password reset link", 0.08);
        demo.Frame(1);
        gmd.Send("M-o");
        gmd.WaitUntilGone("Commit 1 change");
        gmd.WaitUntilGone("uncommitted changes");
        demo.Caption("Committed: ▲ says it is not pushed yet");
        demo.Frame(2.5, "committed");
        gmd.Send("p");
        gmd.WaitUntilGone("▲");
        demo.Caption("`p` pushed it");
        demo.Frame(2.5, "pushed");
        gmd.Send("U");
        gmd.WaitUntilGone("▼");
        demo.Caption("`⇧u` pulled every shown branch: main, which was behind origin (▼), is up to date");
        demo.Frame(3.5, "updated");

        // Merge main, where a teammate changed the start of src/app.js, which this branch changed
        // too: git stops on it, and the resolver combines the two, which changed other words. Left
        // to highlight the current branch, whose e lists the branches to merge into it, down past
        // ~feature/checkout and feature/search to main.
        Press(gmd, demo, "Left", 0.6);
        gmd.Send("e");
        gmd.WaitFor("Merge into feature/login");
        demo.Caption("`e` merges another branch into yours: main, with a teammate's change");
        demo.Frame(1.2, "merge-menu");
        Press(gmd, demo, "Down", 0.4);
        Press(gmd, demo, "Down", 1);
        gmd.Send("Enter");
        gmd.WaitFor("Merge Stopped on Conflicts");
        demo.Caption("Both changed the start of src/app.js: git stops on a conflict");
        demo.Frame(3, "conflict-box");

        gmd.Send("Enter"); // Resolve Conflicts, the default
        gmd.WaitFor("─── Conflict 1");
        demo.Caption("Each side as what it changed, and below, the file as it will be saved");
        demo.Frame(4.5, "resolver");
        gmd.Send("c");
        gmd.WaitFor("both changes combined");
        demo.Caption("They changed other words of the lines, and `c` combines both");
        demo.Frame(4, "combined");
        gmd.Send("s");
        gmd.WaitFor("Conflicts Resolved");
        demo.Caption("`s` saves the file, and with no conflict left, Commit finishes the merge");
        demo.Frame(2.5, "resolved");
        gmd.Send("Enter");
        gmd.WaitFor("Merge branch 'main'");
        demo.Frame(1.5, "merge-commit");
        gmd.Send("M-o");
        gmd.WaitUntilGone("on 'feature/login':"); // The commit dialog's title
        gmd.WaitUntilGone("uncommitted");
        demo.Caption("Merged, with both changes");
        demo.Frame(3.5, "merged");

        demo.Save(CastPath);
    }

    // The two pictures the README compares, of the repository the demo starts from: git's own graph
    // of it, 'git log --graph', in git's colors, and gmd's log as gmd first shows it. Each a cast of
    // one frame, which scripts/demo renders beside the animation, so that they are made again with
    // it, and change only when what git or gmd draws does.
    [TestMethod]
    [TestCategory("Demo")]
    public async Task RecordReadmePictures()
    {
        if (PicturesPath == "")
            Assert.Inconclusive("The README's pictures are only recorded by scripts/demo");

        using var repo = await DemoRepo.CreateAsync();

        // Before gmd runs, which adds refs of its own that --all would show
        var log = Proc.Ok("git", ["log", "--graph", "--oneline", "--decorate", "--all", "--color=always"], repo.Path);
        var rows = log.TrimEnd('\n').Split('\n');
        var width = rows.Max(r => Regex.Replace(r, @"\x1b\[[0-9;]*m", "").Length) + 1;
        DemoRecording.SaveText(Path.Join(PicturesPath, "git-log.cast"), rows, width);

        using var gmd = TmuxSession.StartGmd(repo, PictureWidth, PictureHeight, commitTime: DemoRepo.Now);
        var picture = new DemoRecording(
            gmd,
            PictureWidth,
            PictureHeight,
            screen => Rewrite(screen, repo.Path),
            isCaptioned: false
        );
        gmd.WaitFor("Initial project setup");
        picture.Frame(1, "gmd-log");
        picture.Save(Path.Join(PicturesPath, "gmd-log.cast"));
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
        var dates = string.Join("|", new[] { today.AddDays(-1), today }.Select(d => d.ToString("yyyy-MM-dd")));
        var now = DemoRepo.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        // A time, or a narrower view's date alone
        screen = screen.Replace(repoPath, $"/{DemoRepo.RelativePath}");
        return Regex.Replace(screen, $@"\b({dates})( \d\d:\d\d)?\b", m => m.Groups[2].Success ? now : now[..10]);
    }
}
