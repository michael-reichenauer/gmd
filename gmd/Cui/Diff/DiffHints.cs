using gmd.Cui.Common;

namespace gmd.Cui.Diff;

// The diff view's key-hint line, see KeyHintLine. 'Esc close' comes early, since getting back to the
// log is the first thing to know in a view that covers it.
static class DiffHints
{
    public static IReadOnlyList<KeyHint> For(bool isUncommitted, bool hasConflicts)
    {
        List<KeyHint> hints = [KeyHintLine.Menu, new("Esc", "close")];
        if (hasConflicts)
            hints.Add(new("Enter", "resolve"));
        if (isUncommitted)
            hints.Add(new("c", "commit"));
        hints.AddRange([new("+ -", "context"), new("s", "to file")]);
        if (isUncommitted)
            hints.Add(new("u", "discard"));
        hints.Add(new("←→", "columns"));
        return hints;
    }
}
