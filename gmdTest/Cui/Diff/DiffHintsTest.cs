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
            "m menu  Esc close  + - context  s to file  ←→ columns",
            Line(DiffHints.For(isUncommitted: false, hasConflicts: false))
        );
        Assert.AreEqual(
            "m menu  Esc close  Enter resolve  c commit  + - context  s to file  u discard  ←→ columns",
            Line(DiffHints.For(isUncommitted: true, hasConflicts: true))
        );
    }

    static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));
}
