using Terminal.Gui.Input;

namespace gmd.Cui.Common;

// Keys that stand in for the arrow keys, for a keyboard that has none, e.g. the one on the screen of
// an iPad, in a terminal in the browser. j and k are up and down as in vim and less, and J and K
// select rows as Shift-↓ and Shift-↑ do. h is left in vim, but in the log it hides a branch, so left
// and right are ',' and '.', which every keyboard has beside the letters, the iPad's too, and which
// look like the < and > above them. A view that takes them has no other use for these keys.
static class LetterArrows
{
    static readonly IReadOnlyDictionary<Key, Key> Arrows = new Dictionary<Key, Key>
    {
        [Key.J] = Key.CursorDown,
        [Key.K] = Key.CursorUp,
        [Key.J.WithShift] = Key.CursorDown.WithShift,
        [Key.K.WithShift] = Key.CursorUp.WithShift,
        [(Key)','] = Key.CursorLeft,
        [(Key)'.'] = Key.CursorRight,
    };

    // The arrow key a key stands in for, or the key itself
    public static Key ArrowOf(Key key) => Arrows.TryGetValue(key, out var arrow) ? arrow : key;
}
