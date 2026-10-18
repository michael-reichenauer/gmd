using gmd.Cui.Common;

namespace gmd.Cui.Diff;

// The diff view's key-hint line, see KeyHintLine. 'Esc close' comes early, since getting back to the
// log is the first thing to know in a view that covers it.
static class DiffHints
{
    // canCommit and hasContext are the DiffSource's: commit and discard for the uncommitted changes
    // the log opened, and more and less context for all but a line history. hasSearch is a search
    // with matches for n to go on with, as the log offers it for its own, and isWhitespaceHidden
    // whether w shows whitespace changes again or hides them. leaveOut is what x does where the
    // cursor is, "leave out" or "put back", "" where it does nothing. isOneColumn is the layout v
    // switches from, where ← and → only scroll, hasList whether the list of files Tab moves to is
    // beside the diff, and isListHidden whether it would be but for l having hidden it.
    // isOnHunkSeparator is the cursor on the '⋯' between two hunks, where Enter shows more context.
    public static IReadOnlyList<KeyHint> For(
        bool canCommit,
        bool hasContext,
        bool hasConflicts,
        bool hasSearch,
        bool isWhitespaceHidden,
        string leaveOut = "",
        bool isOneColumn = false,
        bool hasList = false,
        bool isListHidden = false,
        bool isOnHunkSeparator = false
    )
    {
        List<KeyHint> hints = [KeyHintLine.Menu, new("Esc", "close")];
        if (isOnHunkSeparator && hasContext)
            hints.Add(new("Enter", "more context"));
        else if (hasConflicts)
            hints.Add(new("Enter", "resolve"));
        if (canCommit)
            hints.Add(new("c", "commit"));
        if (leaveOut != "")
            hints.Add(new("x", leaveOut));
        hints.Add(new("] [", "change"));
        if (hasContext)
            hints.Add(new("+ -", "context"));
        hints.Add(new("} {", "file"));
        if (hasList)
            hints.Add(new("Tab", "file list"));
        else if (isListHidden)
            hints.Add(new("l", "file list"));
        if (hasSearch)
            hints.Add(new("n", "next match"));
        hints.Add(new("f", "search"));
        if (canCommit)
            hints.Add(new("u", "discard"));
        hints.Add(new("w", isWhitespaceHidden ? "show whitespace" : "hide whitespace"));
        hints.Add(new("v", isOneColumn ? "side by side" : "one column"));
        hints.Add(new("←→", isOneColumn ? "scroll" : "columns"));
        return hints;
    }
}
