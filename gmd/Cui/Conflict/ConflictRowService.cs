using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

interface IConflictRowService
{
    ConflictRows ToRows(ConflictFile file, ConflictResolution resolution, bool isShowBase, ConflictNames? names = null);
    Text ToRowText(ConflictRow row, ConflictColumns columns, int startX);
    Text ToHeader(ConflictFile file, ConflictResolution resolution, int currentHunk, GitOperation operation);
}

// Turns a conflicted file and what the user has decided about it into drawn rows.
//
// Pure: no view, no ContentView, no driver — so the whole layout is snapshot testable through
// Text.ToString(), which is how BlameRowService is tested.
class ConflictRowService : IConflictRowService
{
    const string PaneSeparator = "│";

    // The whole file is drawn, not just the conflicts: the text between them is what tells you what
    // the conflict is part of. ']' and '[' are what make a long file navigable.
    //
    // Numbered as each side has it: the text between conflicts and the left side as ours, the right
    // side as theirs, each counting its own lines of the conflicts before.
    public ConflictRows ToRows(
        ConflictFile file,
        ConflictResolution resolution,
        bool isShowBase,
        ConflictNames? names = null
    )
    {
        names ??= ConflictNames.Of(file, ConflictSides.None);
        var rows = new ConflictRows();
        var (ours, theirs) = (1, 1); // The line numbers the next line has in each side

        foreach (var segment in file.Segments)
        {
            if (segment.Hunk == null)
            {
                segment.Lines.ForEach(l =>
                    rows.Add(
                        ConflictRow.Span(Gutter(" ", Color.Dark, ours++).White(Shown(l)), gutterWidth: GutterWidth)
                    )
                );
                theirs += segment.Lines.Count;
                continue;
            }

            AddHunk(
                rows,
                segment.Hunk,
                resolution,
                names,
                isShowBase,
                file.HasBase || segment.Hunk.HasBase,
                ours,
                theirs
            );
            (ours, theirs) = (ours + segment.Hunk.Ours.Count, theirs + segment.Hunk.Theirs.Count);
        }

        if (rows.Count == 0)
            rows.Add(ConflictRow.Span(Text.Dark("<empty file>")));

        return rows;
    }

    // How wide a line's gutter is: its mark, its number in 4 and a space
    const int GutterWidth = 6;

    static void AddHunk(
        ConflictRows rows,
        ConflictHunk hunk,
        ConflictResolution resolution,
        ConflictNames names,
        bool isShowBase,
        bool hasAncestor,
        int oursStart,
        int theirsStart
    )
    {
        var choice = resolution.ChoiceOf(hunk.Index);
        var title = Text.BrightMagenta($"─── Conflict {hunk.Index + 1} ").Add(ChoiceText(choice, names));
        rows.Add(ConflictRow.Header(title, hunk.Index));

        // The sides by name, not 'ours' and 'theirs' — during a rebase those two mean the opposite of
        // what is expected, and the names say which side is really which — and by what they did
        var labels = new List<Text> { names.OursTitle, names.TheirsTitle };

        // The base slot is drawn whenever the pane is on, even for a conflict whose ancestor is
        // empty — both sides added lines where there were none. Leaving it out for that one hunk
        // would put its 'theirs' under the ancestor column, since the column widths are worked out
        // once for the whole view rather than per conflict.
        if (isShowBase)
            labels.Insert(1, Text.Dark(BaseLabelOf(hunk)));

        rows.Add(new ConflictRow(labels, ConflictRowMode.Panes, hunk.Index));

        // Each side as what it did to the common ancestor, which is what says why they conflict, when
        // the ancestor is known; else as it is, in the side's color
        var sides = new List<IReadOnlyList<Text>>
        {
            SideTexts(hunk.Ours, hunk.Base, hasAncestor, oursStart, Color.Yellow, OursWords),
            SideTexts(hunk.Theirs, hunk.Base, hasAncestor, theirsStart, Color.Cyan, TheirsWords),
        };
        if (isShowBase)
            sides.Insert(
                1,
                hunk.Base.Select(l => (Text)Text.Black(new string(' ', GutterWidth)).Dark(Shown(l))).ToList()
            );

        var height = sides.Max(s => s.Count);
        for (int i = 0; i < height; i++)
        {
            var panes = sides.Select(s => i < s.Count ? s[i] : Text.Empty).ToList();
            rows.Add(new ConflictRow(panes, ConflictRowMode.Panes, hunk.Index, GutterWidth: GutterWidth));
        }
    }

    // The words a side changed, on the side's own color
    static readonly Color OursWords = new(Terminal.Gui.Color.Black, Terminal.Gui.Color.Brown);
    static readonly Color TheirsWords = new(Terminal.Gui.Color.Black, Terminal.Gui.Color.Cyan);

    // The lines of a side, each with its gutter: as its change against the ancestor, '+' a line it
    // added, '~' a line it changed, its words marked, '-' a line of the ancestor's it removed, dark,
    // and a line it kept dark too, so that what it did stands out; or with no ancestor known, as they
    // are, in the side's color
    static IReadOnlyList<Text> SideTexts(
        IReadOnlyList<FileLine> side,
        IReadOnlyList<FileLine> ancestor,
        bool hasAncestor,
        int start,
        Color color,
        Color words
    )
    {
        if (!hasAncestor)
            return side.Select((l, i) => (Text)Gutter(" ", color, start + i).Color(color, Shown(l))).ToList();

        return SideChanges
            .Of(ancestor, side)
            .Select(l =>
                (Text)(
                    l.Kind switch
                    {
                        SideLineKind.Added => Gutter("+", color, start + l.SideIndex).White(Shown(l.Text)),
                        SideLineKind.Changed => Gutter("~", color, start + l.SideIndex).Add(Words(l.Words, words)),
                        SideLineKind.Removed => Text.Red("-").Dark($"{"", 4} ").Dark(Shown(l.Text)),
                        _ => Gutter(" ", color, start + l.SideIndex).Dark(Shown(l.Text)),
                    }
                )
            )
            .ToList();
    }

