using Terminal.Gui;

namespace gmd.Cui.Common;

// The keys that pick an item of an open menu: the key the item shows beside it. The shortcut column
// names the key that runs the same command from the view the menu was opened in, so pressing it in
// the menu does what it says, rather than nothing, as it used to.
//
// This is the parsing and the choice of item, with no view, so it is tested without a terminal;
// Menu registers the keys.
static class MenuShortcuts
{
    static readonly Key[] FunctionKeys =
    [
        Key.F1,
        Key.F2,
        Key.F3,
        Key.F4,
        Key.F5,
        Key.F6,
        Key.F7,
        Key.F8,
        Key.F9,
        Key.F10,
        Key.F11,
        Key.F12,
    ];

    // The keys a shortcut stands for. "C" is 'c', "Shift-E" is 'E', "], N" is ']' or 'n', and "Ctrl-C",
    // "Alt-C", "F7" and "Backspace" are those keys. What a menu itself uses (Enter, Esc and the
    // arrows) gives none, and neither does what is not a key: some menus use the column for other
    // things, such as the initials of the author of a branch.
    public static IReadOnlyList<Key> KeysOf(string shortcut) =>
        shortcut
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(KeyOf)
            .OfType<Key>()
            .ToList();

    // The item each key picks: the first item showing it. A letter picks its item in both cases,
    // since the column writes letters in upper case and that is what gets pressed, unless another
    // item shows the upper case one as "Shift-...", which is then a command of its own. A disabled
    // item still claims its keys, which then do nothing: 'P' on a menu whose "Shift-P" is greyed
    // out must not fall back to the item showing "P", a different command.
    public static IReadOnlyDictionary<Key, int> Of(IReadOnlyList<MenuItem> items)
    {
        Dictionary<Key, int> keys = [];
        for (int i = 0; i < items.Count; i++)
        {
            foreach (var key in KeysOf(items[i].Shortcut))
                keys.TryAdd(key, i);
        }

        // After every item, so that a "Shift-..." further down the menu wins over the upper case
        for (int i = 0; i < items.Count; i++)
        {
            foreach (var key in KeysOf(items[i].Shortcut).Where(k => k is >= Key.a and <= Key.z))
                keys.TryAdd((Key)char.ToUpperInvariant((char)key), i);
        }

        return keys.Where(k => !items[k.Value].IsDisabled).ToDictionary();
    }

    static Key? KeyOf(string part)
    {
        // Printable ASCII only: '←' and '→' are the arrows, which move in the menu
        if (part.Length == 1)
        {
            if (part[0] is <= ' ' or > '~')
                return null;
            return char.IsLetter(part[0]) ? (Key)char.ToLowerInvariant(part[0]) : (Key)part[0];
        }

        if (part.StartsWith("Shift-") && part.Length == 7 && char.IsLetter(part[6]))
            return (Key)char.ToUpperInvariant(part[6]);
        if (part.StartsWith("Ctrl-") && part.Length == 6 && char.IsLetter(part[5]))
            return (Key)char.ToUpperInvariant(part[5]) | Key.CtrlMask;
        if (part.StartsWith("Alt-") && part.Length == 5 && char.IsLetter(part[4]))
            return (Key)char.ToUpperInvariant(part[4]) | Key.AltMask;
        if (part == "Backspace")
            return Key.Backspace;
        if (part.Length is 2 or 3 && part[0] == 'F' && int.TryParse(part[1..], out var n) && n is >= 1 and <= 12)
            return FunctionKeys[n - 1];

        return null;
    }
}
