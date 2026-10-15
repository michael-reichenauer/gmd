using gmd.Cui.Common;

namespace gmd.Cui.Diff;

// The diff view's key-hint line, see KeyHintLine. 'Esc close' comes early, since getting back to the
// log is the first thing to know in a view that covers it.
static class DiffHints
{
    // canCommit and hasContext are the DiffSource's: commit and discard for the uncommitted changes
    // the log opened, and more and less context for all but a line history. hasSearch is a search
    // with matches for n to go on with, as the log offers it for its own.
    public static IReadOnlyList<KeyHint> For(bool canCommit, bool hasContext, bool hasConflicts, bool hasSearch)
    {
        List<KeyHint> hints = [KeyHintLine.Menu, new("Esc", "close")];
        if (hasConflicts)
            hints.Add(new("Enter", "resolve"));
        if (canCommit)
            hints.Add(new("c", "commit"));
        if (hasContext)
            hints.Add(new("+ -", "context"));
        hints.Add(new("s", "to file"));
        if (hasSearch)
            hints.Add(new("n", "next match"));
        hints.Add(new("f", "search"));
        if (canCommit)
            hints.Add(new("u", "discard"));
        hints.Add(new("←→", "columns"));
        return hints;
    }
}
