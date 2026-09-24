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
            gmd.WaitUntilGone("▲"),
            repo.Path
        );

        Assert.AreEqual(
            await repo.GitAsync("rev-parse main"),
            await repo.GitAsync("rev-parse origin/main"),
            "The remote branch is on the commit the local one is"
        );

        // v1.0 is still there, and still unpushed: pushing a branch does not push its tags,
        // and the fetch that follows no longer prunes the ones the remote has not got
        Assert.AreEqual("v1.0", await repo.GitAsync("tag --list"));
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
            gmd.WaitUntilGone("▼"),
            repo.Path
        );

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
        var message = gmd.WaitFor("Pull/Update All Branches");
        Assert.AreEqual(
            """
                                  ╭ Pull/Update All Branches ───────────────────────────────────────────────╮
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

        ScreenText.AssertEqual(
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
            gmd.WaitUntilGone("Pull/Update All Branches"),
            repo.Path
        );

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
}
