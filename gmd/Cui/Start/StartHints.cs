using gmd.Cui.Common;

namespace gmd.Cui.Start;

// The start page's key-hint line, see KeyHintLine
static class StartHints
{
    // The start page's line, shown in place of the log's until a repository is opened. The numbers
    // open the listed repositories, and Delete takes the selected one off the recent list, which it
    // only says where it can.
    public static IReadOnlyList<KeyHint> For(int numbered, bool canRemove)
    {
        List<KeyHint> hints = [new("Enter", "open")];
        if (numbered > 0)
            hints.Add(new(numbered == 1 ? "1" : $"1-{numbered}", "open repository"));
        hints.AddRange([new("o", "open folder"), new("c", "clone"), new("i", "init")]);
        if (canRemove)
            hints.Add(new("Del", "remove from list"));
        hints.Add(new("Esc", "quit"));
        return hints;
    }
}
