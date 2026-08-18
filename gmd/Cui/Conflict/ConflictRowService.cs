using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

interface IConflictRowService
{
    ConflictRows ToRows(ConflictFile file, ConflictResolution resolution, bool isShowBase);
    Text ToRowText(ConflictRow row, ConflictColumns columns, int startX);
    Text ToHeader(ConflictFile file, ConflictResolution resolution, int currentHunk, GitOperation operation);
}

// Turns a conflicted file and what the user has decided about it into drawn rows.
//
// Pure: no view, no ContentView, no driver — so the whole layout is snapshot testable through
// Text.ToString(), which is how BlameService is tested.
class ConflictRowService : IConflictRowService
{
    const string PaneSeparator = "│";

    // The whole file is drawn, not just the conflicts: the text between them is what tells you what
    // the conflict is part of. ']' and '[' are what make a long file navigable.
    public ConflictRows ToRows(ConflictFile file, ConflictResolution resolution, bool isShowBase)
    {
        var rows = new ConflictRows();

        foreach (var segment in file.Segments)
        {
            if (segment.Hunk == null)
            {
                segment.Lines.ForEach(l => rows.Add(ConflictRow.Span(Text.White(l.Text))));
                continue;
            }

            AddHunk(rows, segment.Hunk, resolution, isShowBase);
        }

        if (rows.Count == 0)
            rows.Add(ConflictRow.Span(Text.Dark("<empty file>")));

        return rows;
    }

    static void AddHunk(ConflictRows rows, ConflictHunk hunk, ConflictResolution resolution, bool isShowBase)
    {
        var choice = resolution.ChoiceOf(hunk.Index);
        var title = Text.BrightMagenta($"─── Conflict {hunk.Index + 1} ").Add(ChoiceText(choice, hunk));
        rows.Add(ConflictRow.Header(title, hunk.Index));

        // The names git wrote into the markers, not 'ours' and 'theirs' — during a rebase those two
        // mean the opposite of what is expected, and these say which side is really which
        var labels = new List<Text> { Text.Yellow(hunk.OursLabel.Max(60)), Text.Cyan(hunk.TheirsLabel.Max(60)) };

        // The base slot is drawn whenever the pane is on, even for a conflict whose ancestor is
        // empty — both sides added lines where there were none. Leaving it out for that one hunk
        // would put its 'theirs' under the ancestor column, since the column widths are worked out
        // once for the whole view rather than per conflict.
        if (isShowBase)
            labels.Insert(1, Text.Dark(BaseLabelOf(hunk)));

        rows.Add(new ConflictRow(labels, ConflictRowMode.Panes, hunk.Index));

        var sides = new List<IReadOnlyList<FileLine>> { hunk.Ours, hunk.Theirs };
        var colors = new List<Color> { Color.Yellow, Color.Cyan };
        if (isShowBase)
        {
            sides.Insert(1, hunk.Base);
            colors.Insert(1, Color.Dark);
        }

        var height = sides.Max(s => s.Count);
        for (int i = 0; i < height; i++)
        {
            var panes = sides
                .Select((s, p) => (Text)(i < s.Count ? Text.Color(colors[p], s[i].Text) : Text.Dark("")))
                .ToList();
            rows.Add(new ConflictRow(panes, ConflictRowMode.Panes, hunk.Index));
        }
    }

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

    static Text ChoiceText(HunkChoice choice, ConflictHunk hunk) =>
        choice switch
        {
            HunkChoice.None => Text.Red("── unresolved ───────"),
            HunkChoice.Ours => Text.Green($"── using {hunk.OursLabel.Max(30)} "),
            HunkChoice.Theirs => Text.Green($"── using {hunk.TheirsLabel.Max(30)} "),
            HunkChoice.OursThenTheirs => Text.Green(
                $"── using {hunk.OursLabel.Max(20)} then {hunk.TheirsLabel.Max(20)} "
            ),
            HunkChoice.TheirsThenOurs => Text.Green(
                $"── using {hunk.TheirsLabel.Max(20)} then {hunk.OursLabel.Max(20)} "
            ),
            // Not the ancestor's own label, which is 'base' for a recovered one and whatever git
            // wrote for a diff3 one — neither says what taking it means the way these words do
            HunkChoice.Base => Text.Green("── using the common ancestor "),
            HunkChoice.Manual => Text.Green("── edited by hand "),
            _ => Text.Dark(""),
        };

    // One drawn line: the panes side by side, or one text across the whole width
    public Text ToRowText(ConflictRow row, ConflictColumns columns, int startX)
    {
        if (row.Mode == ConflictRowMode.Divider)
            return row.Panes[0].ToLine(columns.TotalWidth);

        // Not padded: only the panes need to be, so their separators line up
        if (row.Mode == ConflictRowMode.SpanAll)
            return Scroll(row.Panes[0], startX, columns.TotalWidth, isPad: false);

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
            text.Add(Scroll(pane, startX, columns.Pane));
        }

        return text;
    }

    // Both sides scroll together, as the diff view's two columns do. 'isFillRest' pads to the pane
    // width so the separators of every row line up — note ToLine is not that, it repeats the first
    // character to draw a rule, which is what a Divider row wants and a pane very much does not.
    static Text Scroll(Text text, int startX, int width, bool isPad = true) =>
        width <= 0 ? Text.Empty : text.Subtext(startX, width, isFillRest: isPad);

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