    static TextBuilder Gutter(string mark, Color markColor, int lineNbr) =>
        Text.Color(markColor, mark).Dark($"{lineNbr, 4} ");

    static Text Words(IReadOnlyList<WordRun> runs, Color changed)
    {
        var text = new TextBuilder();
        runs.ForEach(r => text.Color(r.IsChanged ? changed : Color.White, Shown(r.Text)));
        return text;
    }

    // A line as drawn, with a tab as three spaces, as the diff and blame draw it: a tab is one
    // character, which the terminal moves to its next tab stop, pushing the panes after it out of line
    public static string Shown(FileLine line) => Shown(line.Text);

    static string Shown(string text) => text.Replace("\t", "   ");

    static string BaseLabelOf(ConflictHunk hunk)
    {
        // Not 'no common ancestor': the file has one, or the pane would not be drawn at all — this
        // region simply had no lines in it, because both sides added lines where there were none.
        // Whether the file has an ancestor at all is the other question, and it is answered before
        // anything is drawn (ConflictView.WithBase).
        if (!hunk.HasBase)
            return "common ancestor (nothing here)";

        return hunk.BaseLabel == "" ? "common ancestor" : hunk.BaseLabel.Max(60);
    }

    static Text ChoiceText(HunkChoice choice, ConflictNames names) =>
        choice switch
        {
            HunkChoice.None => Text.Red("── unresolved ───────"),
            HunkChoice.Ours => Text.Green($"── using {names.Ours.Max(30)} "),
            HunkChoice.Theirs => Text.Green($"── using {names.Theirs.Max(30)} "),
            HunkChoice.OursThenTheirs => Text.Green($"── using {names.Ours.Max(20)} then {names.Theirs.Max(20)} "),
            HunkChoice.TheirsThenOurs => Text.Green($"── using {names.Theirs.Max(20)} then {names.Ours.Max(20)} "),
            // Not the ancestor's own label, which is 'base' for a recovered one and whatever git
            // wrote for a diff3 one — neither says what taking it means the way these words do
            HunkChoice.Base => Text.Green("── using the common ancestor "),
            HunkChoice.Manual => Text.Green("── edited by hand "),
            _ => Text.Dark(""),
        };

    // One drawn line: the panes side by side, or one text across the whole width
    public Text ToRowText(ConflictRow row, ConflictColumns columns, int startX)
    {
        // Not padded: only the panes need to be, so their separators line up
        if (row.Mode == ConflictRowMode.SpanAll)
            return Scroll(row.Panes[0], startX, columns.TotalWidth, row.GutterWidth, isPad: false);

        // The rows are built once, from what the user asked for, but the widths are worked out per
        // draw and may have dropped the ancestor because the view is too narrow for three. When they
        // disagree it is the *middle* pane that goes — the ancestor is what the step-down drops.
        // Taking the first n panes instead would drop 'theirs', losing a whole side of the conflict.
        var panes = row.Panes;
        if (panes.Count > columns.PaneCount)
            panes = [panes[0], .. panes.Skip(panes.Count - (columns.PaneCount - 1))];

        var text = new TextBuilder();
        for (int p = 0; p < columns.PaneCount; p++)
        {
            if (p > 0)
                text.Dark(PaneSeparator);

            var pane = p < panes.Count ? panes[p] : Text.Empty;
            text.Add(Scroll(pane, startX, columns.Pane, row.GutterWidth));
        }

        return text;
    }

    // Both sides scroll together, as the diff view's two columns do, the gutter staying where it is.
    // 'isFillRest' pads to the pane width so the separators of every row line up (ToLine is not that:
    // it repeats the first character, to draw a rule).
    static Text Scroll(Text text, int startX, int width, int gutterWidth, bool isPad = true)
    {
        if (width <= 0)
            return Text.Empty;
        if (gutterWidth == 0 || startX == 0)
            return text.Subtext(startX, width, isFillRest: isPad);

        var gutter = Math.Min(gutterWidth, width);
        return text.Subtext(0, gutter, isFillRest: true)
            .ToTextBuilder()
            .Add(text.Subtext(gutterWidth + startX, width - gutter, isFillRest: isPad));
    }

    // 'Resolve  src/Foo.cs   conflict 2 of 5   ·  3 still to resolve'
    public Text ToHeader(ConflictFile file, ConflictResolution resolution, int currentHunk, GitOperation operation)
    {
        var text = Text.Dark($"{OperationName(operation)}  ").White(file.Path);

        if (resolution.HunkCount > 0)
            text.Dark("   conflict ").Cyan($"{currentHunk + 1}").Dark($" of {resolution.HunkCount}");

        var left = resolution.UnresolvedCount;
        text.Dark("   ").Add(left == 0 ? Text.Green("all resolved") : Text.Red($"{left} still to resolve"));

        return text;
    }

    static string OperationName(GitOperation operation) =>
        operation switch
        {
            GitOperation.Merge => "Merge",
            GitOperation.CherryPick => "Cherry pick",
            GitOperation.Revert => "Revert",
            GitOperation.Rebase => "Rebase",
            GitOperation.Am => "Apply patches",
            _ => "Resolve",
        };
}
