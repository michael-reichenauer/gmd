using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// Pushing and pulling, against a bare 'origin' next door (see E2eRepo.CreateWithOriginAsync and
// the fixtures built on it), which is also what puts the ahead and behind markers on these screens.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class PushPullTest
{
    // Pushing the current branch with 'p'. The fixture has one commit that is not on the remote,
    // so before the push the local and remote branches are on different commits and each names
    // itself — '(^/main)' on the remote's tip and '(● main)' on the local one — and the commit
    // between them carries the bright green '▲'. Afterwards they are back on the same commit and
    // are drawn as the one combined '(^)(● main)' tip.
    //
    // This is the only test here that pushes, so it is also the only cover the ahead markers and
    // the split branch tips have at this tier.
    [TestMethod]
    public async Task TestPushTheCurrentBranch()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add zeta");

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
            gmd.WaitForStable(),
            repo.Path
        );

        gmd.Send("p");

        var screen = gmd.WaitUntilGone("▲");
        Assert.AreEqual(
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
            ScreenText.Rows(screen, repo.Path, 0, 8)
        );
        Assert.AreEqual("Pushed 'main'", ScreenText.LastLine(gmd.WaitFor("Pushed 'main'")), "What was done is said");

        Assert.AreEqual(
            await repo.GitAsync("rev-parse main"),
            await repo.GitAsync("rev-parse origin/main"),
            "The remote branch is on the commit the local one is"
        );

        // v1.0 is still there, and still unpushed: pushing a branch does not push its tags,
        // and the fetch that follows no longer prunes the ones the remote has not got
        Assert.AreEqual("v1.0", await repo.GitAsync("tag --list"));

        // And it was a plain push. 'p' used to force push (with lease) any branch that had a remote,
        // not only after Force Push was chosen in the question a diverged branch gets, see below.
        var log = gmd.WaitForLog("push --porcelain origin --set-upstream refs/heads/main:refs/heads/main");
        Assert.IsFalse(log.Contains("push --force-with-lease"), "'p' should not force push");
    }

    // A click on ▲ or ▼ in the application bar opens a menu, the current branch or all of them. It
    // used to push or pull every shown branch there and then.
    [TestMethod]
    [DataRow(true, "▲1", "Push Current Branch")]
    [DataRow(false, "▼1", "Pull Current Branch")]
    public async Task TestClickingTheAheadOrBehindMarkerOpensAMenu(bool isAhead, string marker, string item)
    {
        using var repo = isAhead ? await E2eRepo.CreateWithOriginAsync() : await E2eRepo.CreateBehindOriginAsync();
        var (localMain, remoteMain) = (
            await repo.GitAsync("rev-parse main"),
            await repo.GitAsync("ls-remote origin main")
        );
        using var gmd = TmuxSession.StartGmd(repo);
        var (x, y) = TmuxSession.PositionOf(gmd.WaitFor(marker), marker);

        gmd.Click(x, y);
        gmd.WaitFor(item);
        gmd.Send("Escape");

        StringAssert.Contains(gmd.WaitUntilGone(item), marker, "Nothing was pushed or pulled");
        Assert.AreEqual(localMain, await repo.GitAsync("rev-parse main"), "main is untouched");
        Assert.AreEqual(remoteMain, await repo.GitAsync("ls-remote origin main"), "and so is origin");
    }

    // A branch whose remote has commits it has not got can only be pushed by force, so 'p' asks,
    // with Cancel the default, and Cancel leaves origin as it was
    [TestMethod]
    public async Task TestPushingADivergedBranchAsksBeforeForcing()
    {
        using var repo = await E2eRepo.CreateWithDivergedMainAsync();
        await repo.GitAsync("checkout -q main");
        var remoteMain = await repo.GitAsync("ls-remote origin main");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("●main");

        gmd.Send("p");
        gmd.WaitFor("Push Warning");
        gmd.Send("Enter");

        gmd.WaitUntilGone("Push Warning");
        Assert.AreEqual(remoteMain, await repo.GitAsync("ls-remote origin main"), "origin is untouched");
    }

    // Uncommitted changes do not stop a push, which sends commits and leaves the changes where they
    // are, and the message says so, since that is easy to assume otherwise
    [TestMethod]
    public async Task TestPushWithUncommittedChanges()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        repo.WriteFile("alpha.txt", "alpha\nchanged\n");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("©1");

        gmd.Send("p");

        Assert.AreEqual(
            "Pushed 'main'; the uncommitted changes stay local",
            ScreenText.LastLine(gmd.WaitFor("Pushed 'main'"))
        );
        Assert.AreEqual(await repo.GitAsync("rev-parse main"), await repo.GitAsync("rev-parse origin/main"));
        Assert.AreEqual(" M alpha.txt", await repo.GitAsync("status -s"), "The change is still there");
    }

    // With a branch highlighted, 'p' pushes that branch, as its branch menu's Push says, rather than
    // the current one. 'work' is current and 'main', a commit ahead of origin, is highlighted.
    [TestMethod]
    public async Task TestPushTheHighlightedBranch()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("checkout -q -b work HEAD~1");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add zeta");
        gmd.Send("Home");
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();

        gmd.Send("p");

        Assert.AreEqual("Pushed 'main'", ScreenText.LastLine(gmd.WaitFor("Pushed 'main'")));
        Assert.AreEqual(await repo.GitAsync("rev-parse main"), await repo.GitAsync("rev-parse origin/main"));
        Assert.AreEqual("work", await repo.GitAsync("rev-parse --abbrev-ref HEAD"), "Still on work");
    }

    // Pulling with 'u', the mirror of the push above: origin has a commit the local branch has
    // not got, so it is drawn bright blue with the '▼' behind marker until it is pulled in.
    [TestMethod]
    public async Task TestPullTheCurrentBranch()
    {
        using var repo = await E2eRepo.CreateBehindOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add zeta");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▼1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣    ▼Add zeta                                                            (^/main) 4dd1e9 Test User      24-10-15 12:07
            ┣─┺ ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitForStable(),
            repo.Path
        );

        gmd.Send("u");

        var screen = gmd.WaitUntilGone("▼");
        Assert.AreEqual(
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
            ScreenText.Rows(screen, repo.Path, 0, 8)
        );
        Assert.AreEqual("Pulled 'main'", ScreenText.LastLine(gmd.WaitFor("Pulled 'main'")), "What was done is said");

        Assert.AreEqual(
            await repo.GitAsync("rev-parse origin/main"),
            await repo.GitAsync("rev-parse main"),
            "The local branch has caught up with the remote"
        );
    }

    // 'Shift-U' updates every branch it can and says which it could not. A branch that is not
    // current is updated with a fetch, and git rejects that for a diverged branch, which used to
    // abort the whole command: every branch after it in the list went unpulled, with an error box
    // as the only sign. Here 'main' is the diverged one and 'dev' the plain fast-forward.
    [TestMethod]
    public async Task TestPullAllBranchesSkipsTheDivergedBranch()
    {
        using var repo = await E2eRepo.CreateWithDivergedMainAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Main local");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●work, ▼2, ▲1                                               (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺      ▲Main local                                                                   (main) 801397 Test User 24-10-15
            ┣│       ▼Main remote                                                                (^/main) 292075 Test User 24-10-15
            ┃│  ┣    ▼Work remote                                                                (^/work) ff355b Test User 24-10-15
            ┃│ ╭┺─┺ ● Work on work                                                               (● work) 1d4a58 Test User 24-10-15
            ┣┴─╯      Add zeta                                                                            4dd1e9 Test User 24-10-15
            ┣         Add delta                                                                    [v1.0] 17d85b Test User 24-10-15
            ┣╮        Merge branch 'dev' into main                                                        4e73d2 Test User 24-10-15
            ┣         Add gamma                                                                           4a15fb Test User 24-10-15
            ┣╯        Add beta                                                                            dd7891 Test User 24-10-15
            ┗         Initial                                                                             9dc406 Test User 24-10-15
            """,
            gmd.WaitForStable(),
            repo.Path
        );

        gmd.Send("U");

        // The diverged branch is named rather than passed over: it keeps its '▼' marker, so
        // silence would look exactly like the pull having failed
        var message = gmd.WaitFor("Pull All Branches");
        Assert.AreEqual(
            """
                                  ╭ Pull All Branches ──────────────────────────────────────────────────────╮
                                  │These branches have both local and remote commits, which an update of all│
                                  │branches cannot merge, since it only fast-forwards a branch it is not on.│
                                  │Switch to the branch and pull it to merge:                               │
                                  │                                                                         │
                                  │  main                                                                   │
                                  │                                                                         │
                                  │                                [◦ OK ◦]                                 │
                                  ╰─────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(message, repo.Path, 15, 9)
        );

        gmd.Send("Enter");

        var updated = gmd.WaitUntilGone("Pull All Branches");
        Assert.AreEqual(
            """
             Gmd {repo}, ●work, ▼1, ▲1                                               (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺      ▲Main local                                                                   (main) 801397 Test User 24-10-15
            ┣│       ▼Main remote                                                                (^/main) 292075 Test User 24-10-15
            ┃│  ┣─┺ ● Work remote                                                             (^)(● work) ff355b Test User 24-10-15
            ┃│ ╭┺     Work on work                                                                        1d4a58 Test User 24-10-15
            ┣┴─╯      Add zeta                                                                            4dd1e9 Test User 24-10-15
            ┣         Add delta                                                                    [v1.0] 17d85b Test User 24-10-15
            ┣╮        Merge branch 'dev' into main                                                        4e73d2 Test User 24-10-15
            ┣         Add gamma                                                                           4a15fb Test User 24-10-15
            ┣╯        Add beta                                                                            dd7891 Test User 24-10-15
            ┗         Initial                                                                             9dc406 Test User 24-10-15
            """,
            ScreenText.Rows(updated, repo.Path, 0, 12)
        );
        Assert.AreEqual("Updated 'work'", ScreenText.LastLine(gmd.WaitFor("Updated 'work'")), "What was done is said");

        Assert.AreEqual(
            await repo.GitAsync("rev-parse origin/work"),
            await repo.GitAsync("rev-parse work"),
            "The behind branch was pulled"
        );
        Assert.AreNotEqual(
            await repo.GitAsync("rev-parse origin/main"),
            await repo.GitAsync("rev-parse main"),
            "The diverged branch was left as it was"
        );
    }

    // Git will not pull a diverged branch until it is told how to join the two sides, and gmd used
    // to pass on its refusal, a dozen lines of hints. It asks now, Merge or Rebase, and saves the
    // answer as pull.rebase, where git reads it too.
    [TestMethod]
    [DataRow("Merge", "false")]
    [DataRow("Rebase", "merges")]
    public async Task TestPullingADivergedBranchAsksHow(string button, string saved)
    {
        using var repo = await E2eRepo.CreateWithDivergedMainAsync();
        await repo.GitAsync("checkout -q main");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Main local");

        gmd.Send("u");
        StringAssert.Contains(
            gmd.WaitFor("Pull Diverged Branch"),
            "'main' has 1 commit not pushed, and origin has 1 commit"
        );
        if (button == "Rebase")
        {
            gmd.Send("Tab");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        gmd.WaitFor("Pulled 'main'");
        Assert.AreEqual(saved, (await repo.GitAsync("config pull.rebase")).Trim());
        var parents = (await repo.GitAsync("log -1 --format=%P main")).Trim().Split(' ');
        Assert.AreEqual(button == "Rebase" ? 1 : 2, parents.Length, button == "Rebase" ? "One line" : "A merge");
    }

    // Once git knows how, whether gmd saved it or the user did, nothing is asked
    [TestMethod]
    public async Task TestPullingADivergedBranchAsGitIsConfiguredAsksNothing()
    {
        using var repo = await E2eRepo.CreateWithDivergedMainAsync();
        await repo.GitAsync("checkout -q main");
        await repo.GitAsync("config pull.rebase false");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Main local");

        gmd.Send("u");

        Assert.IsFalse(gmd.WaitFor("Pulled 'main'").Contains("Pull Diverged Branch"));
    }
}
