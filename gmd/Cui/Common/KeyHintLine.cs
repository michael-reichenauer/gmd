namespace gmd.Cui.Common;

record KeyHint(string Key, string Text);

// A hint with no key, naming what the keys act on, drawn in the colors it is given
record LabelHint(Text Label) : KeyHint("", Label.ToString());

// The key-hint line at the bottom of a view: the few keys that do something useful there, so that
// the keys are learned by using gmd rather than by reading the help first. The log view's are in
// KeyHints, and the other views have theirs beside them (DiffHints, BlameHints, ConflictHints,
// StartHints); this is the line they are drawn on, by KeyHintBar, and the status message drawn in
// their place for a few seconds, see StatusLine.
//
// A key is written the way it is typed, and a shifted one with '⇧', so '⇧p' is Shift-P, which is a
// different command from 'p'. The hints are listed most useful first, which is also the order they
// are dropped in, from the end, when the line is too narrow for all of them; '? help' is always
// kept, at the right.
//
// The line is drawn as the view's bottom border, in the color of the line under the application
// bar, with the hints set into it, so that it reads as the frame of the view and not as one more row
// of it, which it did when it was the hints on their own.
//
// The layout alone, with no view, so it is tested without a terminal.
static class KeyHintLine
{
    const string Gap = "  ";
    public static readonly KeyHint Help = new("?", "help");
    public static readonly KeyHint Menu = new("m", "menu");

    // The border runs into the line for this long before the hints, and after the help. The lead is
    // long enough that the hints start clearly inside a line, so the row reads as a border rather
    // than as one more row of the view.
    const string Lead = "──────────";
    const string Tail = "──";

    // The hints on one line of the given width, set into the border: as many as fit, from the left,
    // and help at the right, with the border between them
    //
    //   ────────── m menu  d diff  f search ──────── ? help ──
    public static Text ToText(IReadOnlyList<KeyHint> hints, int width)
    {
        // The border and its spaces around the hints, the help and the border around it
        var frame = Lead.Length + 1 + 1 + 1 + 1 + Length(Help) + 1 + Tail.Length;
        var shown = hints.ToList();
        while (shown.Count > 0 && frame + Length(shown) > width)
            shown.RemoveAt(shown.Count - 1);

        var text = new TextBuilder().Color(Color.BrightMagenta, Lead);
        if (shown.Count > 0)
        {
            text.Dark(" ");
            for (int i = 0; i < shown.Count; i++)
                Add(i == 0 ? text : text.Dark(Gap), shown[i]);
            text.Dark(" ");
        }

        var border = width - text.Length - 1 - Length(Help) - 1 - Tail.Length;
        if (border < 0)
            return text; // Not even the help fits

        text.Color(Color.BrightMagenta, new string('─', border)).Dark(" ");
        Add(text, Help);
        return text.Dark(" ").Color(Color.BrightMagenta, Tail);
    }

    // A status message, in place of the hints and set into the border the same way: green for what a
    // command did, yellow for why a key did nothing, red for what failed in the background, and cyan
    // for what a command is doing while it runs. Cut to the line if it is longer.
    //
    //   ────────── Pushed 'main' ─────────────────────────────
    public static Text ToText(StatusMessage message, int width)
    {
        var room = Math.Max(0, width - Lead.Length - 2 - Tail.Length);
        var text = message.Text.ReplaceLineEndings(" ");
        text = text.Length > room ? text[..Math.Max(0, room - 1)] + "┅" : text;
        var color = message.Kind switch
        {
            StatusKind.Info => Color.Green,
            StatusKind.Notice => Color.Yellow,
            StatusKind.Progress => Color.Cyan,
            _ => Color.BrightRed,
        };

        var border = Math.Max(0, width - Lead.Length - 2 - text.Length);
        return new TextBuilder()
            .Color(Color.BrightMagenta, Lead)
            .Dark(" ")
            .Color(color, text)
            .Dark(" ")
            .Color(Color.BrightMagenta, new string('─', border));
    }

    static void Add(TextBuilder text, KeyHint hint)
    {
        if (hint is LabelHint label)
            text.Add(label.Label);
        else
            text.Cyan(hint.Key).Dark($" {hint.Text}");
    }

    static int Length(KeyHint hint) => hint.Key == "" ? hint.Text.Length : hint.Key.Length + 1 + hint.Text.Length;

    static int Length(IReadOnlyList<KeyHint> hints) => hints.Sum(Length) + Math.Max(0, hints.Count - 1) * Gap.Length;
}
