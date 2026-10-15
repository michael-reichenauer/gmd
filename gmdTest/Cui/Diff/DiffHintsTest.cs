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
            "m menu  Esc close  + - context  s to file  f search  w hide whitespace  ←→ columns",
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
            "m menu  Esc close  Enter resolve  c commit  + - context  s to file  f search  u discard  w hide whitespace  ←→ columns",
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

    // A line history, 'git log -L', takes no context, so '+' and '-' are not offered
    [TestMethod]
    public void TestALineHistoryHasNoContextToStep()
    {
        Assert.AreEqual(
            "m menu  Esc close  s to file  f search  w hide whitespace  ←→ columns",
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
            "m menu  Esc close  + - context  s to file  n next match  f search  w hide whitespace  ←→ columns",
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
            "m menu  Esc close  + - context  s to file  f search  w show whitespace  ←→ columns",
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

    static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));
}
