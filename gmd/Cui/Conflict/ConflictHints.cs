using gmd.Cui.Common;

namespace gmd.Cui.Conflict;

// The resolver's key-hint line, see KeyHintLine. 'Esc close' comes early, as in the diff's.
static class ConflictHints
{
    // 1, 2, 3, 4 and 0 are said by the resolver's own result pane, for the conflict under the cursor
    // hasFiles is a workspace of more than one file, which } and { go between
    public static IReadOnlyList<KeyHint> For(bool hasFiles = false)
    {
        List<KeyHint> hints =
        [
            KeyHintLine.Menu,
            new("Esc", "close"),
            new("] [", "next, previous"),
            new("c", "combine"),
            new("s", "save"),
        ];
        if (hasFiles)
            hints.Add(new("} {", "file"));
        hints.AddRange([new("e", "edit"), new("b", "ancestor"), new("a", "whole file"), new("u", "clear")]);
        return hints;
    }
}
