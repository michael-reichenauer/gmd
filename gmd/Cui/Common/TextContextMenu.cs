using gmd.Common.Spelling;
using Terminal.Gui;

namespace gmd.Cui.Common;

// The context menu of a text input, opened by a right click or Shift+F10: the gesture and the key
// that open a menu in every other view, and what Terminal.Gui's own edit menu for a text input
// answered to, which this replaces, since that one knew nothing of spelling. What is in it is
// decided here, with no view, so it is unit testable; UITextView and UITextField open it.
//
// The spelling comes first: the suggestions when the caret is on a misspelled word, and otherwise
// the way to them with its key, which is how F7 gets discovered. Then the edit actions, with the
// keys the view binds them to, which differ between a TextView and a TextField.
static class TextContextMenu
{
    public static bool IsMenuKey(Key key) => key == (Key.F10 | Key.ShiftMask);

    public static string Title(WordSpan? misspelled) => misspelled == null ? "Edit" : "Spelling";

    // The spelling part, for the misspelled word at the caret if there is one. Nothing when spell
    // checking is off, and a disabled entry when nothing is misspelled, so the key is still shown.
    public static IEnumerable<MenuItem> SpellingItems(
        ISpellChecker? spellChecker,
        WordSpan? misspelled,
        bool hasMisspelled,
        Action<string> replace,
        Action showSuggestions,
        Action redraw
    )
    {
        if (spellChecker?.IsEnabled != true)
            return [];
        if (misspelled == null)
            return Menu.Items.Item("Spelling Suggestions ...", "F7", showSuggestions, () => hasMisspelled);
        return Menu
            .Items.Items(SpellSpans.Suggestions(spellChecker, misspelled.Word, replace))
            .Item(SpellSpans.AddToDictionary(spellChecker, misspelled.Word, redraw));
    }

    // The whole menu: the spelling part, then the edit actions of the view. Those are not public
    // in Terminal.Gui, so each is run by the key the view binds it to, exactly as if it was pressed,
    // and that key is what the menu shows for it.
    public static IEnumerable<MenuItem> Items(IEnumerable<MenuItem> spelling, View view, Func<bool> hasSelection)
    {
        var items = Menu.Items.Items(spelling);
        items.Separator(items.Count > 0);
        return items
            .Item("Select All", ShortcutOf(view, Command.SelectAll), () => Run(view, Command.SelectAll))
            .Item("Copy", ShortcutOf(view, Command.Copy), () => Run(view, Command.Copy), hasSelection)
            .Item("Cut", ShortcutOf(view, Command.Cut), () => Run(view, Command.Cut), hasSelection)
            .Item("Paste", ShortcutOf(view, Command.Paste), () => Run(view, Command.Paste))
            .Item("Undo", ShortcutOf(view, Command.Undo), () => Run(view, Command.Undo))
            .Item("Redo", ShortcutOf(view, Command.Redo), () => Run(view, Command.Redo));
    }

    // The key a view binds a command to, written the way the other menus write theirs
    public static string ShortcutOf(View view, Command command) =>
        ShortcutHelper.GetShortcutTag(view.GetKeyFromCommand(command)).ToString()!.Replace('+', '-');

    static void Run(View view, Command command) =>
        view.ProcessKey(new KeyEvent(view.GetKeyFromCommand(command), new KeyModifiers()));
}
