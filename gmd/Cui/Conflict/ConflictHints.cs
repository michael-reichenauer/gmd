using gmd.Cui.Common;

namespace gmd.Cui.Conflict;

// The resolver's key-hint line, see KeyHintLine. 'Esc close' comes early, as in the diff's.
static class ConflictHints
{
    // 1, 2, 3, 4 and 0 are said by the resolver's own result pane, for the conflict under the cursor
    public static IReadOnlyList<KeyHint> For() =>
        [
            KeyHintLine.Menu,
            new("Esc", "close"),
            new("] [", "next, previous"),
            new("s", "save"),
            new("e", "edit"),
            new("b", "ancestor"),
            new("a", "whole file"),
            new("u", "clear"),
        ];
}
