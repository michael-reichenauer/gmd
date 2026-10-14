using gmd.Cui.Common;

namespace gmd.Cui.Blame;

// The blame view's key-hint line, see KeyHintLine. 'Esc close' comes early, as in the diff's.
static class BlameHints
{
    public static IReadOnlyList<KeyHint> For(bool isDetailsShown, bool hasPrevious, bool canGoBack)
    {
        List<KeyHint> hints =
        [
            KeyHintLine.Menu,
            new("Esc", "close"),
            new("Enter", isDetailsShown ? "hide details" : "details"),
        ];
        hints.Add(new("d", "diff"));
        hints.Add(new("l", "line history"));
        if (hasPrevious)
            hints.Add(new("p", "previous"));
        if (canGoBack)
            hints.Add(new("Backspace", "back"));
        hints.AddRange([new("i", "copy id"), new("g", "gutter")]);
        return hints;
    }
}
