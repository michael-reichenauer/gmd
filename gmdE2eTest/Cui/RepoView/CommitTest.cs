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
             Gmd {repo}, ●main, ©2                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ©2 uncommitted changes                                                (● main)                       NN-NN-NN NN:NN
            ┣  ● Add delta                                                              [v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            ScreenText.MaskTimes(ScreenText.Of(gmd.WaitFor("Initial"), repo.Path), "uncommitted")
        );
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
            ScreenText.Rows(dialog, repo.Path, 11, 6)
        );

        // The subject field has the focus, so the message is simply typed, and Enter presses the
        // default OK button
        gmd.SendText("Add epsilon");
        gmd.WaitFor("Add epsilon");
        gmd.Send("Enter");

        // The uncommitted row is gone, the new commit is at the top with the branch tip on it, and
        // its sid and time are the pinned ones — not masked, because the session pinned the dates
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add epsilon                                                          (● main) 2d0391 Test User      24-10-15 12:07
            ┣    Add delta                                                              [v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted changes"),
            repo.Path
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
        gmd.Send("Tab"); // And on to the OK button, since Enter in the body is a newline
        gmd.WaitForStable();
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("uncommitted changes"), "Add epsilon");
        Assert.AreEqual("Add epsilon\n\nSome body text", await repo.GitAsync("log --format=%B -1"));
        // The log view has no caret, so the dialog's must not outlive it
        Assert.IsFalse(gmd.IsCursorVisible, "The caret should be hidden again once the dialog has closed");
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
        gmd.WaitFor("©1 uncommitted changes");
        gmd.Send("c");
        gmd.WaitFor("Binary Files Detected");

        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("Binary Files Detected"), "©1 uncommitted changes");
        Assert.IsFalse(gmd.Capture().Contains("Commit 1 changes"), "Cancel should not go on to commit");
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
    // is slowed down by a pre-commit hook that sleeps, which is what a big commit does to git.
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

        // Once the commit is through the marquee is gone and the commit is at the top
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add epsilon                                                          (● main) 2d0391 Test User      24-10-15 12:07
            ┣    Add delta                                                              [v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted changes"),
            repo.Path
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
             Gmd {repo}, ●main, ▲1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Add zeta                                                            (● main) 4dd1e9 Test User      24-10-15 12:07
            ┣╯    Add delta                                                     (^/main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );

        gmd.Send("a");
        var dialog = gmd.WaitFor("Amend 0 changes");

        // The dialog is the commit one retitled, with the message of the commit being amended
        // filled in and the cursor at its end. '0 changes' because the working tree is clean here:
        // amending only the message is a normal thing to do.
        Assert.AreEqual(
            """
                                   ╭ Amend ─────────────────────────────────────────────────────────────────╮
                                   │ Amend 0 changes on 'main':                                             │
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
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▲1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Add zeta amended                                                    (● main) 9df2d6 Test User      24-10-15 12:07
            ┣╯    Add delta                                                     (^/main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("Add zeta amended"),
            repo.Path
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

    // The guard that makes amend safe: once the commit is on the remote it is not offered at all,
    // and the key silently does nothing rather than rewriting published history.
    [TestMethod]
    public async Task TestAmendIsRefusedForAPushedCommit()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("push -q origin main"); // Nothing is ahead any more
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(8));
        gmd.WaitFor("Initial");

        gmd.Send("a");

        // No dialog opens. The '▲' ahead markers are gone now that everything is pushed, and the
        // remote is drawn as its own '(^)' tip beside the local one on the same commit.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣─┺ ● Add zeta                                                         (^)(● main) 4dd1e9 Test User      24-10-15 12:07
            ┣     Add delta                                                             [v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitForStable(),
            repo.Path
        );

        Assert.AreEqual("Add zeta", await repo.GitAsync("log --format=%s -1"), "Nothing should be rewritten");
    }

    // Uncommitting the last commit, i.e. 'git reset HEAD~1', which puts its changes back into the
    // working tree. Reached through the commit menu's Undo sub menu.
    //
    // On a clean tree the menu opens with the cursor already on 'Commit Diff ...' — 'Commit ...'
    // and 'Amend ...' are both disabled, and Menu.Show starts on the first item that is not — so
    // 'Undo' is one move away rather than three.
    [TestMethod]
    public async Task TestUncommitTheLastCommit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        gmd.WaitFor("Uncommit");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        // The commit is gone and what it added is back in the working tree as an untracked file
        gmd.WaitFor("uncommitted");
        Assert.AreEqual("Merge branch 'dev' into main", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("?? delta.txt", await repo.GitAsync("status --porcelain"));
    }

    // Squashing a range of commits into one. The range is a shift-selection of two rows, which is
    // what puts the ids into the menu item's own text and is what enables it at all.
    [TestMethod]
    public async Task TestSquashTwoCommitsIntoOne()
    {
        using var repo = await E2eRepo.CreatePushedPlainCommitsAsync(4);
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(4));
        gmd.WaitFor("Commit number 00");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Commit Diff ...");
        for (var i = 0; i < 2; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");

        // The item names the range it would squash, which is how it says a selection was picked up
        gmd.WaitFor("Squash c02add...8332dd");
        gmd.Send("Enter");

        gmd.WaitFor("Squash c02add...8332dd on 'main'");
        gmd.Send("Enter");

        // The two are now one, and the branch has diverged from its remote: one commit ahead of
        // origin and two behind it, since the originals are still the remote's. Which is the
        // clearest possible illustration of why squashing pushed commits is the wrong way round.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▼2, ▲1                                               (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Commit number 02                                                    (● main) 96a909 Test User      24-10-15 12:04
            ┣│   ▼Commit number 03                                                    (^/main) c02add Test User      24-10-15 12:03
            ┣│   ▼Commit number 02                                                             8332dd Test User      24-10-15 12:02
            ┣╯    Commit number 01                                                             5692a8 Test User      24-10-15 12:01
            ┗     Commit number 00                                                             a823b7 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("▲"),
            repo.Path
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

        gmd.Send("m");
        gmd.WaitFor("Commit Diff ...");
        for (var i = 0; i < 2; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Squash c02add...8332dd");
        gmd.Send("Enter");

        gmd.WaitFor("Squash c02add...8332dd on 'main'");
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
    // Then seven moves down to it. The menu opens on 'Commit Diff ...' — with nothing to commit,
    // 'Commit ...' and 'Amend ...' are both disabled and Menu.Show starts on the first that is not.
    [TestMethod]
    public async Task TestCherryPickACommitFromAnotherBranch()
    {
        using var repo = await E2eRepo.CreateWithUnmergedBranchAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(3));
        gmd.WaitFor("Work on dev");

        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("m");
        gmd.WaitFor("Cherry Pick Commit to dev");
        for (var i = 0; i < 7; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        // The dialog arrives filled in with the message of the commit being picked
        gmd.WaitFor("Add gamma, 1 uncommitted changes");
        gmd.Send("Enter");

        // 'dev' now has its own copy of the commit, with an id of its own, and main still has the
        // original — the same subject on two branches is what a cherry-pick looks like
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●dev                                                         (dev) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
              ┣ ● Add gamma                                                            (● dev) b03776 Test User      24-10-15 12:03
            ┣ ┃   Add gamma                                                             (main) de2e9a Test User      24-10-15 12:02
            ┃╭┺   Work on dev                                                                  ee3602 Test User      24-10-15 12:01
            ┗╯    Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted"),
            repo.Path
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
    // Four moves down to 'Stash' rather than five: 'Amend ...' is disabled without a remote to be
    // ahead of, and OnCursorDown skips it. With a clean tree it is three, since 'Stash Changes'
    // being disabled changes what is enabled above as well — see TestStashPopBringsTheChangesBack.
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
        gmd.WaitFor("Stash Changes");
        gmd.Send("Enter");
        gmd.WaitFor("Stash Message");
        gmd.SendText("stashed work");
        gmd.Send("Enter");

        // The uncommitted row is gone, the tree is clean, and the commit it was stashed on carries
        // the 'ß'. The application bar counts it too, where the change count used to be.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ß1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣ ß● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted"),
            repo.Path
        );

        Assert.AreEqual("stash@{0}: On main: stashed work", await repo.GitAsync("stash list"));
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"), "The working tree is clean again");
    }

    // And back again. Three moves rather than four, since a clean tree disables 'Stash Changes',
    // which is also why 'Stash Pop' is where the cursor lands when the sub menu opens.
    [TestMethod]
    public async Task TestStashPopBringsTheChangesBack()
    {
        using var repo = await E2eRepo.CreateWithStashAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (int i = 0; i < 3; i++)
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
}
