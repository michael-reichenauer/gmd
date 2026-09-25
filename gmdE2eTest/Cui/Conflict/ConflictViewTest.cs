using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Conflict;

// The conflict resolver, opened on a merge stopped by a conflict: where it opens, moving between
// conflicts, resolving one, and what closing and saving do with the conflicts not yet decided.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class ConflictViewTest
{
    // The conflict resolver opens on the first conflict rather than at the top of the file. A
    // conflict is usually a long way down a file that is mostly text both sides agree on, so a view
    // that opened at the top would be showing anything except what it was opened for.
    [TestMethod]
    public async Task TestConflictResolverOpensOnTheFirstConflict()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);

        // Lines 35 to 61 of an 80 line file, i.e. the conflict, with the text around it for context
        // and the result of it in the pane below
        ScreenText.AssertEqual(
            """
            Merge  long.txt   conflict 1 of 1   1 still to resolve
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            line 35
            line 36
            line 37
            line 38
            line 39
            ─── Conflict 1 ── unresolved ───────
            HEAD                                                       │dev
            line 40 on main                                            │line 40 on dev
            line 41
            line 42
            line 43
            line 44
            line 45                                                                                                                ┃
            line 46                                                                                                                ┃
            line 47                                                                                                                ┃
            line 48                                                                                                                ┃
            line 49                                                                                                                ┃
            line 50                                                                                                                ┃
            line 51                                                                                                                ┃
            line 52                                                                                                                ┃
            line 53                                                                                                                ┃
            line 54                                                                                                                ┃
            line 55                                                                                                                ┃
            line 56                                                                                                                ┃
            line 57
            line 58
            line 59
            line 60
            line 61
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            Conflict 1 is not resolved yet — press 1, 2, 3, 4 or 0
            """,
            gmd.WaitFor("─── Conflict 1"),
            repo.Path
        );
    }

    // ']' and '[' walk to the conflict from wherever the cursor is. For a file with a single
    // conflict that is the whole of what they do — there is no second conflict to step to — and
    // stepping by conflict number left both keys dead in exactly the file where the conflict is
    // hardest to find by hand.
    [TestMethod]
    public async Task TestNextAndPreviousConflictReachTheOnlyConflict()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        // The top of the file, from where the conflict is below the screen
        gmd.Send("Home");
        StringAssert.Contains(gmd.WaitUntilGone("─── Conflict 1"), "line 1", "The top of the file");
        gmd.Send("]");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on main", "']' goes to the one conflict");

        // And the end of the file, from where it is above the screen
        gmd.Send("End");
        StringAssert.Contains(gmd.WaitUntilGone("─── Conflict 1"), "line 80", "The end of the file");
        gmd.Send("[");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on dev", "'[' goes back to it");
    }

    // The letter shortcuts work in both cases. Upper case is how the menu and the help write a
    // shortcut, so upper case is what gets pressed. It used to be a safety matter too: a key the
    // resolver did not handle fell through to the log view below, where 'U' pulls every branch and
    // 'P' pushes every branch. The resolver is modal now, so an unhandled key does nothing at all.
    [TestMethod]
    public async Task TestUpperCaseShortcutsActOnTheConflict()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        // 'U' is un-decide, and reaching the log view instead would leave this decided
        gmd.Send("1");
        gmd.WaitFor("all resolved");
        gmd.Send("U");
        StringAssert.Contains(gmd.WaitFor("1 still to resolve"), "── unresolved", "'U' un-decides it");

        // 'N' and 'P' are the next and previous conflict, as the menu says they are
        gmd.Send("Home");
        gmd.WaitUntilGone("─── Conflict 1");
        gmd.Send("N");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on main", "'N' goes to the conflict");

        gmd.Send("End");
        gmd.WaitUntilGone("─── Conflict 1");
        gmd.Send("P");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on dev", "'P' goes back to it");
    }

    // '0' resolves a conflict to the common ancestor, i.e. undoes what both sides did to it. The
    // ancestor is not in the file — the fixture uses git's default conflict style — so this is also
    // the test that it is recovered on demand, and it is shown as it is taken, since a decision made
    // from text that is not on the screen is one the user cannot check.
    [TestMethod]
    public async Task TestResolvingAConflictToTheCommonAncestor()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("0");

        // Three panes now, with the recovered ancestor in the middle, and the pane below showing
        // what the conflict resolves to: the line as it was before either side touched it
        ScreenText.AssertEqual(
            """
            Merge  long.txt   conflict 1 of 1   all resolved
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            line 35
            line 36
            line 37
            line 38
            line 39
            ─── Conflict 1 ── using the common ancestor
            HEAD                                   │common ancestor                        │dev
            line 40 on main                        │line 40                                │line 40 on dev
            line 41
            line 42
            line 43
            line 44
            line 45                                                                                                                ┃
            line 46                                                                                                                ┃
            line 47                                                                                                                ┃
            line 48                                                                                                                ┃
            line 49                                                                                                                ┃
            line 50                                                                                                                ┃
            line 51                                                                                                                ┃
            line 52                                                                                                                ┃
            line 53                                                                                                                ┃
            line 54                                                                                                                ┃
            line 55                                                                                                                ┃
            line 56                                                                                                                ┃
            line 57
            line 58
            line 59
            line 60
            line 61
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            line 40
            """,
            gmd.WaitFor("all resolved"),
            repo.Path
        );

        // And saving it writes the ancestor's line back into the file
        gmd.Send("S");
        gmd.WaitUntilGone("─── Conflict 1");

        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt"));
        StringAssert.Contains(text, "line 39\nline 40\nline 41", "Both sides' changes are undone");
        Assert.IsFalse(text.Contains("<<<<<<<"), "and no markers are left in it");
    }

    // Nothing is written until 'S', so closing is what throws decisions away — and the moment it is
    // most likely to happen is once every conflict has been decided and the file looks finished on
    // screen. That case used to close without a word and lose the lot, since the guard tested
    // "not fully resolved" rather than "anything decided".
    [TestMethod]
    public async Task TestClosingWithEveryConflictDecidedButUnsavedAsksFirst()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("1");
        gmd.WaitFor("all resolved");
        gmd.Send("Escape");

        StringAssert.Contains(
            gmd.WaitFor("Unsaved Decisions"),
            "1 of 1 conflicts have been decided but not saved",
            "Closing with decisions unsaved asks rather than discarding them"
        );

        // 'Stay' leaves the resolver open with the decision still made
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitUntilGone("Unsaved Decisions"), "all resolved", "Still there to save");

        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt"));
        StringAssert.Contains(text, "<<<<<<<", "and nothing has been written to the file");
    }

    // A key the resolver has no use for does nothing there. The resolver is opened from the diff
    // view, and an unhandled key used to fall through to it: 'c' is the diff's commit, which closed
    // the resolver with no word about the decisions made in it and queued a commit behind it. Tab is
    // the other half: with nothing below to take it, it must not move the focus off the file either,
    // or every key after it goes nowhere.
    [TestMethod]
    public async Task TestKeysTheResolverDoesNotUseDoNothing()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");
        gmd.Send("1");
        gmd.WaitFor("all resolved");

        gmd.Send("c");
        StringAssert.Contains(gmd.WaitForStable(), "all resolved", "'c' leaves the resolver open");

        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.Send("2");
        StringAssert.Contains(gmd.WaitFor("using dev"), "all resolved", "The keys still reach the resolver");
    }

    // Nothing decided is nothing to lose, so that close is not about unsaved work — but leaving the
    // file conflicted is still worth a word, since the merge cannot be committed until it is not
    [TestMethod]
    public async Task TestClosingWithNothingDecidedWarnsAboutTheConflictsInstead()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("Escape");

        StringAssert.Contains(
            gmd.WaitFor("Unresolved Conflicts"),
            "1 of 1 conflicts are still unresolved",
            "No decisions to lose, so it is the conflicts that are warned about"
        );
    }

    // Saving with conflicts still undecided writes their markers back, so the file no longer holds
    // the conflicts the view was built from — the decided one is stable text now and the rest have
    // moved up to fill its number. The view therefore re-reads it, and this is the test that a
    // second save then works: it used to be refused with "the file has changed on disk", leaving
    // closing and re-opening the resolver as the only way to finish the file.
    [TestMethod]
    public async Task TestSavingTwiceFinishesAFileResolvedInTwoGoes()
    {
        using var repo = await E2eRepo.CreateWithTwoConflictsAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        // Decide only the first of the two, and save
        gmd.Send("1");
        gmd.WaitFor("1 still to resolve");
        gmd.Send("S");
        StringAssert.Contains(gmd.WaitFor("Saved"), "1 of its conflicts still have their markers in it");
        gmd.Send("Enter");

        // Re-read, so what is left is one conflict rather than the second of two
        StringAssert.Contains(
            gmd.WaitFor("conflict 1 of 1"),
            "1 still to resolve",
            "The view is the file as it is now, not as it was opened"
        );

        // ... and deciding it and saving again finishes the file rather than being refused
        gmd.Send("2");
        gmd.WaitFor("all resolved");
        gmd.Send("S");
        gmd.WaitUntilGone("─── Conflict 1");

        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt"));
        Assert.IsFalse(text.Contains("<<<<<<<"), "No markers are left in it");
        StringAssert.Contains(text, "line 20 on main", "The first conflict took ours");
        StringAssert.Contains(text, "line 60 on dev", "and the second theirs");
    }

    // The conflicts of the uncommitted merge, reached the way a user reaches them: the diff of the
    // uncommitted changes, its Resolve Conflicts menu, and the one conflicted file in it
    static void OpenTheResolver(TmuxSession gmd)
    {
        gmd.WaitFor("CONFLICTS");
        gmd.Send("d");
        gmd.WaitFor("Conflicts:   long.txt");
        gmd.Send("Enter");
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");
    }
}
