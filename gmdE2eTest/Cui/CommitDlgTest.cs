// The spell check tests type misspelled words on purpose. Ignored here rather than added to the
// dictionary, so that the rest of the file is still spell checked.
// cspell:ignore resonable issu Sumerize brnach

using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// The commit dialog beyond the commit itself: the spell check in both of its text fields, the
// hint that counts what it found, the context menu of a text input, and the diff of what is
// about to be committed.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class CommitDlgTest
{
    // Misspelled words in the commit dialog are drawn in red: in the subject field, which has no
    // per character color hook and is overdrawn, and in the body, which has one. The word the
    // caret is still at the end of is being typed and is left alone until it is finished. F7 opens
    // the suggestions for the misspelled word at or after the caret, Enter on one replaces the
    // word, and Ctrl+G is the same key — with nothing misspelled after the caret it wraps around.
    [TestMethod]
    public async Task TestCommitDialogSpellCheck()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.SendText("Fix resonable issu");
        gmd.WaitFor("Fix resonable issu");
        Assert.AreEqual(
            "                       -DWWW rrrrrrrrr WWWW                                D                    m",
            ScreenText.ColorRows(gmd.CaptureColors(), 14, 1),
            "'resonable' is red, 'issu' is still being typed"
        );

        gmd.SendText(" ");
        gmd.WaitForStable();
        Assert.AreEqual(
            "                       -DWWW rrrrrrrrr rrrr                                D                    m",
            ScreenText.ColorRows(gmd.CaptureColors(), 14, 1),
            "The space finished 'issu'"
        );

        gmd.Send("Tab"); // Into the message body
        gmd.WaitForStable();
        gmd.SendText("Sumerize the brnach");
        gmd.WaitFor("Sumerize the brnach");
        gmd.Send("Left"); // The caret into 'brnach' rather than at its end, so it is not being typed
        gmd.WaitForStable();
        Assert.AreEqual(
            "                       -Drrrrrrrr WWW rrrrrr                                                   Dm",
            ScreenText.ColorRows(gmd.CaptureColors(), 16, 1)
        );

        gmd.Send("F7");
        ScreenText.AssertEqual(
            """
                                   ││Sumerize the brnach                                                   ││
                                   ││             ╭ Spelling ─────────────────╮                            ││
                                   ││             │branch                     │                            ││
                                   ││             │breach                     │                            ││
                                   ││             │broach                     │                            ││
                                   ││             │───────────────────────────│                            ││
                                   ││             │Add 'brnach' to dictionary │                            ││
                                   ││             │Ignore                     │                            ││
            """,
            ScreenText.Rows(gmd.WaitFor("Spelling"), repo.Path, 16, 8)
        );

        gmd.Send("Enter");
        gmd.WaitFor("Sumerize the branch");
        Assert.AreEqual(
            "                       -Drrrrrrrr WWW WWWWWW                                                   Dm",
            ScreenText.ColorRows(gmd.CaptureColors(), 16, 1)
        );
        // The caret is back once the menu has closed, right after the replaced word. The menu had
        // hidden it, and closing a modal used to leave it hidden until focus moved away and back.
        Assert.IsTrue(gmd.IsCursorVisible, "The caret should show again after the spelling menu closed");
        Assert.AreEqual((44, 16), gmd.CursorPosition, "The caret should be right after the replaced word");

        gmd.Send("C-g");
        ScreenText.AssertEqual(
            """
                                   ││Sumerize the branch                                                   ││
                                   ││╭ Spelling ───────────────────╮                                       ││
                                   │││Mesmerizer                   │                                       ││
                                   │││Summarize                    │                                       ││
            """,
            ScreenText.Rows(gmd.WaitFor("Spelling"), repo.Path, 16, 4)
        );
        gmd.Send("Escape");
        gmd.WaitUntilGone("Spelling");
        Assert.IsTrue(gmd.IsCursorVisible, "The caret should show again after the spelling menu was escaped");
    }

    // A right click or Shift+F10 in a text input opens gmd's own context menu in place of the one
    // Terminal.Gui has for it: the spelling suggestions on top when the caret is on a misspelled
    // word, otherwise the way to them with its key, which is how F7 gets discovered, and then the
    // edit actions with the keys the input binds them to. Only the key can be sent through tmux,
    // so it is what pins the menu; the right click shares the code.
    [TestMethod]
    public async Task TestCommitDialogContextMenu()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        // The subject field is a TextField, which binds other keys to the edit actions than the
        // body does. The caret is after 'issue', off the misspelled word, so the spelling part is
        // the way to the suggestions.
        gmd.SendText("Fix resonable issue");
        gmd.WaitFor("Fix resonable issue");
        gmd.Send("S-F10");
        ScreenText.AssertEqual(
            """
                                   │[Fix resonable issue                               ]                    │
                                   │┌───────────────────╭ Edit ─────────────────────────╮──────────────────┐│
                                   ││                   │Spelling Suggestions ...     F7│                  ││
                                   ││                   │───────────────────────────────│                  ││
                                   ││                   │Select All               Ctrl-T│                  ││
                                   ││                   │Copy                     Ctrl-C│                  ││
                                   ││                   │Cut                      Ctrl-X│                  ││
                                   ││                   │Paste                    Ctrl-V│                  ││
                                   ││                   │Undo                     Ctrl-Z│                  ││
                                   ││                   │Redo                     Ctrl-Y│                  ││
                                   ││                   ╰───────────────────────────────╯                  ││
            """,
            ScreenText.Rows(gmd.WaitFor("Select All"), repo.Path, 14, 11)
        );
        gmd.Send("Escape");
        gmd.WaitUntilGone("Select All");
        Assert.IsTrue(gmd.IsCursorVisible, "The caret should show again after the menu was escaped");

        gmd.Send("Tab"); // Into the message body
        gmd.WaitForStable();
        gmd.SendText("Sumerize the brnach");
        gmd.WaitFor("Sumerize the brnach");
        gmd.Send("Left"); // The caret into 'brnach'
        gmd.WaitForStable();

        // On a misspelled word the suggestions come first, and the menu hangs under the word
        gmd.Send("S-F10");
        ScreenText.AssertEqual(
            """
                                   ││Sumerize the brnach                                                   ││
                                   ││             ╭ Spelling ───────────────────────╮                      ││
                                   ││             │branch                           │                      ││
                                   ││             │breach                           │                      ││
                                   ││             │broach                           │                      ││
                                   ││             │Add 'brnach' to dictionary       │                      ││
                                   ││             │─────────────────────────────────│                      ││
                                   ││             │Select All                 Ctrl-T│                      ││
                                   ││             │Copy                        Alt-C│                      ││
                                   ││             │Cut                         Alt-W│                      ││
                                   │└─ 3 misspelle│Paste                      Ctrl-Y│estions ──────────────┘│
                                   │              │Undo                       Ctrl-Z│                       │
                                   ╰──────────────│Redo                       Ctrl-R│───────────────────────╯
                                                  ╰─────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Spelling"), repo.Path, 16, 14)
        );

        gmd.Send("Enter"); // The first suggestion replaces the word
        gmd.WaitFor("Sumerize the branch");
        Assert.IsTrue(gmd.IsCursorVisible, "The caret should show again after the menu closed");

        // Off a misspelled word, the menu is the edit menu with the way to the suggestions on top
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("S-F10");
        ScreenText.AssertEqual(
            """
                                   ││Sumerize the branch                                                   ││
                                   ││                   ╭ Edit ─────────────────────────╮                  ││
                                   ││                   │Spelling Suggestions ...     F7│                  ││
                                   ││                   │───────────────────────────────│                  ││
                                   ││                   │Select All               Ctrl-T│                  ││
                                   ││                   │Copy                      Alt-C│                  ││
                                   ││                   │Cut                       Alt-W│                  ││
                                   ││                   │Paste                    Ctrl-Y│                  ││
                                   ││                   │Undo                     Ctrl-Z│                  ││
                                   ││                   │Redo                     Ctrl-R│                  ││
                                   │└─ 2 misspelled word╰───────────────────────────────╯ons ──────────────┘│
            """,
            ScreenText.Rows(gmd.WaitFor("Spelling Suggestions"), repo.Path, 16, 11)
        );

        // ... which does what F7 does: the misspelled word at or after the caret, wrapping around
        gmd.Send("Enter");
        ScreenText.AssertEqual(
            """
                                   ││Sumerize the branch                                                   ││
                                   ││╭ Spelling ───────────────────╮                                       ││
                                   │││Mesmerizer                   │                                       ││
                                   │││Summarize                    │                                       ││
            """,
            ScreenText.Rows(gmd.WaitFor("Summarize"), repo.Path, 16, 4)
        );
        gmd.Send("Escape");
        gmd.WaitUntilGone("Summarize");
        Assert.IsTrue(gmd.IsCursorVisible, "The caret should show again after the spelling menu was escaped");
    }

    // While any word is red, the bottom edge of the message frame says how many and how to get at
    // the suggestions; otherwise it is the plain edge. It counts the subject as well as the body,
    // follows the same rule as the color, i.e. a word still being typed is not counted, and keeps
    // up with what the spelling menu does to the text.
    [TestMethod]
    public async Task TestCommitDialogSpellingHint()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");
        Assert.AreEqual(
            "                       │└──────────────────────────────────────────────────────────────────────┘│",
            ScreenText.Rows(gmd.WaitForStable(), repo.Path, 26, 1),
            "Nothing typed yet, so the plain edge"
        );

        gmd.SendText("Fix resonable issu");
        ScreenText.AssertEqual(
            """
                                   │└─ 1 misspelled word, F7 or right-click for suggestions ───────────────┘│
            """,
            ScreenText.Rows(gmd.WaitFor("misspelled"), repo.Path, 26, 1)
        );
        Assert.AreEqual(
            "                       -DD r rrrrrrrrrr rrrrD DD DD DDDDDDDDDDD DDD DDDDDDDDDDD DDDDDDDDDDDDDDDDm",
            ScreenText.ColorRows(gmd.CaptureColors(), 26, 1),
            "The count is red, the rest of the edge dark"
        );

        gmd.SendText(" "); // Finishes 'issu'
        gmd.WaitFor("2 misspelled words");

        gmd.Send("Tab"); // Into the message body
        gmd.WaitForStable();
        gmd.SendText("Sumerize the brnach");
        gmd.WaitFor("3 misspelled words"); // 'brnach' is still being typed
        gmd.Send("Left");
        gmd.WaitFor("4 misspelled words");

        // Replacing a word from the spelling menu is counted at once
        gmd.Send("F7");
        gmd.WaitFor("Add 'brnach' to dictionary");
        gmd.Send("Enter");
        gmd.WaitFor("Sumerize the branch");
        ScreenText.AssertEqual(
            """
                                   │└─ 3 misspelled words, F7 or right-click for suggestions ──────────────┘│
            """,
            ScreenText.Rows(gmd.WaitFor("3 misspelled words"), repo.Path, 26, 1)
        );
    }

    // 'Add to dictionary' teaches the checker a word for good: it stops being red at once, and it
    // is saved in the config, so it is still known in the next session. The menu is driven with
    // End and Up rather than counted Downs, since how many suggestions a word gets is the
    // dictionary's business.
    [TestMethod]
    public async Task TestCommitDialogAddWordToDictionary()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.SendText("Add gmd to the list");
        gmd.WaitFor("Add gmd to the list");
        Assert.AreEqual(
            "                       -DWWW rrr WW WWW WWWW                               D                    m",
            ScreenText.ColorRows(gmd.CaptureColors(), 14, 1)
        );

        gmd.Send("C-g");
        gmd.WaitFor("Add 'gmd' to dictionary");
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitUntilGone("Spelling");
        Assert.AreEqual(
            "                       -DWWW WWW WW WWW WWWW                               D                    m",
            ScreenText.ColorRows(gmd.CaptureColors(), 14, 1)
        );

        var config = File.ReadAllText(Path.Join(gmd.Home, ".gmdconfig"));
        StringAssert.Contains(config, "\"SpellWords\"");
        StringAssert.Contains(config, "\"gmd\"");
    }

    // Ctrl-D in the commit dialog shows the diff of what is about to be committed, i.e. reviewing
    // the changes without losing the message already typed. It is also the only path to the
    // uncommitted diff, which is a different screen from a commit diff — it has no commit id or
    // author, and its 'Modified' half is drawn side by side.
    [TestMethod]
    public async Task TestCommitDialogShowsTheUncommittedDiff()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.Send("C-d");
        var screen = gmd.WaitFor("Added: epsilon.txt");

        // From the message row down: the row above it is the diff's own date, which is 'now'
        Assert.AreEqual(
            """
            Message: Uncommitted changes

            2 Files:
              Modified:    alpha.txt
              Added:       epsilon.txt

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Modified: alpha.txt

            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
               1 alpha                                                 │   1 alpha
            ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│   2┃changed
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Added: epsilon.txt

            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
               1┃epsilon
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            """,
            ScreenText.Rows(screen, repo.Path, 2, 20)
        );

        // Escape leaves the diff and lands back on the commit dialog rather than on the log view,
        // which is a modal over a modal over the log view
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Added: epsilon.txt"), "Commit 2 changes on 'main':");
    }
}
