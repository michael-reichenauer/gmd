namespace gmd.Cui.Common;

// A normal menu item and base class for SubMenu and MenuSeparator
record MenuItem(string Text, string Shortcut, Action Action, Func<bool>? CanExecute = null)
{
    public bool IsDisabled { get; init; }

    // Why the item is greyed out, said on the status line when it is picked anyway, by a click or
    // its key: a greyed item that says nothing leaves the user to guess. Asked only then, and only
    // while it is greyed out, so it can name whichever of its conditions failed.
    public Func<string>? WhyNot { get; init; }
}

// To create a sub menu
record SubMenu : MenuItem
{
    public SubMenu(string text, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
        : base(text, shortcut, () => { }, canExecute)
    {
        Children = children;
    }

    public IEnumerable<MenuItem> Children { get; init; }

    // Typing in the sub menu, or in a sub menu of it, closes the menus and calls this with what was
    // typed, see Menu.OnTypeText. For a long list of names, where typing finds one faster.
    public Action<string>? OnTypeText { get; init; }
}

// To create a menu separator line or header line
record MenuSeparator : MenuItem
{
    public MenuSeparator(string text = "")
        : base(text, "", () => { }, () => false) { }
}
