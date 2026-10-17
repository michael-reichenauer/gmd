using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// Committing, and the other commands that make or rewrite commits: amend, uncommit, squash,
// cherry-pick and stash.
//
// These change the repository rather than only looking at it, which is why a test that lets gmd
// commit pins the commit dates of its session (StartGmd's commitTime): without that the commit gmd
// makes is dated 'now', so its sid and its row in the time column would differ every run. See
// TmuxSession.EnvironmentVariables.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class CommitTest
{
    // The uncommitted row, which nothing in the suite drew until now: E2eRepo leaves the working
    // tree clean, so the Repo.UncommittedId sentinel commit, its '©2' change count in the
    // application bar and the current branch marker moving up onto it had no coverage at any tier.
    [TestMethod]
    public async Task TestUncommittedChangesAreShown()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);

        // Its time is DateTime.Now rather than a commit date, so it is the one thing on this
        // screen that has to be masked. The commit rows below it keep their pinned times.
        Assert.AreEqual(
            """
             Gmd {repo}, ●main, ©2                                          changes on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ©2 uncommitted changes                                            (● main)                         NNNN-NN-NN NN:NN
            ┣  ● Add delta                                                          [v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                              4e73d230 Test User      2024-10-15 12:05
            ┣    Add gamma                                                                 4a15fb13 Test User      2024-10-15 12:04
            ┣╯   Add beta                                                                  dd78912d Test User      2024-10-15 12:01
            ┗    Initial                                                                   9dc406e3 Test User      2024-10-15 12:00
            """,
            ScreenText.MaskTimes(ScreenText.Of(gmd.WaitFor("Initial"), repo.Path), "uncommitted")
        );
    }

    // A file renamed while gmd is open is a change, left to the file monitor to see. A rename keeps
    // the file's modification time, and the monitor used to date a change by that time, so the
    // rename looked older than the last read, was taken as seen by it, and never showed.
    [TestMethod]
    public async Task TestAFileRenamedWhileGmdIsOpenIsShown()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        File.Move(Path.Join(repo.Path, "alpha.txt"), Path.Join(repo.Path, "renamed.txt"));

        gmd.WaitFor("uncommitted changes");
    }

    // 'c' commits, i.e. the dialog, the git command behind it and the refreshed log view. The one
    // keystroke in this suite that writes a commit.
    [TestMethod]
    public async Task TestCommitChanges()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");

        gmd.Send("c");
        var dialog = gmd.WaitFor("Commit 2 changes");

        // The dialog itself: the change count and the branch it would commit to in the title row,
        // the subject field, the message body below it and the buttons
        Assert.AreEqual(
            """
                                   ╭ Commit ────────────────────────────────────────────────────────────────╮
                                   │ Commit 2 changes on 'main':                                            │
                                   │                                                                        │
                                   │[                                                  ]                    │
                                   │┌──────────────────────────────────────────────────────────────────────┐│
                                   ││                                                                      ││
            """,
            ScreenText.Rows(dialog, repo.Path, 9, 6)
        );

        // The subject field has the focus, so the message is simply typed, and Enter presses the
        // default OK button
        gmd.SendText("Add epsilon");
        gmd.WaitFor("Add epsilon");
        gmd.Send("Enter");

        // The uncommitted row is gone, the new commit is at the top with the branch tip on it, and
        // its sid and time are the pinned ones — not masked, because the session pinned the dates
        var committed = gmd.WaitUntilGone("uncommitted changes");
        Assert.AreEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add epsilon                                                      (● main) 2d039107 Test User      2024-10-15 12:07
            ┣    Add delta                                                          [v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                              4e73d230 Test User      2024-10-15 12:05
            ┣    Add gamma                                                                 4a15fb13 Test User      2024-10-15 12:04
            ┣╯   Add beta                                                                  dd78912d Test User      2024-10-15 12:01
            ┗    Initial                                                                   9dc406e3 Test User      2024-10-15 12:00
            """,
            ScreenText.Rows(committed, repo.Path, 0, 8)
        );
        Assert.AreEqual(
            "Committed to 'main'",
            ScreenText.LastLine(gmd.WaitFor("Committed to 'main'")),
            "What was done is said"
        );

        // And the repository really changed, which the screen alone does not prove. Both files are
        // in the commit: gmd runs 'git add .' before 'git commit -am', and the add is what picks
        // up the untracked one.
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
        Assert.AreEqual("Add epsilon", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("alpha.txt\nepsilon.txt", await repo.GitAsync("show --name-only --format= HEAD"));
    }

    // The subject and the message body are two separate fields joined into one commit message, so
    // what git ends up storing is worth pinning: the blank line between them is what makes the
    // subject a subject.
    [TestMethod]
    public async Task TestCommitWithAMessageBody()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.SendText("Add epsilon");
        gmd.WaitFor("Add epsilon");
        gmd.Send("Tab"); // From the subject field into the message body
        gmd.WaitForStable();
        gmd.SendText("Some body text");
        gmd.WaitFor("Some body text");
        gmd.Send("Tab"); // And on to the file list, since Enter in the body is a newline; the list
        // leaves Enter to the dialog, i.e. OK
        gmd.WaitForStable();
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("uncommitted changes"), "Add epsilon");
        Assert.AreEqual("Add epsilon\n\nSome body text", await repo.GitAsync("log --format=%B -1"));
        // The log view has no caret, so the dialog's must not outlive it
        Assert.IsFalse(gmd.IsCursorVisible, "The caret should be hidden again once the dialog has closed");
    }

    // The commit dialog lists the files, all ticked, and a file unticked with Space is left out of
    // the commit, as it is, uncommitted. The heading counts what is still ticked.
    [TestMethod]
    public async Task TestAnUntickedFileIsLeftUncommitted()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        var dialog = gmd.WaitFor("Files, Space");
        StringAssert.Contains(dialog, "◙ M alpha.txt");
        StringAssert.Contains(dialog, "◙ A epsilon.txt");

        gmd.SendText("Add epsilon only");
        gmd.WaitFor("Add epsilon only");
        gmd.Send("Tab"); // Into the message body
        gmd.WaitForStable();
        gmd.Send("Tab"); // And the file list, on its first file
        gmd.WaitForStable();
        gmd.Send("Space");
        dialog = gmd.WaitFor("Commit 1 of 2 changes on 'main'");
        StringAssert.Contains(dialog, "□ M alpha.txt");
        gmd.Send("M-o");

        gmd.WaitFor("Committed to 'main'");
        Assert.AreEqual("epsilon.txt", (await repo.GitAsync("show --name-only --format= HEAD")).Trim());
        Assert.AreEqual(" M alpha.txt", (await repo.GitAsync("status --porcelain")).TrimEnd(), "Left as it was");
    }

    // Escape cancels the dialog, and cancelling has to leave the repository alone. Note that the
    // same key one view further out asks to quit gmd, so this also pins that the dialog swallows it.
    [TestMethod]
    public async Task TestCancelCommitLeavesTheRepoUnchanged()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.Send("Escape");

        StringAssert.Contains(gmd.WaitUntilGone("Commit 2 changes"), "©2 uncommitted changes");
        Assert.IsTrue(gmd.IsRunning, "Escape in the commit dialog should close it, not quit gmd");
        Assert.AreEqual(" M alpha.txt\n?? epsilon.txt", await repo.GitAsync("status -s"));
        Assert.AreEqual("Add delta", await repo.GitAsync("log --format=%s -1"), "Nothing should be committed");
    }

    // A binary file in the changes is asked about before the commit dialog opens, and Enter on that
    // question used to be Undo, which reverts a changed binary file and deletes a new one, with no
    // way back. Cancel is the default now: it changes nothing, and 'c' again is a key away.
    [TestMethod]
    public async Task TestEnterOnTheBinaryFilesQuestionChangesNothing()
    {
        using var repo = await E2eRepo.CreateAsync();
        repo.WriteFile("image.bin", "binary\0data");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("©1 uncommitted change");
        gmd.Send("c");
        gmd.WaitFor("Binary Files Detected");

        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("Binary Files Detected"), "©1 uncommitted change");
        Assert.IsFalse(gmd.Capture().Contains("Commit 1 change"), "Cancel should not go on to commit");
        Assert.AreEqual("?? image.bin", await repo.GitAsync("status -s"));
        Assert.AreEqual("binary\0data", File.ReadAllText(Path.Join(repo.Path, "image.bin")));
    }

    // The dialog's one validation rule, which is the difference between a rejected commit and a
    // commit with an empty message
    [TestMethod]
    public async Task TestCommitWithAnEmptyMessageIsRejected()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.Send("Enter");

        // Drawn inside the commit dialog, which is still open behind it
        var screen = gmd.WaitFor("Empty commit message");
        Assert.AreEqual(
            """
                                   ││                    ╭ Error ! ───────────────────╮                    ││
                                   ││                    │Empty commit message        │                    ││
                                   ││                    │                            │                    ││
                                   ││                    │          [◦ OK ◦]          │                    ││
                                   ││                    ╰────────────────────────────╯                    ││
            """,
            ScreenText.Rows(screen, repo.Path, 17, 5)
        );

        Assert.AreEqual("Add delta", await repo.GitAsync("log --format=%s -1"), "Nothing should be committed");
    }

    // The progress marquee while the commit runs. A push always showed it, and a commit ran under
    // the same progress, but closing the commit dialog in between left the marquee behind the
    // application bar (see Progress.Activated), so a long commit looked like a hung gmd. The commit
    // is slowed down by a pre-commit hook that sleeps, as a hook that formats the code slows a big one.
    // The status line says what is going on meanwhile, and then what was done.
    [TestMethod]
    public async Task TestCommitShowsProgressWhileGitWorks()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        await SlowDownCommitsAsync(repo, seconds: 5);
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");

        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");
        gmd.SendText("Add epsilon");
        gmd.WaitFor("Add epsilon");
        gmd.Send("Enter");

        // The marquee pulses over the start of the repo path on the application bar, so the screen
        // is read the moment it shows rather than once it has settled, which it does not while the
        // marquee moves. The uncommitted row still being there is what says the commit is in
        // flight, i.e. that the marquee is for it and not for the refresh after it.
        var working = gmd.WaitForMoving(IsMarqueeShown, "the progress marquee");
        StringAssert.Contains(working, "uncommitted changes");
        var saying = gmd.WaitForMoving(s => s.Contains("Committing"), "the progress message");
        Assert.AreEqual("Committing to 'main'...", ScreenText.LastLine(saying));
        StringAssert.Contains(saying, "uncommitted changes", "Said while the commit is still in flight");

        // Once the commit is through the marquee is gone and the commit is at the top
        var committed = gmd.WaitUntilGone("uncommitted changes");
        Assert.AreEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add epsilon                                                      (● main) 2d039107 Test User      2024-10-15 12:07
            ┣    Add delta                                                          [v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                              4e73d230 Test User      2024-10-15 12:05
            ┣    Add gamma                                                                 4a15fb13 Test User      2024-10-15 12:04
            ┣╯   Add beta                                                                  dd78912d Test User      2024-10-15 12:01
            ┗    Initial                                                                   9dc406e3 Test User      2024-10-15 12:00
            """,
            ScreenText.Rows(committed, repo.Path, 0, 8)
        );
        Assert.AreEqual(
            "Committed to 'main'",
            ScreenText.LastLine(gmd.WaitFor("Committed to 'main'")),
            "What was done is said"
        );
    }

    // Whether the application bar shows the progress marquee: '[' and ']' with the moving '●'
    // blocks between them, where the repo path otherwise starts
    static bool IsMarqueeShown(string screen) =>
        System.Text.RegularExpressions.Regex.IsMatch(screen.Split('\n')[0], @"^ Gmd \[[ ●]{6}\] ");

    // Makes every commit in the repo take at least this long, by a pre-commit hook that sleeps.
    // The fixture points core.hooksPath at a folder that does not exist, so that the developer's
    // own hooks cannot run; this points it at one inside .git, where 'git add .' cannot pick the
    // hook up as a file to commit.
    static async Task SlowDownCommitsAsync(TempRepo repo, int seconds)
    {
        var hooks = Path.Join(repo.Path, ".git", "slow-hooks");
        Directory.CreateDirectory(hooks);
        var hook = Path.Join(hooks, "pre-commit");
        File.WriteAllText(hook, $"#!/bin/sh\nsleep {seconds}\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await repo.GitAsync($"config core.hooksPath \"{hooks}\"");
    }

    // 'a' amends the last commit, but only while it is still ahead of the remote, i.e. not yet
    // published. That guard is why this is the one flow here that needs a fixture with an origin,
    // and the fixture is also the only place in this suite where the ahead marker, the '(^/main)'
    // remote branch tip and the local branch drawn beside its remote reach a snapshot at all.
    [TestMethod]
    public async Task TestAmendTheLastCommit()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(8));

        // '▲1' in the application bar and '▲' on the commit are the one commit not yet pushed,
        // and '(^/main)' is origin/main, still on the commit below it
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▲1                                           commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Add zeta                                                        (● main) 4dd1e9f8 Test User      2024-10-15 12:07
            ┣╯    Add delta                                                 (^/main)[v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                             4e73d230 Test User      2024-10-15 12:05
            ┣     Add gamma                                                                4a15fb13 Test User      2024-10-15 12:04
            ┣╯    Add beta                                                                 dd78912d Test User      2024-10-15 12:01
            ┗     Initial                                                                  9dc406e3 Test User      2024-10-15 12:00
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );

        gmd.Send("a");
        var dialog = gmd.WaitFor("Amend the last commit");

        // The dialog is the commit one retitled, with the message of the commit being amended
        // filled in and the cursor at its end. '0 changes' because the working tree is clean here:
        // amending only the message is a normal thing to do.
        Assert.AreEqual(
            """
                                   ╭ Amend ─────────────────────────────────────────────────────────────────╮
                                   │ Amend the last commit on 'main':                                       │
                                   │                                                                        │
                                   │[Add zeta                                          ]                    │
            """,
            ScreenText.Rows(dialog, repo.Path, 11, 4)
        );

        gmd.SendText(" amended");
        gmd.WaitFor("Add zeta amended");
        gmd.Send("Enter");

        // Same row, same position, new message and a new id — and the time column does not move,
        // since amending keeps the author date and only the committer date is rewritten
        var committed = gmd.WaitFor("Add zeta amended");
        Assert.AreEqual(
            """
             Gmd {repo}, ●main, ▲1                                           commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Add zeta amended                                                (● main) 9df2d618 Test User      2024-10-15 12:07
            ┣╯    Add delta                                                 (^/main)[v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                             4e73d230 Test User      2024-10-15 12:05
            ┣     Add gamma                                                                4a15fb13 Test User      2024-10-15 12:04
            ┣╯    Add beta                                                                 dd78912d Test User      2024-10-15 12:01
            ┗     Initial                                                                  9dc406e3 Test User      2024-10-15 12:00
            """,
            ScreenText.Rows(committed, repo.Path, 0, 8)
        );
        Assert.AreEqual(
            "Amended the last commit",
            ScreenText.LastLine(gmd.WaitFor("Amended the last commit")),
            "What was done is said"
        );

        // One commit was rewritten rather than added, and it is still the one commit that is ahead
        Assert.AreEqual("Add zeta amended", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("Add delta", await repo.GitAsync("log --format=%s -1 HEAD~1"));
        Assert.AreEqual(
            "2024-10-15 12:07:00 +0000|2024-10-15 12:08:00 +0000",
            await repo.GitAsync("log --format=%ad|%cd --date=iso -1"),
            "Amending should keep the author date and move only the committer date"
        );
        StringAssert.Contains(await repo.GitAsync("status -sb"), "[ahead 1]");
    }

    // Amend with changes in the working tree ticks them all, to amend with, and unticking them all
    // gives the last commit a new message alone. A ticked file was asked for, so the message could
    // not be fixed without taking work in progress into the commit.
    [TestMethod]
    public async Task TestAmendTheMessageAloneWithChangesThere()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        repo.WriteFile("alpha.txt", "alpha\nchanged\n");
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(8));
        gmd.WaitFor("Initial");
        gmd.Send("a");
        gmd.WaitFor("Amend 1 change on 'main'");

        gmd.SendText(" amended");
        gmd.WaitFor("Add zeta amended");
        gmd.Send("Tab"); // Into the message body
        gmd.WaitForStable();
        gmd.Send("Tab"); // And the file list
        gmd.WaitForStable();
        gmd.Send("Space");
        gmd.WaitFor("Amend the last commit on 'main'");
        gmd.Send("M-o");

        gmd.WaitFor("Amended the last commit");
        Assert.AreEqual("Add zeta amended", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual(
            "zeta.txt",
            (await repo.GitAsync("show --name-only --format= HEAD")).Trim(),
            "The files it had"
        );
        Assert.AreEqual(
            " M alpha.txt",
            (await repo.GitAsync("status --porcelain")).TrimEnd(),
            "The change left as it was"
        );
    }

    // The heading names the current branch. It named the branch of the top row, which with no
    // changes is the newest commit shown, here main's, while dev is checked out.
    [TestMethod]
    public async Task TestTheAmendHeadingNamesTheCurrentBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.GitAsync("checkout -q dev");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("More dev work");

        gmd.Send("a");

        StringAssert.Contains(gmd.WaitFor("Amend the last commit"), "Amend the last commit on 'dev':");
    }

    // A pushed commit is amended too, and then replaced on origin by the force push 'p' offers. On a
    // branch others build on, here main, that is asked first, with No the default, and No leaves it.
    [TestMethod]
    public async Task TestAmendOfAPushedCommitOnMainIsAskedFirst()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("push -q origin main"); // Nothing is ahead any more
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(8));
        gmd.WaitFor("Initial");

        gmd.Send("a");

        StringAssert.Contains(
            gmd.WaitFor("Rewrite Pushed Commits"),
            "'main' is the main branch, which others build on."
        );
        gmd.Send("Enter"); // No, the default
        gmd.WaitUntilGone("Rewrite Pushed Commits");
        Assert.AreEqual("Add zeta", await repo.GitAsync("log --format=%s -1"), "Nothing should be rewritten");
    }

    // On a branch of one's own nothing is asked: the dialog opens with its box to push with force when
    // done ticked, and the amend is pushed at once, replacing the old commit on origin
    [TestMethod]
    public async Task TestAmendAPushedCommitOfABranchOfOnesOwnAndForcePushIt()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("push -q origin main");
        await repo.GitAsync("checkout -q -b feature");
        await repo.CommitFileAtAsync("feature.txt", "feature\n", "Add feature", TempRepo.BaseTime.AddMinutes(8));
        await repo.GitAsync("push -q --set-upstream origin feature");
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(9));
        gmd.WaitFor("Add feature");

        gmd.Send("a");
        StringAssert.Contains(
            gmd.WaitFor("Amend the last commit on 'feature'"),
            "Push to 'origin/feature' with force when done"
        );
        gmd.SendText(", reworded");
        gmd.WaitFor("Add feature, reworded");
        gmd.Send("M-o");

        Assert.AreEqual(
            "Amended the last commit, and pushed 'feature' with force",
            ScreenText.LastLine(gmd.WaitFor("Amended the last commit"))
        );
        Assert.AreEqual(
            "Add feature, reworded",
            (await repo.GitAsync($"-C \"{repo.Path}-origin\" log --format=%s -1 feature")).Trim()
        );
    }

    // A pushed commit of a branch of one's own is dropped as one not pushed is, and its question has
    // Drop and Push too, which replaces the branch on origin, Cancel still the default. The menu opens
    // on 'Amend ...', the last commit's, and Undo is four moves down, past this commit's own Amend,
    // 'Commit Diff' and 'Mark for Diff'; in it Drop is four past 'Recover Lost Commits ...', where it
    // opens, the last change, a commit on origin, being no change to undo.
    [TestMethod]
    public async Task TestDropAPushedCommitAndPushIt()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("push -q origin main");
        await repo.GitAsync("checkout -q -b feature");
        await repo.CommitFileAtAsync("one.txt", "one\n", "Add one", TempRepo.BaseTime.AddMinutes(8));
        await repo.CommitFileAtAsync("two.txt", "two\n", "Add two", TempRepo.BaseTime.AddMinutes(9));
        await repo.GitAsync("push -q --set-upstream origin feature");
        var sid = (await repo.GitAsync("rev-parse HEAD~1")).Trim()[..8];
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(10));
        gmd.WaitFor("Add one");
        gmd.Send("Down");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor($"Amend {sid} ...");
        OpenSubMenu(gmd, 4, "Recover Lost Commits");
        for (int i = 0; i < 4; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        StringAssert.Contains(
            gmd.WaitFor("Drop the commit from 'feature'?"),
            "It is on origin: Drop and Push replaces it there too"
        );
        gmd.Send("Left"); // From Cancel, the default, past Drop to Drop and Push
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual(
            "Dropped 'Add one', and pushed 'feature' with force",
            ScreenText.LastLine(gmd.WaitFor("Dropped 'Add one'"))
        );
        Assert.AreEqual(
            "Add two\nAdd zeta",
            (await repo.GitAsync($"-C \"{repo.Path}-origin\" log --format=%s -2 feature")).Trim()
        );
    }

    // A commit moved past the next one up the branch, from the Reorder sub menu, whose items name the
    // commit it is moved past, and Undo's last change. Reorder is nine moves from 'Amend ...', where
    // the menu opens, past this commit's own Amend, 'Commit Diff', 'Mark for Diff', 'Undo', 'Stash',
    // 'Tag', 'Create Branch from Commit ...' and 'Move to New Branch ...'.
    [TestMethod]
    public async Task TestMoveACommitAboveTheNextOne()
    {
        using var repo = await ThreeFilesAsync();
        var sid = (await repo.GitAsync("rev-parse HEAD~1")).Trim()[..8];
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(3));
        gmd.WaitFor("Add one");
        gmd.Send("Down");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor($"Amend {sid} ...");
        OpenSubMenu(gmd, 9, "Move Above 'Add three'");
        StringAssert.Contains(gmd.WaitForStable(), "Move Below 'Add one'");
        gmd.Send("Enter");

        Assert.AreEqual(
            "Moved 'Add two' above 'Add three': Undo takes it back",
            ScreenText.LastLine(gmd.WaitFor("Moved 'Add two'"))
        );
        Assert.AreEqual("Add two\nAdd three\nAdd one", (await repo.GitAsync("log --format=%s")).Trim());
    }

    // Commits of another branch selected with Shift-Down, here main's two, are picked as they were
    // made, with no dialog, and the item counts them. Main has 'Add eta' on top of 'Add gamma', so the
    // rows are eta, gamma and dev's 'Work on dev', where the cursor starts: two up, and two Shift-Down,
    // the first of which starts the selection on eta. The item is eight moves down, as for one commit.
    [TestMethod]
    public async Task TestCherryPickTwoCommitsFromAnotherBranch()
    {
        using var repo = await E2eRepo.CreateWithUnmergedBranchAsync();
        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAtAsync("eta.txt", "eta\n", "Add eta", TempRepo.BaseTime.AddMinutes(3));
        await repo.GitAsync("checkout -q dev");
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(4));
        gmd.WaitFor("Work on dev");

        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("m");
        gmd.WaitFor("Cherry Pick 2 Commits into dev");
        for (var i = 0; i < 8; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        Assert.AreEqual(
            "Cherry picked 2 commits into 'dev': Undo takes them back",
            ScreenText.LastLine(gmd.WaitFor("Cherry picked 2 commits"))
        );
        Assert.AreEqual("Add eta\nAdd gamma\nWork on dev", (await repo.GitAsync("log --format=%s -3 dev")).Trim());
    }

    // The current branch reset to an older commit, keeping what the commits after it changed as
    // uncommitted changes, after a question with Cancel the default. In the Undo sub menu of 'Add
    // two' the item is eight moves from 'Undo Commit', where it opens, past 'Recover Lost Commits',
    // 'Restore Deleted Branch', 'Restore Discarded Changes', 'Revert Commit', 'Drop', 'Uncommit Last
    // Commit' and 'Uncommit ... and Newer'.
    [TestMethod]
    public async Task TestResetTheBranchToACommitKeepingTheChanges()
    {
        using var repo = await ThreeFilesAsync();
        var sid = (await repo.GitAsync("rev-parse HEAD~1")).Trim()[..8];
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add one");
        gmd.Send("Down");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor($"Amend {sid} ...");
        OpenSubMenu(gmd, 4, "Recover Lost Commits");
        for (int i = 0; i < 8; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitFor($"Reset 'main' to {sid}?"), "The commit after it leaves the branch.");
        gmd.Send("Left"); // From Cancel, the default, past Discard Changes to Keep Changes
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual(
            $"Reset 'main' to {sid}, keeping the changes uncommitted: Undo takes it back",
            ScreenText.LastLine(gmd.WaitFor($"Reset 'main' to {sid},"))
        );
        Assert.AreEqual("Add two\nAdd one", (await repo.GitAsync("log --format=%s")).Trim());
        Assert.AreEqual("?? three.txt", (await repo.GitAsync("status --porcelain")).Trim());
    }

    // Uncommitting the last commit, i.e. 'git reset HEAD~1', which puts its changes back into the
    // working tree. Reached through the commit menu's Undo sub menu.
    //
    // On a clean tree the menu opens with the cursor on 'Amend ...' — 'Commit ...' is disabled, and
    // Menu.Show starts on the first item that is not — so 'Undo' is three moves away, past 'Commit
    // Diff' and 'Mark for Diff'. In it the cursor starts on 'Undo Commit', the last change of the branch, and 'Discard
    // Changes in a File' is disabled, and so is 'Drop 17d85ba8', since the tag v1.0 is on the commit,
    // so 'Uncommit Last Commit' is five moves down, past 'Recover Lost Commits ...', 'Restore Deleted
    // Branch ...', 'Restore Discarded Changes ...' and 'Revert Commit'.
    [TestMethod]
    public async Task TestUncommitTheLastCommit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Uncommit");
        for (var i = 0; i < 5; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        // The commit is gone and what it added is back in the working tree as an untracked file
        gmd.WaitFor("uncommitted");
        Assert.AreEqual("Merge branch 'dev' into main", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("?? delta.txt", await repo.GitAsync("status --porcelain"));
    }

    // Undo of the branch's last change, named after it, and Undo again, which redoes it. The undo of
    // a commit leaves its changes in the working tree, as Uncommit does; the redo commits them back.
    // 'Undo' is three moves from 'Amend ...', where the menu opens, past 'Commit Diff' and 'Mark for
    // Diff'. The second time, on the uncommitted row, 'Commit ...' is enabled and 'Mark for Diff' is
    // not, so it is three again.
    [TestMethod]
    public async Task TestUndoTheLastCommitAndRedoIt()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Undo Commit 'Add delta'");
        gmd.Send("Enter");

        var screen = gmd.WaitFor("Undid the commit 'Add delta' on 'main'");
        Assert.AreEqual("Undid the commit 'Add delta' on 'main': Undo again redoes it", ScreenText.LastLine(screen));
        Assert.AreEqual("Merge branch 'dev' into main", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("?? delta.txt", (await repo.GitAsync("status --porcelain")).TrimEnd());

        gmd.WaitFor("uncommitted");
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Redo Commit 'Add delta'");
        gmd.Send("Enter");

        gmd.WaitFor("Redid the commit 'Add delta' on 'main'");
        Assert.AreEqual("Add delta", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
    }

    // An older commit not pushed yet is amended from its own commit menu, in the commit dialog with its
    // message, and the commits after it are replayed on it. The menu opens on 'Amend ...', which is
    // the 'a' key's and amends the last commit, with this commit's own right below it.
    [TestMethod]
    public async Task TestAmendAnOlderCommit()
    {
        using var repo = await ThreeFilesAsync();
        var sid = (await repo.GitAsync("rev-parse HEAD~1")).Trim()[..8];
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(3));
        gmd.WaitFor("Add one");
        gmd.Send("Down");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor($"Amend {sid} ...");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitFor($"Amend {sid} on 'main':"), "[Add two");
        gmd.SendText(", reworded");
        gmd.WaitFor("Add two, reworded");
        gmd.Send("Enter");

        var screen = gmd.WaitFor("Amended 'Add two, reworded'");
        Assert.AreEqual("Amended 'Add two, reworded' on 'main': Undo takes it back", ScreenText.LastLine(screen));
        Assert.AreEqual("Add three\nAdd two, reworded\nAdd one", (await repo.GitAsync("log --format=%s")).Trim());
    }

    // Dropping a commit asks first, with No the default, then takes it and its changes out of the
    // branch, and it is Undo's last change, which brings it back. Undo is four moves from where the
    // menu opens, past this commit's own Amend, 'Commit Diff' and 'Mark for Diff'; Drop is five into it, past 'Recover
    // Lost Commits ...', 'Restore Deleted Branch ...', 'Restore Discarded Changes ...' and 'Revert Commit'.
    [TestMethod]
    public async Task TestDropACommitAndUndoIt()
    {
        using var repo = await ThreeFilesAsync();
        var sid = (await repo.GitAsync("rev-parse HEAD~1")).Trim()[..8];
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(3));
        gmd.WaitFor("Add one");
        gmd.Send("Down");
        gmd.WaitForStable();

        void PickDrop()
        {
            gmd.Send("m");
            gmd.WaitFor($"Amend {sid} ...");
            OpenSubMenu(gmd, 4, "Recover Lost Commits");
            for (int i = 0; i < 5; i++)
            {
                gmd.Send("Down");
                gmd.WaitForStable();
            }
            gmd.Send("Enter");
            gmd.WaitFor("Drop the commit from 'main'?");
        }

        PickDrop();
        gmd.Send("Enter");
        gmd.WaitUntilGone("Drop the commit");
        Assert.AreEqual("Add three\nAdd two\nAdd one", (await repo.GitAsync("log --format=%s")).Trim(), "Not dropped");

        PickDrop();
        gmd.Send("Left"); // From No, the default, to Yes
        gmd.WaitForStable();
        gmd.Send("Enter");

        var screen = gmd.WaitFor("Dropped 'Add two'");
        Assert.AreEqual("Dropped 'Add two' from 'main': Undo brings it back", ScreenText.LastLine(screen));
        Assert.AreEqual("Add three\nAdd one", (await repo.GitAsync("log --format=%s")).Trim());
        Assert.IsFalse(File.Exists(Path.Join(repo.Path, "two.txt")));

        // From the top row, the last commit, which has no Amend of its own besides 'Amend ...'
        gmd.Send("Home");
        gmd.WaitForStable();
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        OpenSubMenu(gmd, 3, "Undo Drop 'Add two'");
        gmd.Send("Enter");

        gmd.WaitFor("Undid the drop of 'Add two' on 'main'");
        Assert.AreEqual("Add three\nAdd two\nAdd one", (await repo.GitAsync("log --format=%s")).Trim());
        Assert.IsTrue(File.Exists(Path.Join(repo.Path, "two.txt")));
    }

    // Three commits of a file each, none pushed, so that one can be amended or dropped without the
    // ones after it conflicting
    static async Task<TempRepo> ThreeFilesAsync()
    {
        var repo = await TempRepo.CreateAsync();
        string[] names = ["one", "two", "three"];
        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i];
            await repo.CommitFileAtAsync($"{name}.txt", $"{name}\n", $"Add {name}", TempRepo.BaseTime.AddMinutes(i));
        }
        return repo;
    }

    // Squashing a range of commits into one. The range is a shift-selection of two rows, which is
    // what puts the ids into the menu item's own text and is what enables it at all. The commits were
    // pushed, so origin still has the two the squash replaced: that is said, they are drawn dark, with
    // nothing to pull, and 'p' pushes the squash with force once asked, with Cancel as the default.
    [TestMethod]
    public async Task TestSquashTwoPushedCommitsIntoOneAndForcePushIt()
    {
        using var repo = await E2eRepo.CreatePushedPlainCommitsAsync(4);
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(4));
        gmd.WaitFor("Commit number 00");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();

        // The menu opens on Amend, the first item it enables, the last commit being one to amend
        // though pushed, as is the row's, Amend 8332dd93; Squash below them names the range it would
        // squash, which is how it says a selection was picked up
        gmd.Send("m");
        gmd.WaitFor("Squash 2 Commits ...");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        // On main, which others build on, rewriting its commits on origin is asked first
        StringAssert.Contains(
            gmd.WaitFor("Rewrite Pushed Commits"),
            "'main' is the main branch, which others build on."
        );
        gmd.Send("Left"); // From No, the default, to Yes
        gmd.WaitForStable();
        gmd.Send("Enter");

        gmd.WaitFor("Squash 2 commits on 'main', 8332dd93 to c02addfd:");
        gmd.Send("Enter");

        // The two are now one, and the originals are still the remote's: the old version, not
        // commits to pull, which a pull would put back beside the squash
        var screen = gmd.WaitFor("was rewritten here");
        Assert.AreEqual(
            """
             Gmd {repo}, ●main, ▲1                                           commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Commit number 02                                                (● main) f5e5fdfb Test User      2024-10-15 12:04
            ┣│    Commit number 03                                                (^/main) c02addfd Test User      2024-10-15 12:03
            ┣│    Commit number 02                                                         8332dd93 Test User      2024-10-15 12:02
            ┣╯    Commit number 01                                                         5692a80d Test User      2024-10-15 12:01
            ┗     Commit number 00                                                         a823b76e Test User      2024-10-15 12:00
            """,
            ScreenText.Rows(screen, repo.Path, 0, 7)
        );
        Assert.AreEqual(
            "'main' was rewritten here by the squash: p pushes it with force, over origin's old version",
            ScreenText.LastLine(screen)
        );
        Assert.AreEqual(
            """
            Commit number 02
            Commit number 01
            Commit number 00
            """,
            await repo.GitAsync("log --format=%s"),
            "The top two commits are one, keeping the older of the two messages"
        );
        Assert.AreEqual("Commit number 02\n\n* Commit number 03", (await repo.GitAsync("log --format=%B -1")).Trim());

        gmd.Send("p");
        StringAssert.Contains(gmd.WaitFor("Force Push"), "and origin still has the 2 commits it replaced:");
        gmd.Send("Left"); // From Cancel, the default, to Force Push
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual(
            "Pushed 'main' with force, replacing the old version on origin",
            ScreenText.LastLine(gmd.WaitFor("Pushed 'main' with force"))
        );
        Assert.AreEqual(
            await repo.GitAsync("rev-parse main"),
            await repo.GitAsync($"-C \"{repo.Path}-origin\" rev-parse main")
        );
    }

    // Regression test: squashing commits that have not been pushed. This used to be refused, with
    // "Commits not on current branch", because the guard asked for 'IsLocalCurrent' alone — a flag
    // only ever set on a *remote* branch whose local branch is current (Augmenter.cs:63). A commit
    // that has not been pushed belongs to the local branch, which never carries it.
    //
    // Which was the wrong way round: the commits it did allow were the ones already published.
    [TestMethod]
    public async Task TestSquashCommitsThatHaveNotBeenPushed()
    {
        using var repo = await E2eRepo.CreateLongAsync(4);
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(4));
        gmd.WaitFor("Commit number 00");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();

        // The menu opens on 'Amend ...', the last commit not being pushed, and Squash is two below it,
        // past the amend of the commit the cursor is on
        gmd.Send("m");
        gmd.WaitFor("Amend 8332dd93 ...");
        for (var i = 0; i < 2; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        gmd.WaitFor("Squash 2 commits on 'main', 8332dd93 to c02addfd:");
        gmd.Send("Enter");

        // With no remote there is nothing left behind, so the two rows simply become one
        gmd.WaitUntilGone("Commit number 03");
        Assert.AreEqual(
            """
            Commit number 02
            Commit number 01
            Commit number 00
            """,
            await repo.GitAsync("log --format=%s")
        );
    }

    // Cherry-picking a commit from another branch onto the current one. It runs with --no-commit
    // and hands what it staged to the commit dialog, which is why the dialog opens with the picked
    // commit's message already in it.
    //
    // 'Up' first: the cursor opens on the current branch's tip, and cherry-pick is only offered for
    // a commit that is not on the current branch (rb != cb). One move up is 'Add gamma' on main,
    // and it is a move that lands there whether the cursor started on row 0 or row 1.
    //
    // Then eight moves down to it. The menu opens on 'Amend ...', of dev's last commit, which is not
    // pushed — with nothing to commit, 'Commit ...' is disabled and Menu.Show starts on the first
    // that is not — and the amend of 'Add gamma', which is not on dev, and 'Squash ...', with nothing
    // selected, are disabled and skipped.
    [TestMethod]
    public async Task TestCherryPickACommitFromAnotherBranch()
    {
        using var repo = await E2eRepo.CreateWithUnmergedBranchAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(3));
        gmd.WaitFor("Work on dev");

        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("m");
        gmd.WaitFor("Cherry Pick into dev");
        for (var i = 0; i < 8; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        // The dialog arrives filled in with the message of the commit being picked
        gmd.WaitFor("Add gamma, 1 uncommitted change");
        gmd.Send("Enter");

        // 'dev' now has its own copy of the commit, with an id of its own, and main still has the
        // original — the same subject on two branches is what a cherry-pick looks like
        var committed = gmd.WaitUntilGone("uncommitted");
        Assert.AreEqual(
            """
             Gmd {repo}, ●dev                                                 commit on dev [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
              ┣ ● Add gamma                                                        (● dev) b03776a3 Test User      2024-10-15 12:03
            ┣ ┃   Add gamma                                                         (main) de2e9a57 Test User      2024-10-15 12:02
            ┃╭┺   Work on dev                                                              ee36025f Test User      2024-10-15 12:01
            ┗╯    Initial                                                                  9dc406e3 Test User      2024-10-15 12:00
            """,
            ScreenText.Rows(committed, repo.Path, 0, 6)
        );
        Assert.AreEqual(
            "Committed to 'dev'",
            ScreenText.LastLine(gmd.WaitFor("Committed to 'dev'")),
            "What was done is said"
        );

        Assert.AreEqual(
            """
            Add gamma
            Work on dev
            Initial
            """,
            await repo.GitAsync("log --format=%s")
        );
    }

    // Stashing, i.e. the menu, the dialog behind it and what the log view says afterwards. The
    // 'ß' is drawn nowhere else, so this is the only cover WriteBlankOrStash has at any tier.
    //
    // Four moves down to 'Stash' rather than six: 'Squash ...' is disabled with nothing selected,
    // and so is 'Mark for Diff' on the uncommitted row, and OnCursorDown skips them. With a clean
    // tree it is four too, from 'Amend ...', 'Commit ...' being disabled and 'Mark for Diff' not —
    // see TestStashPopBringsTheChangesBack.
    [TestMethod]
    public async Task TestStashPutsTheChangesAsideAndMarksTheCommit()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (int i = 0; i < 4; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Stash Changes ...");
        gmd.Send("Enter");
        gmd.WaitFor("Stash Message");
        gmd.SendText("stashed work");
        gmd.Send("Enter");

        // The uncommitted row is gone, the tree is clean, and the commit it was stashed on carries
        // the 'ß'. The application bar counts it too, where the change count used to be.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ß1                                           commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣ ß● Add delta                                                  (● main)[v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                              4e73d230 Test User      2024-10-15 12:05
            ┣    Add gamma                                                                 4a15fb13 Test User      2024-10-15 12:04
            ┣╯   Add beta                                                                  dd78912d Test User      2024-10-15 12:01
            ┗    Initial                                                                   9dc406e3 Test User      2024-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted"),
            repo.Path
        );

        Assert.AreEqual("stash@{0}: On main: stashed work", await repo.GitAsync("stash list"));
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"), "The working tree is clean again");
    }

    // And back again. Four moves, from 'Amend ...', where the menu opens on a clean tree. A clean
    // tree disables 'Stash Changes' too, which is why 'Stash Pop' is where the cursor lands when
    // the sub menu opens.
    [TestMethod]
    public async Task TestStashPopBringsTheChangesBack()
    {
        using var repo = await E2eRepo.CreateWithStashAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (int i = 0; i < 4; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Stash Pop");
        gmd.Send("Right");
        gmd.WaitFor("stashed work");
        gmd.Send("Enter");

        gmd.WaitFor("uncommitted");
        Assert.AreEqual("", await repo.GitAsync("stash list"), "The stash is gone once it is popped");
        Assert.AreEqual(
            """
             M alpha.txt
            ?? epsilon.txt
            """,
            await repo.GitAsync("status --porcelain"),
            "Both the modified file and the untracked one come back"
        );
    }

    // Applying a stash is popping it but keeping it, which is said on the status line, since nothing
    // else on the screen shows that it is still there. Stash Apply is right below Stash Pop, which is
    // where the cursor lands, as in the test above.
    [TestMethod]
    public async Task TestStashApplyKeepsTheStash()
    {
        using var repo = await E2eRepo.CreateWithStashAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenCommitSubMenu(gmd, 4, "Stash Pop");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        gmd.WaitFor("stashed work");
        gmd.Send("Enter");

        var screen = gmd.WaitFor("Applied 'stashed work'");
        Assert.AreEqual(
            "Applied 'stashed work' and kept the stash: Stash Drop removes it",
            ScreenText.LastLine(screen)
        );
        Assert.AreEqual("stash@{0}: On main: stashed work", await repo.GitAsync("stash list"), "Still there");
        Assert.AreEqual(
            """
             M alpha.txt
            ?? epsilon.txt
            """,
            await repo.GitAsync("status --porcelain"),
            "The changes are back, as with a pop"
        );
    }

    // Dropping a stash asks first, with No the default: its changes exist nowhere else. Stash Drop is
    // three below Stash Pop, which is where the cursor lands, as in the test above, past Stash Apply
    // and Stash Diff.
    [TestMethod]
    public async Task TestStashDropAsksFirst()
    {
        using var repo = await E2eRepo.CreateWithStashAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenCommitSubMenu(gmd, 4, "Stash Pop");
        for (int i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("stashed work");
        gmd.Send("Enter");
        gmd.WaitFor("Drop the stash?");

        gmd.Send("Enter");

        gmd.WaitUntilGone("Drop the stash?");
        Assert.AreEqual("stash@{0}: On main: stashed work", await repo.GitAsync("stash list"), "Still there");
    }

    // Removing a tag asks first, with No the default, and says when it goes from origin too
    [TestMethod]
    public async Task TestRemoveTagAsksFirst()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenCommitSubMenu(gmd, 5, "Add Tag");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        gmd.WaitFor("v1.0");
        gmd.Send("Enter");
        gmd.WaitFor("Remove the tag 'v1.0'?");

        gmd.Send("Enter");

        gmd.WaitUntilGone("Remove the tag");
        Assert.AreEqual("v1.0", await repo.GitAsync("tag"), "Still there");
    }

    // Discarding all changes and the ignored files makes the folder as a fresh clone has it, which
    // also deletes what git ignores on purpose, such as a .env file of secrets. So the question
    // lists what would be deleted, git's dry run of the clean, and No is the default.
    [TestMethod]
    public async Task TestDiscardingTheIgnoredFilesListsThemAndAsksFirst()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.CommitFileAtAsync(".gitignore", ".env\nbin/\n", "Ignore secrets", TempRepo.BaseTime.AddMinutes(7));
        File.WriteAllText(Path.Join(repo.Path, ".env"), "TOKEN=secret\n");
        Directory.CreateDirectory(Path.Join(repo.Path, "bin"));
        File.WriteAllText(Path.Join(repo.Path, "bin", "out.dll"), "x");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Ignore secrets");

        OpenCommitSubMenu(gmd, 3, "Recover Lost Commits");
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Enter");

        var question = gmd.WaitFor("as a fresh clone");
        StringAssert.Contains(question, "Discard All Changes and Ignored Files");
        StringAssert.Contains(question, "  .env");
        StringAssert.Contains(question, "  bin/");

        gmd.Send("Enter");

        gmd.WaitUntilGone("as a fresh clone");
        Assert.IsTrue(File.Exists(Path.Join(repo.Path, ".env")), "No is the default, so nothing is deleted");
    }

    // Opens the commit menu of the current row and the sub menu 'moves' down from the first
    // enabled item, one key per Send since a menu redraw drops what was sent behind it
    static void OpenCommitSubMenu(TmuxSession gmd, int moves, string firstItem)
    {
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        OpenSubMenu(gmd, moves, firstItem);
    }

    // The sub menu that many moves down the open menu
    static void OpenSubMenu(TmuxSession gmd, int moves, string firstItem)
    {
        for (int i = 0; i < moves; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor(firstItem);
    }
}
