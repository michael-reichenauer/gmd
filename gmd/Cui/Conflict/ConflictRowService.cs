using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

interface IConflictRowService
{
    ConflictRows ToRows(ConflictFile file, ConflictResolution resolution, bool isShowBase, ConflictNames? names = null);
    Text ToRowText(ConflictRow row, ConflictColumns columns, int startX);
    Text ToHeader(ConflictFile file, ConflictResolution resolution, int currentHunk, GitOperation operation);
    ConflictResult ToResult(
        ConflictFile file,
        ConflictResolution resolution,
        ConflictNames names,
        Func<ConflictHunk, bool>? canCombine = null
    );
}

// The file as it will be saved, as rows, and the rows of each conflict's lines in it: where they
// start, and where they end, the row after them. Code is the line of the file each row is, as drawn
// after its gutter, null for a row that is none, which its code is colored as (ConflictSyntax).
record ConflictResult(
    IReadOnlyList<Text> Rows,
    IReadOnlyDictionary<int, int> Starts,
    IReadOnlyDictionary<int, int> Ends,
    IReadOnlyList<string?> Code
)
{
    public static readonly ConflictResult None = new([], new Dictionary<int, int>(), new Dictionary<int, int>(), []);

    // The rows of the result at a place in the file, where the cursor is above: a conflict's lines,
    // or its one row while undecided, and a line of the text between conflicts, the same line, which
    // is as many rows after the conflict before it in the result as in the rows above; (0, 0) when
    // there is nothing
    public (int Start, int End) RangeOf(ConflictAnchor anchor)
    {
        if (Rows.Count == 0)
            return (0, 0);
        if (anchor.IsInHunk)
            return Starts.TryGetValue(anchor.HunkIndex, out var start) ? (start, Ends[anchor.HunkIndex]) : (0, 0);

        var after = anchor.HunkIndex == -1 ? 0 : Ends.GetValueOrDefault(anchor.HunkIndex);
        var row = Math.Min(after + anchor.Offset, Rows.Count - 1);
        return (row, row + 1);
    }
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
                foreach (var l in segment.Lines)
                {
                    var line = new PaneLine(CodeVersion.Ours, ours++);
                    var text = Gutter(" ", Color.Dark, line.LineNbr).White(Shown(l));
                    rows.Add(ConflictRow.Span(text, gutterWidth: GutterWidth, line: line));
                }
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
    internal const int GutterWidth = 6;

    // The cursor on a row as the diff has it: the line number in the color, black on white in the
    // file above and white on gray in the result below, the mark beside it and the line itself as
    // they are; a row of no line number, e.g. a conflict's header, highlighted whole
    internal static Text WithCursor(Text row, int gutterWidth, Color color) =>
        gutterWidth == 0
            ? row.ToHighlight()
            : row.Subtext(0, 1)
                .ToTextBuilder()
                .Color(color, row.Subtext(1, gutterWidth - 1, isFillRest: true).ToString())
                .Add(row.Subtext(gutterWidth, Math.Max(0, row.Length - gutterWidth)));

    internal static readonly Color CursorColor = new(ColorName.Black, ColorName.White);
    internal static readonly Color ResultCursorColor = new(ColorName.White, ColorName.Dark);

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
        var isCombined = resolution.CombinedOf(hunk.Index) != null;
        // Its line is drawn on to the end of the row, in the same color, as the row is drawn
        var title = Text.BrightMagenta($"─── Conflict {hunk.Index + 1} ── ").Add(ChoiceText(choice, names, isCombined));
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
        var sides = new List<IReadOnlyList<(Text Text, PaneLine Line)>>
        {
            SideTexts(hunk.Ours, hunk.Base, hasAncestor, oursStart, Color.Yellow, OursWords, CodeVersion.Ours),
            SideTexts(hunk.Theirs, hunk.Base, hasAncestor, theirsStart, Color.Cyan, TheirsWords, CodeVersion.Theirs),
        };
        if (isShowBase)
            sides.Insert(
                1,
                hunk.Base.Select(l => ((Text)Text.Black(new string(' ', GutterWidth)).Dark(Shown(l)), PaneLine.None))
                    .ToList()
            );

        var height = sides.Max(s => s.Count);
        for (int i = 0; i < height; i++)
        {
            var panes = sides.Select(s => i < s.Count ? s[i].Text : Text.Empty).ToList();
            var lines = sides.Select(s => i < s.Count ? s[i].Line : PaneLine.None).ToList();
            rows.Add(new ConflictRow(panes, ConflictRowMode.Panes, hunk.Index, GutterWidth: GutterWidth, Lines: lines));
        }
    }

    // The words a side changed, on the side's own color
    static readonly Color OursWords = new(ColorName.Black, ColorName.Yellow);
    static readonly Color TheirsWords = new(ColorName.Black, ColorName.Cyan);

    // The lines of a side, each with its gutter: as its change against the ancestor, '+' a line it
    // added, '~' a line it changed, its words marked, '-' a line of the ancestor's it removed, dark,
    // and a line it kept dark too, so that what it did stands out; or with no ancestor known, as they
    // are, in the side's color. Each with the line of the side's version of the file it is, but for a
    // line the side removed.
    static IReadOnlyList<(Text Text, PaneLine Line)> SideTexts(
        IReadOnlyList<FileLine> side,
        IReadOnlyList<FileLine> ancestor,
        bool hasAncestor,
        int start,
        Color color,
        Color words,
        CodeVersion version
    )
    {
        if (!hasAncestor)
            return side.Select(
                    (l, i) =>
                        ((Text)Gutter(" ", color, start + i).Color(color, Shown(l)), new PaneLine(version, start + i))
                )
                .ToList();

        return SideChanges
            .Of(ancestor, side)
            .Select(l =>
                (
                    (Text)(
                        l.Kind switch
                        {
                            SideLineKind.Added => Gutter("+", color, start + l.SideIndex).White(Shown(l.Text)),
                            SideLineKind.Changed => Gutter("~", color, start + l.SideIndex).Add(Words(l.Words, words)),
                            SideLineKind.Removed => Text.Red("-").Dark($"{"", 4} ").Dark(Shown(l.Text)),
                            _ => Gutter(" ", color, start + l.SideIndex).Dark(Shown(l.Text)),
                        }
                    ),
                    l.Kind == SideLineKind.Removed ? PaneLine.None : new PaneLine(version, start + l.SideIndex)
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

    static Text ChoiceText(HunkChoice choice, ConflictNames names, bool isCombined) =>
        choice switch
        {
            HunkChoice.Manual when isCombined => Text.Green("both changes combined"),
            HunkChoice.None => Text.Red("unresolved"),
            HunkChoice.Ours => Text.Green($"using {names.Ours.Max(30)}"),
            HunkChoice.Theirs => Text.Green($"using {names.Theirs.Max(30)}"),
            HunkChoice.OursThenTheirs => Text.Green($"using {names.Ours.Max(20)} then {names.Theirs.Max(20)}"),
            HunkChoice.TheirsThenOurs => Text.Green($"using {names.Theirs.Max(20)} then {names.Ours.Max(20)}"),
            // Not the ancestor's own label, which is 'base' for a recovered one and whatever git
            // wrote for a diff3 one — neither says what taking it means the way these words do
            HunkChoice.Base => Text.Green("using the common ancestor"),
            HunkChoice.Manual => Text.Green("edited by hand"),
            _ => Text.Dark(""),
        };

    // One drawn line: the panes side by side, or one text across the whole width
    public Text ToRowText(ConflictRow row, ConflictColumns columns, int startX)
    {
        // Not padded: only the panes need to be, so their separators line up. A conflict's header has
        // its line drawn on to the end of the row, which the panes may stop a column or two short of.
        if (row.Mode == ConflictRowMode.SpanAll)
        {
            var span = Scroll(row.Panes[0], startX, columns.TotalWidth, row.GutterWidth, isPad: false);
            var width = Math.Max(columns.Width, columns.TotalWidth);
            return row.IsHunkHeader && span.Length + 1 < width
                ? span.ToTextBuilder().BrightMagenta(" " + new string('─', width - span.Length - 1))
                : span;
        }

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

    // The file as it will be saved, as the decisions so far make it: the text between conflicts, each
    // decided conflict's lines with a bar in the color of the side they came from, magenta where
    // written by hand, and each undecided one a row saying so, which a save leaves the conflict's
    // markers in place of. Numbered as the file will be, but for those.
    //
    // canCombine says of an undecided conflict whether Combine can be had, which its row then offers.
    public ConflictResult ToResult(
        ConflictFile file,
        ConflictResolution resolution,
        ConflictNames names,
        Func<ConflictHunk, bool>? canCombine = null
    )
    {
        List<Text> rows = [];
        List<string?> code = [];
        Dictionary<int, int> starts = [];
        Dictionary<int, int> ends = [];
        var nbr = 1;
        void AddLine(TextBuilder gutter, FileLine line)
        {
            rows.Add(gutter.White(Shown(line)));
            code.Add(Shown(line));
        }
        void AddNoLine(Text row)
        {
            rows.Add(row);
            code.Add(null);
        }

        foreach (var segment in file.Segments)
        {
            if (segment.Hunk is not ConflictHunk hunk)
            {
                segment.Lines.ForEach(l => AddLine(Gutter(" ", Color.Dark, nbr++), l));
                continue;
            }

            starts[hunk.Index] = rows.Count;
            var choice = resolution.ChoiceOf(hunk.Index);
            if (choice == HunkChoice.None)
                AddNoLine(Undecided(hunk, names, canCombine?.Invoke(hunk) ?? false));
            else
            {
                var lines = ResultLines(hunk, choice, resolution);
                if (lines.Count == 0)
                    AddNoLine(Text.Color(Color.Magenta, "▌").Dark("      nothing, the region is removed"));
                lines.ForEach(l => AddLine(Gutter("▌", l.Color, nbr++), l.Line));
            }
            ends[hunk.Index] = rows.Count;
        }

        return new ConflictResult(rows, starts, ends, code);
    }

    // The lines a decision takes, each with the color of where it came from
    static IReadOnlyList<(FileLine Line, Color Color)> ResultLines(
        ConflictHunk hunk,
        HunkChoice choice,
        ConflictResolution resolution
    )
    {
        IEnumerable<(FileLine, Color)> Of(IReadOnlyList<FileLine> lines, Color color) => lines.Select(l => (l, color));
        var (ours, theirs) = (Of(hunk.Ours, Color.Yellow), Of(hunk.Theirs, Color.Cyan));
        if (resolution.CombinedOf(hunk.Index) is IReadOnlyList<CombinedLine> combined)
            return combined.Select(l => (new FileLine(l.Text), ColorOf(l.Origin))).ToList();
        return choice switch
        {
            HunkChoice.Ours => ours.ToList(),
            HunkChoice.Theirs => theirs.ToList(),
            HunkChoice.OursThenTheirs => ours.Concat(theirs).ToList(),
            HunkChoice.TheirsThenOurs => theirs.Concat(ours).ToList(),
            HunkChoice.Base => Of(hunk.Base, Color.Dark).ToList(),
            _ => Of(resolution.ResultOf(hunk), Color.Magenta).ToList(),
        };
    }

    // A combined line in the color of where it came from, a line with words of both magenta
    static Color ColorOf(LineOrigin origin) =>
        origin switch
        {
            LineOrigin.Ours => Color.Yellow,
            LineOrigin.Theirs => Color.Cyan,
            LineOrigin.Both => Color.Magenta,
            _ => Color.Dark,
        };

    static Text Undecided(ConflictHunk hunk, ConflictNames names, bool canCombine) =>
        Text.Red("▌")
            .Dark("      ")
            .Red($"Conflict {hunk.Index + 1} is not decided:")
            .Dark(" 1 ")
            .Yellow(names.Ours.Max(20))
            .Dark(", 2 ")
            .Cyan(names.Theirs.Max(20))
            .Dark(canCombine ? ", c combine both" : "")
            .Dark(", 3 or 4 both, 0 neither, e edit");

    // 'Resolve  src/Foo.cs   conflict 2 of 5   ·  3 still to resolve'
    public Text ToHeader(ConflictFile file, ConflictResolution resolution, int currentHunk, GitOperation operation)
    {
        var text = Text.Dark($"{Title(operation)}  ").White(file.Path);

        if (resolution.HunkCount > 0)
            text.Dark("   conflict ").Cyan($"{currentHunk + 1}").Dark($" of {resolution.HunkCount}");

        var left = resolution.UnresolvedCount;
        text.Dark("   ").Add(left == 0 ? Text.Green("all resolved") : Text.Red($"{left} still to resolve"));

        return text;
    }

    // What the view does, and the operation it does it for, as gmd names it elsewhere: the operation
    // alone, e.g. 'Merge', read as if the view were the merge rather than the resolver of its conflicts
    static string Title(GitOperation operation) =>
        operation == GitOperation.None ? "Resolve conflicts" : $"Resolve conflicts: {OperationText.Name(operation)}";
}
