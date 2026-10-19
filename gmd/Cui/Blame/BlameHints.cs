using gmd.Cui.Common;

namespace gmd.Cui.Blame;

// The blame view's key-hint line, see KeyHintLine. 'Esc close' comes early, as in the diff's.
static class BlameHints
{
    // hasSearch is a search with matches for n to go on with, as in the diff's
    public static IReadOnlyList<KeyHint> For(bool isDetailsShown, bool hasPrevious, bool canGoBack, bool hasSearch)
    {
        List<KeyHint> hints =
        [
            KeyHintLine.Menu,
            new("Esc", "close"),
            new("Enter", isDetailsShown ? "hide details" : "details"),
        ];
        hints.Add(new("d", "diff"));
        hints.Add(new("l", "line history"));
        hints.Add(new("⇧l", "file history"));
        if (hasPrevious)
            hints.Add(new("p", "previous"));
        if (canGoBack)
            hints.Add(new("Backspace", "back"));
        if (hasSearch)
            hints.Add(new("n", "next match"));
        hints.Add(new("f", "search"));
        hints.AddRange([new("i", "copy id"), new("g", "gutter")]);
        return hints;
    }
}
