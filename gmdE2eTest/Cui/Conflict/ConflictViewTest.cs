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
               35 line 35
               36 line 36
               37 line 37
               38 line 38
               39 line 39
            ─── Conflict 1 ── unresolved ───────
            main (HEAD) · Change it on main                            │dev · Change it on dev
            ~  40 line 40 on main                                      │~  40 line 40 on dev
               41 line 41
               42 line 42                                                                                                          ┃
               43 line 43                                                                                                          ┃
               44 line 44                                                                                                          ┃
               45 line 45                                                                                                          ┃
               46 line 46                                                                                                          ┃
               47 line 47                                                                                                          ┃
               48 line 48                                                                                                          ┃
               49 line 49                                                                                                          ┃
               50 line 50
               51 line 51
               52 line 52
               53 line 53
               54 line 54
               55 line 55
            ─── Result of conflict 1 ─────────────────────────────────────────────────────────────── ▌main ▌dev ▌both or by hand ───
               37 line 37
               38 line 38
               39 line 39
            ▌      Conflict 1 is not decided: 1 main, 2 dev, 3 or 4 both, 0 neither, e edit
               40 line 41
               41 line 42
               42 line 43                                                                                                          ┃
               43 line 44                                                                                                          ┃
               44 line 45                                                                                                          ┃
               45 line 46                                                                                                          ┃
               46 line 47
               47 line 48
               48 line 49
               49 line 50
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
               35 line 35
               36 line 36
               37 line 37
               38 line 38
               39 line 39
            ─── Conflict 1 ── using the common ancestor
            main (HEAD) · Change it on main        │common ancestor                        │dev · Change it on dev
            ~  40 line 40 on main                  │      line 40                          │~  40 line 40 on dev
               41 line 41
               42 line 42                                                                                                          ┃
               43 line 43                                                                                                          ┃
               44 line 44                                                                                                          ┃
               45 line 45                                                                                                          ┃
               46 line 46                                                                                                          ┃
               47 line 47                                                                                                          ┃
               48 line 48                                                                                                          ┃
               49 line 49                                                                                                          ┃
               50 line 50
               51 line 51
               52 line 52
               53 line 53
               54 line 54
               55 line 55
            ─── Result of conflict 1 ─────────────────────────────────────────────────────────────── ▌main ▌dev ▌both or by hand ───
               37 line 37
               38 line 38
               39 line 39
            ▌  40 line 40
               41 line 41
               42 line 42
               43 line 43                                                                                                          ┃
               44 line 44                                                                                                          ┃
               45 line 45                                                                                                          ┃
               46 line 46                                                                                                          ┃
               47 line 47
               48 line 48
               49 line 49
               50 line 50
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

    // Escape on the question is Stay, like any other way of backing out of it. It used to fall to
    // Discard and Close, the one answer that loses the decisions, since only Stay's own button was
    // checked for.
    [TestMethod]
    public async Task TestEscapeOnTheUnsavedDecisionsQuestionStays()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");
        gmd.Send("1");
        gmd.WaitFor("all resolved");
        gmd.Send("Escape");
        gmd.WaitFor("Unsaved Decisions");

        gmd.Send("Escape");

        StringAssert.Contains(gmd.WaitUntilGone("Unsaved Decisions"), "all resolved", "Still there to save");
        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt"));
        StringAssert.Contains(text, "<<<<<<<", "and nothing has been written to the file");
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
        StringAssert.Contains(gmd.WaitFor("Saved"), "was saved, with 1 of its conflicts still to resolve");
        Assert.AreEqual("UU long.txt", (await repo.GitAsync("status --porcelain")).Trim(), "And it is not staged");
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
    // The resolver's key hints, on its bottom row below the result pane, which names what 1 to 4
    // and 0 take for the conflict under the cursor
    [TestMethod]
    public async Task TestTheResolverHasHintsOfItsOwn()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        OpenTheResolver(gmd);

        var screen = gmd.WaitFor("] [ next, previous");
        StringAssert.Contains(ScreenText.LastLine(screen), "m menu  Esc close  ] [ next, previous  c combine  s save");
        StringAssert.Contains(screen, "is not decided: 1 main", "The result pane is still there, above the hints");
    }

    // A file one side deleted has no text to merge, so it is resolved whole, with no view of the
    // resolver open. The choice returned before git had run, and the diff, read again at once, could
    // still list the file as conflicted. It is read once git is done now.
    [TestMethod]
    public async Task TestKeepingAFileTheOtherSideDeletedResolvesIt()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAtAsync("file.txt", "one\n", "Initial", TempRepo.BaseTime);
        await repo.GitAsync("checkout -q -b dev");
        await repo.GitAsync("rm -q file.txt");
        repo.GitAt(["commit", "-q", "-m", "Delete the file"], TempRepo.BaseTime.AddMinutes(1));
        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAtAsync("file.txt", "one\ntwo\n", "Change the file", TempRepo.BaseTime.AddMinutes(2));
        await repo.GitAllowFailAsync("merge dev");
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        gmd.WaitFor("CONFLICTS");
        gmd.Send("d");
        // Git's diff has nothing to show of a file one side deleted, so the diff lists no file, and
        // Enter offers the conflicts to resolve
        gmd.WaitFor("Enter resolve");
        gmd.Send("Enter");
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitFor("Keep the File"), "file.txt was changed here but deleted on the other side.");
        gmd.Send("Left"); // From Cancel, the default, past Delete It to Keep the File
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");

        // The last conflict resolved, the merge can be finished; later, back to the diff
        StringAssert.Contains(gmd.WaitFor("Conflicts Resolved"), "Every conflict of the merge is resolved.");
        gmd.Send("Right");
        gmd.WaitForStable();
        gmd.Send("Enter");

        gmd.WaitUntilGone("Enter resolve"); // The diff, read again, has no conflict left to resolve
        Assert.AreEqual("", (await repo.GitAsync("ls-files -u")).Trim(), "Git has it resolved");
        Assert.AreEqual(
            "one\ntwo\n",
            await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt")),
            "Kept as changed here"
        );
    }

    // Two files a side deleted, each question cancelled: the second is asked about after the first,
    // and then the resolver closes. Going round the end asked about the first again, and so on, for
    // as long as each was cancelled, with Cancel the default, so that Enter went round as well.
    [TestMethod]
    public async Task TestCancellingTheQuestionOfEachFileCloses()
    {
        using var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("a.txt", "a\n", "Add a", t);
        await repo.CommitFileAtAsync("b.txt", "b\n", "Add b", t.AddMinutes(1));
        await repo.GitAsync("checkout -q -b dev");
        await repo.GitAsync("rm -q a.txt b.txt");
        repo.GitAt(["commit", "-q", "-m", "Delete both"], t.AddMinutes(2));
        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAtAsync("a.txt", "a2\n", "Change a", t.AddMinutes(3));
        await repo.CommitFileAtAsync("b.txt", "b2\n", "Change b", t.AddMinutes(4));
        await repo.GitAllowFailAsync("merge dev");
        using var gmd = TmuxSession.StartGmd(repo);
        var bar = gmd.WaitFor("2 conflicts");
        var (x, y) = TmuxSession.PositionOf(bar, "2 conflicts");
        gmd.Click(x + 2, y); // The bar's menu of the merge
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitFor("Resolve a.txt"), "a.txt was changed here but deleted");
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitFor("Resolve b.txt"), "b.txt was changed here but deleted");
        gmd.Send("Escape");

        gmd.WaitUntilGone("Resolve b.txt");
        var screen = gmd.WaitForStable();
        Assert.IsFalse(screen.Contains("Resolve a.txt"), "Not asked about again:\n" + screen);
        StringAssert.Contains(screen, "2 conflicts", "Back in the log, the merge still stopped");
    }

    // A file whose markers were taken out by hand, in an editor, but not marked resolved: the two
    // sides were the only choices, and either threw the hand merge away. It is marked resolved as it is.
    [TestMethod]
    public async Task TestAFileResolvedByHandIsMarkedResolvedAsItIs()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        var merged = string.Join("\n", Enumerable.Range(1, 80).Select(i => $"line {i} merged by hand")) + "\n";
        await File.WriteAllTextAsync(Path.Join(repo.Path, "long.txt"), merged);
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        gmd.WaitFor("CONFLICTS");
        gmd.Send("d");
        gmd.WaitFor("Enter resolve");
        gmd.Send("Enter");
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitFor("Mark Resolved"), "no conflict markers left in it");
        for (var i = 0; i < 3; i++) // From Cancel, the default, to Mark Resolved
        {
            gmd.Send("Left");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        // The last conflict resolved, Commit, the default, closes the diff for the commit of the merge
        gmd.WaitFor("Conflicts Resolved");
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitFor("Commit 1 change"), "Merge branch 'dev'");
        Assert.AreEqual("", (await repo.GitAsync("ls-files -u")).Trim(), "Git has it resolved");
        Assert.AreEqual(merged, await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt")), "As it was merged");
    }

    // 'b' shows the common ancestor, which makes a conflict as tall as its longest side. The cursor
    // stays on the conflict it was on: it stayed at its row, which the taller first conflict had
    // moved the text above the second one to, so 'b' on conflict 2 left it on conflict 1.
    [TestMethod]
    public async Task TestShowingTheAncestorKeepsTheCursorOnItsConflict()
    {
        using var repo = await E2eRepo.CreateWithUnevenConflictsAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");
        gmd.Send("]");
        gmd.WaitFor("conflict 2 of 2");

        gmd.Send("b");

        var screen = gmd.WaitFor("common ancestor");
        StringAssert.Contains(ScreenText.Rows(screen, repo.Path, 0, 1), "conflict 2 of 2");
    }

    // The result pane below is the file as it will be saved, scrolled to the conflict the cursor is in,
    // a few lines above it in sight, which its title names. It is scrolled with Shift-↓ and Shift-↑,
    // since the pane never takes the focus from the file, or by the mouse wheel over it. Back on a
    // conflict, its result is shown from its start again.
    [TestMethod]
    public async Task TestALongResultIsScrolled()
    {
        using var repo = await E2eRepo.CreateWithUnevenConflictsAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "─── Result of conflict 1 ───");
        gmd.Send("]");
        StringAssert.Contains(gmd.WaitFor("conflict 2 of 2"), "─── Result of conflict 2 ───");
        gmd.Send("1");
        WaitForLastLine(gmd, "▌  64 line 60 on main, 11 of 12");

        for (int i = 0; i < 4; i++)
        {
            gmd.Send("S-Down");
            gmd.WaitForStable();
        }
        WaitForLastLine(gmd, "68 line 63");

        var bottom = gmd.Capture().Split('\n').Length - 1;
        gmd.Wheel(10, bottom, -1);
        WaitForLastLine(gmd, "67 line 62");
        gmd.Send("S-Up");
        WaitForLastLine(gmd, "66 line 61");

        gmd.Send("[");
        gmd.WaitFor("conflict 1 of 2");
        gmd.Send("]");
        gmd.WaitFor("conflict 2 of 2");
        WaitForLastLine(gmd, "▌  64 line 60 on main, 11 of 12");
    }

    // The bottom row, the result pane's last when the hints are off, read as it settles and without
    // the pane's scrollbar
    static void WaitForLastLine(TmuxSession gmd, string expected)
    {
        gmd.WaitForMoving(
            s => ScreenText.LastLine(s).TrimEnd('┃').TrimEnd() == expected,
            $"the last line to read '{expected}'"
        );
        gmd.WaitForStable();
    }

    // c combines both sides' changes where they touch no word in common: in one conflict each side
    // changed other words of a line, and added a line, and in the other one side only added lines
    // above the lines the other rewrote. Saved, the file is both changes, and marked resolved.
    [TestMethod]
    public async Task TestCombineBothChanges()
    {
        using var repo = await E2eRepo.CreateWithCombinableConflictsAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("CONFLICTS");
        gmd.Send("d");
        gmd.WaitFor("C Pricing.cs");
        gmd.Send("Enter");
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");
        StringAssert.Contains(
            gmd.WaitFor("─── Conflict 1"),
            "Conflict 1 is not decided: 1 main, 2 dev, c combine both"
        );

        gmd.Send("c");
        Assert.AreEqual(
            "Conflict 1 is both changes combined: u takes it back",
            ScreenText.LastLine(gmd.WaitFor("both changes combined:"))
        );
        gmd.Send("]");
        gmd.WaitFor("conflict 2 of 2");
        gmd.Send("c");
        gmd.WaitFor("all resolved");
        gmd.Send("s");
        gmd.WaitUntilGone("all resolved");

        Assert.AreEqual(
            """
            decimal Total(Order order)
            {
                var sum = order.Sum;
                var discounted = sum - sum * Discount(order.Customer);
                return Math.Round(discounted + discounted * vatRate, 2);
            }

            decimal Discount(Customer customer)
            {
                if (customer.IsGold)
                    return 0.10m;
                return customer.IsMember ? 0.05m : 0m;
            }

            """,
            File.ReadAllText(Path.Join(repo.Path, "Pricing.cs"))
        );
        Assert.AreEqual("M  Pricing.cs", (await repo.GitAsync("status --porcelain")).TrimEnd(), "Marked resolved");
    }

    // Where both change the same words there is nothing to combine, and c says why
    [TestMethod]
    public async Task TestCombineSaysWhyItCannot()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("c");
        Assert.AreEqual(
            "The two sides change the same words here, or write lines in one place: e edits it by hand",
            ScreenText.LastLine(gmd.WaitFor("change the same words"))
        );
        StringAssert.Contains(gmd.Capture(), "── unresolved", "Nothing decided");
    }

    // The files of a merge are resolved one after the other, from the bar's Resolve Conflicts: a
    // terminal wide enough lists them beside the resolver, } goes to the next, and saving a file
    // resolved opens the next one still conflicted. Once none is left, the merge is committed, or
    // not yet, as asked; it was a box saying to resolve them in the diff, and a commit from the log.
    [TestMethod]
    public async Task TestTheFilesOfAMergeAreResolvedOneAfterTheOther()
    {
        using var repo = await E2eRepo.CreateWithConflictsInTwoFilesAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 160);
        var bar = gmd.WaitFor("Merging: 2 conflicts");
        var (x, y) = TmuxSession.PositionOf(bar, "Merging: 2 conflicts");
        gmd.Click(x + 2, y);
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");

        var screen = gmd.WaitFor("─── Conflict 1");
        Assert.AreEqual(
            """
            Merge  Pricing.cs   conflict 1 of 2   2 still to resolve   file 1 of 2
            """,
            ScreenText.Rows(screen, repo.Path, 0, 1)
        );
        Assert.AreEqual(
            """
            Conflicts 2 left       │
            ▌● Pricing.cs  0 of 2  │
             ● Shipping.cs         │
            """,
            ListOf(screen)
        );

        // } goes to the other file, the one main and dev each set a cost in
        gmd.Send("}");
        screen = gmd.WaitFor("file 2 of 2");
        StringAssert.Contains(ScreenText.Rows(screen, repo.Path, 0, 1), "Merge  Shipping.cs   conflict 1 of 1");
        gmd.Send("1"); // main's cost
        gmd.WaitForStable();

        // Saved, it is ticked, and the one still conflicted opens
        gmd.Send("s");
        screen = gmd.WaitFor("file 1 of 2");
        Assert.AreEqual(
            """
            Conflicts 1 left       │
            ▌● Pricing.cs  0 of 2  │
             ✓ Shipping.cs         │
            """,
            ListOf(screen)
        );
        Assert.AreEqual("", (await repo.GitAsync("diff --name-only HEAD -- Shipping.cs")).Trim(), "main's cost");

        gmd.Send("c");
        gmd.WaitForStable();
        gmd.Send("]");
        gmd.WaitForStable();
        gmd.Send("c");
        gmd.WaitFor("conflict 2 of 2   all resolved");

        // The last file saved, every conflict is resolved, and Commit, the default, commits the merge
        gmd.Send("s");
        StringAssert.Contains(gmd.WaitFor("Conflicts Resolved"), "A commit finishes it.");
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitFor("Commit 1 change on 'main'"), "Merge branch 'dev'");
        Assert.AreEqual("", (await repo.GitAsync("ls-files -u")).Trim(), "Git has them resolved");
    }

    // The list of files at the left of the resolver, the rows below the header
    static string ListOf(string screen) =>
        string.Join("\n", ScreenText.Rows(screen, "", 2, 3).Split('\n').Select(r => r[..Math.Min(r.Length, 24)]));

    // e types a conflict's result by hand in the result pane, in the file around it, with the sides in
    // sight above, from both sides' changes combined where they can be. Esc is done, and what was typed
    // is the decision; leaving it with nothing typed leaves the conflict as it was. It was a box over
    // the sides, which hid what was being merged.
    [TestMethod]
    public async Task TestAConflictIsTypedInTheResultPane()
    {
        using var repo = await E2eRepo.CreateWithCombinableConflictsAsync();
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        OpenTheResolver(gmd, "Pricing.cs");
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("e");
        var screen = gmd.WaitFor("Typing conflict 1");
        Assert.AreEqual(
            """
            ─── Typing conflict 1 by hand, from both sides' changes combined, Esc when done ──────── ▌main ▌dev ▌both or by hand ───
                1 decimal Total(Order order)
                2 {
                3     var sum = order.Sum;
            ▌   4     var discounted = sum - sum * Discount(order.Customer);
            ▌   5     return Math.Round(discounted + discounted * vatRate, 2);
            ▌
                6 }
                7
            """,
            ScreenText.Rows(screen, repo.Path, 25, 9)
        );
        Assert.AreEqual(
            "Esc done  Ctrl-Z undo",
            ScreenText.LastLine(screen).Trim('─', ' ').Replace("? help", "").Trim('─', ' ')
        );
        Assert.IsTrue(gmd.IsCursorVisible, "The caret is in the text");
        Assert.AreEqual((6, 29), gmd.CursorPosition, "At the start of its first line");

        // Letters that are keys of the resolver are text here, and a line added renumbers the file after it
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.SendText(" // s and q");
        gmd.WaitFor("// s and q");
        gmd.Send("Enter");
        gmd.WaitForStable();
        gmd.SendText("    Log(sum)");
        screen = gmd.WaitFor("Log(sum)");
        Assert.AreEqual(
            """
            ▌   4     var discounted = sum - sum * Discount(order.Customer); // s and q
            ▌   5     Log(sum)
            ▌   6     return Math.Round(discounted + discounted * vatRate, 2);
            ▌
                7 }
            """,
            ScreenText.Rows(screen, repo.Path, 29, 5)
        );

        gmd.Send("Escape");
        screen = gmd.WaitFor("edited by hand");
        StringAssert.Contains(ScreenText.Rows(screen, repo.Path, 0, 1), "1 still to resolve");
        StringAssert.Contains(ScreenText.LastLine(screen), "Conflict 1 is written by hand: u takes it back");

        // A click on the sides leaves the text too, here with nothing typed, so conflict 2 is as it was
        gmd.Send("]");
        gmd.WaitFor("conflict 2 of 2");
        gmd.Send("e");
        gmd.WaitFor("Typing conflict 2");
        gmd.Click(10, 13);
        screen = gmd.WaitUntilGone("Typing conflict 2");
        StringAssert.Contains(ScreenText.LastLine(screen), "Conflict 2 is as it was, nothing was typed");
        StringAssert.Contains(ScreenText.Rows(screen, repo.Path, 0, 1), "1 still to resolve");

        gmd.Send("c");
        gmd.WaitFor("all resolved");
        gmd.Send("s");
        gmd.WaitFor("Conflicts Resolved");
        Assert.AreEqual(
            """
            decimal Total(Order order)
            {
                var sum = order.Sum;
                var discounted = sum - sum * Discount(order.Customer); // s and q
                Log(sum)
                return Math.Round(discounted + discounted * vatRate, 2);
            }

            decimal Discount(Customer customer)
            {
                if (customer.IsGold)
                    return 0.10m;
                return customer.IsMember ? 0.05m : 0m;
            }

            """,
            await File.ReadAllTextAsync(Path.Join(repo.Path, "Pricing.cs"))
        );
    }

    static void OpenTheResolver(TmuxSession gmd, string file = "long.txt")
    {
        gmd.WaitFor("CONFLICTS");
        gmd.Send("d");
        gmd.WaitFor($"C {file}");
        gmd.Send("Enter");
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");
    }
}
