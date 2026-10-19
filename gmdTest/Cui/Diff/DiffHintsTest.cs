using gmd.Cui.Common;
using gmd.Cui.Diff;

namespace gmdTest.Cui.Diff;

[TestClass]
public class DiffHintsTest
{
    // The diff's own line, the way back to the log early on: commit and discard for the uncommitted
    // changes only, and Enter for a conflict
    [TestMethod]
    public void TestTheDiffOffersCommitAndDiscardForTheUncommittedChanges()
    {
        Assert.AreEqual(
            "m menu  Esc close  ] [ change  + - context  } { file  f search  w hide whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: false,
                    hasContext: true,
                    hasConflicts: false,
                    hasSearch: false,
                    isWhitespaceHidden: false
                )
            )
        );
        Assert.AreEqual(
            "m menu  Esc close  Enter resolve  c commit  ] [ change  + - context  } { file  f search  u discard  w hide whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: true,
                    hasContext: true,
                    hasConflicts: true,
                    hasSearch: false,
                    isWhitespaceHidden: false
                )
            )
        );
    }

    // On the '⋯' between two hunks Enter shows more context, as + does, conflicts or not
    [TestMethod]
    public void TestEnterOnTheMarkOfLinesLeftOutShowsMore()
    {
        Assert.AreEqual(
            "m menu  Esc close  Enter more context  ] [ change  + - context  } { file  f search  w hide whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: false,
                    hasContext: true,
                    hasConflicts: true,
                    hasSearch: false,
                    isWhitespaceHidden: false,
                    isOnHunkSeparator: true
                )
            )
        );
    }

    // A line history, 'git log -L', takes no context, so '+' and '-' are not offered
    [TestMethod]
    public void TestALineHistoryHasNoContextToStep()
    {
        Assert.AreEqual(
            "m menu  Esc close  ] [ change  } { file  f search  w hide whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: false,
                    hasContext: false,
                    hasConflicts: false,
                    hasSearch: false,
                    isWhitespaceHidden: false
                )
            )
        );
    }

    // n goes on to the next match once a search found any, as in the log
    [TestMethod]
    public void TestASearchWithMatchesOffersTheNextOne()
    {
        Assert.AreEqual(
            "m menu  Esc close  ] [ change  + - context  } { file  n next match  f search  w hide whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: false,
                    hasContext: true,
                    hasConflicts: false,
                    hasSearch: true,
                    isWhitespaceHidden: false
                )
            )
        );
    }

    // w says what it would do: hide the changes of whitespace alone, or show them again
    [TestMethod]
    public void TestWhitespaceIsShownAgainOnceHidden()
    {
        Assert.AreEqual(
            "m menu  Esc close  ] [ change  + - context  } { file  f search  w show whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: false,
                    hasContext: true,
                    hasConflicts: false,
                    hasSearch: false,
                    isWhitespaceHidden: true
                )
            )
        );
    }

    // x says what it does where the cursor is, leave the lines out of the commit or put them back
    [TestMethod]
    public void TestXSaysWhetherItLeavesOutOrPutsBack()
    {
        Assert.AreEqual(
            "m menu  Esc close  c commit  x put back  ] [ change  + - context  } { file  f search  u discard  w hide whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: true,
                    hasContext: true,
                    hasConflicts: false,
                    hasSearch: false,
                    isWhitespaceHidden: false,
                    leaveOut: "put back"
                )
            )
        );
    }

    // b blames the file the cursor is in, after the search, where the cursor is in a file to blame
    [TestMethod]
    public void TestBIsOfferedInAFileToBlame()
    {
        Assert.AreEqual(
            "m menu  Esc close  ] [ change  + - context  } { file  f search  b blame  w hide whitespace  v one column  ←→ columns",
            Line(
                DiffHints.For(
                    canCommit: false,
                    hasContext: true,
                    hasConflicts: false,
                    hasSearch: false,
                    isWhitespaceHidden: false,
                    canBlame: true
                )
            )
        );
    }

    static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));

    // In one column, v shows the sides again, and ← and → only scroll, there being no column to pick
    [TestMethod]
    public void TestOneColumnOffersTheSidesAgain()
    {
        var hints = DiffHints.For(false, true, false, false, false, "", isOneColumn: true);

        StringAssert.Contains(Line(hints), "v side by side  ←→ scroll");
    }

    // Tab is offered where the list of files is beside the diff, which it moves the keys to
    [TestMethod]
    public void TestTheFileListIsOfferedWhereItIsShown()
    {
        StringAssert.Contains(
            Line(DiffHints.For(false, true, false, false, false, "", hasList: true)),
            "} { file  Tab file list"
        );
        Assert.IsFalse(Line(DiffHints.For(false, true, false, false, false)).Contains("Tab"));
    }

    // Where l has hidden the list, l is offered in its place, to have it back
    [TestMethod]
    public void TestAHiddenFileListOffersL()
    {
        var line = Line(DiffHints.For(false, true, false, false, false, "", isListHidden: true));
        StringAssert.Contains(line, "} { file  l file list");
        Assert.IsFalse(line.Contains("Tab"));
    }
}
