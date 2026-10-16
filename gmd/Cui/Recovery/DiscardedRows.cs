using System.Globalization;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Recovery;

// The rows of the discarded changes dialog, kept apart from the dialog so they can be asserted
// without a driver, and what the status line says of a restore. One row per discard, or stash
// dropped, newest first:
//
//     Time              Discarded
//     2026-10-16 14:02  3 lines of lines.txt
//     2026-10-16 13:40  All changes
//     2026-10-15 17:12  Stash  On main: experiment
static class DiscardedRows
{
    const int TimeWidth = 18; // 'yyyy-MM-dd HH:mm' and two spaces
    const int KindWidth = 7; // 'Stash' and two spaces

    // Narrower than this and what was discarded has no room to be said
    public const int MinWidth = TimeWidth + 30;

    public static Text Header(int width) =>
        Text.Dark("Time".Max(TimeWidth, true)).Dark("Discarded".Max(Math.Max(0, width - TimeWidth), true)).ToText();

    public static Text Row(DiscardedChange discarded, int width)
    {
        var time = discarded.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).Max(TimeWidth, true);
        var whatWidth = Math.Max(0, width - TimeWidth - (discarded.IsStash ? KindWidth : 0));
        var text = Text.Dark(time);
        if (discarded.IsStash)
            text.Cyan("Stash".Max(KindWidth, true));
        return text.White(discarded.What.Max(whatWidth, true)).ToText();
    }

    // What Restore does with the selected row, which the row cannot show
    public static string Reason(DiscardedChange discarded) =>
        discarded.IsStash
            ? "Restore puts the stash back in the list of stashes"
            : "Restore puts it back in the working tree, if its files are as the discard left them";

    public static string Restoring(DiscardedChange discarded) => $"Restoring {What(discarded)}";

    public static string Restored(DiscardedChange discarded) => $"Restored {What(discarded)}";

    // E.g. 'all changes', '3 lines of a.txt', 'the stash ...'
    static string What(DiscardedChange discarded) =>
        discarded.IsStash ? $"the stash '{discarded.What}'"
        : discarded.What.StartsWith("All ") ? $"a{discarded.What[1..]}"
        : discarded.What;
}
