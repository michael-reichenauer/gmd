using DiffPlex;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

// Where a line of a combination came from: the common ancestor, kept by both, one side, or both,
// written by both or with words of both
enum LineOrigin
{
    Ancestor,
    Ours,
    Theirs,
    Both,
}

record CombinedLine(string Text, LineOrigin Origin);

// Both sides' changes to the common ancestor in one, which the resolver offers as Combine where
// they touch no word in common. Git's merge works in lines, and calls a conflict wherever the lines
// two sides changed touch, though most such changes are of other words or other lines, and both are
// wanted: the branch added a line above a return main changed, or main renamed a variable the
// branch then used.
//
// Each side is aligned with the ancestor line by line, as the resolver draws them (SideChanges): a
// line kept, changed, or removed, and lines added before each. Where only one side touched a line,
// its change is taken, and where both changed it, the line is combined word by word, as long as
// they changed other words. Lines both wrote in one place, a line one removed and the other changed,
// or a word both changed, have no one combination, and there is none. No view, so it is unit
// testable; what it makes is taken as the user's own text, as an edit by hand is.
static class ConflictCombine
{
    static readonly Differ differ = new();

    // The lines of both sides' changes in one, or null where they cannot be had together
    public static IReadOnlyList<CombinedLine>? Of(
        IReadOnlyList<FileLine> ancestor,
        IReadOnlyList<FileLine> ours,
        IReadOnlyList<FileLine> theirs
    )
    {
        var (o, t) = (Align(ancestor, ours), Align(ancestor, theirs));
        List<CombinedLine> lines = [];
        for (var i = 0; i <= ancestor.Count; i++)
        {
            // The lines each wrote before the ancestor's line, from one side, or the same from both
            var (oursAdded, theirsAdded) = (o.Added[i], t.Added[i]);
            if (oursAdded.Count > 0 && theirsAdded.Count > 0 && !oursAdded.SequenceEqual(theirsAdded))
                return null;
            var origin =
                oursAdded.Count > 0 && theirsAdded.Count > 0 ? LineOrigin.Both
                : oursAdded.Count > 0 ? LineOrigin.Ours
                : LineOrigin.Theirs;
            lines.AddRange((oursAdded.Count > 0 ? oursAdded : theirsAdded).Select(l => new CombinedLine(l, origin)));

            if (i == ancestor.Count)
                break;

            var line = Line(ancestor[i].Text, o.Lines[i], t.Lines[i]);
            if (ReferenceEquals(line, Conflicting))
                return null;
            if (line != null) // Not removed
                lines.Add(line);
        }

        return lines;
    }

    // What happened to a line of the ancestor in a side: kept, changed to a text, or removed
    enum Kind
    {
        Kept,
        Changed,
        Removed,
    }

    // A side as what it did to each line of the ancestor, and the lines it added before each, and
    // after the last
    record Alignment(IReadOnlyList<(Kind Kind, string Text)> Lines, IReadOnlyList<List<string>> Added);

    static Alignment Align(IReadOnlyList<FileLine> ancestor, IReadOnlyList<FileLine> side)
    {
        List<(Kind, string)> lines = [];
        var added = Enumerable.Range(0, ancestor.Count + 1).Select(_ => new List<string>()).ToList();
        foreach (var line in SideChanges.Of(ancestor, side))
        {
            switch (line.Kind)
            {
                case SideLineKind.Added:
                    added[lines.Count].Add(line.Text);
                    break;
                case SideLineKind.Changed:
                    lines.Add((Kind.Changed, line.Text));
                    break;
                case SideLineKind.Removed:
                    lines.Add((Kind.Removed, ""));
                    break;
                default:
                    lines.Add((Kind.Kept, line.Text));
                    break;
            }
        }
        return new Alignment(lines, added);
    }

    static readonly CombinedLine Conflicting = new("", LineOrigin.Both);

    // A line of the ancestor as both sides leave it: null when removed, Conflicting when they cannot
    // both be had
    static CombinedLine? Line(string ancestor, (Kind Kind, string Text) ours, (Kind Kind, string Text) theirs) =>
        (ours.Kind, theirs.Kind) switch
        {
            (Kind.Kept, Kind.Kept) => new CombinedLine(ancestor, LineOrigin.Ancestor),
            (Kind.Kept, Kind.Changed) => new CombinedLine(theirs.Text, LineOrigin.Theirs),
            (Kind.Changed, Kind.Kept) => new CombinedLine(ours.Text, LineOrigin.Ours),
            (Kind.Removed, Kind.Removed) or (Kind.Kept, Kind.Removed) or (Kind.Removed, Kind.Kept) => null,
            (Kind.Changed, Kind.Changed) when ours.Text == theirs.Text => new CombinedLine(ours.Text, LineOrigin.Both),
            (Kind.Changed, Kind.Changed) => Words(ancestor, ours.Text, theirs.Text) is string text
                ? new CombinedLine(text, LineOrigin.Both)
                : Conflicting,
            _ => Conflicting, // Removed by one and changed by the other
        };

    // A line both sides changed, combined word by word: each side's changes to the ancestor's words,
    // which are taken together where they are of other words. A side's words written right before or
    // after the other's change go there; written inside it, they have no place left.
    static string? Words(string ancestor, string ours, string theirs)
    {
        var pieces = LineWords.Chunker.Chunk(ancestor);
        var edits = Edits(ancestor, ours).Concat(Edits(ancestor, theirs)).Distinct().ToList();
        foreach (var a in edits)
        {
            foreach (var b in edits.Where(b => b != a))
            {
                var isOverlapping = Math.Max(a.Start, b.Start) < Math.Min(a.End, b.End);
                var isSamePlace = a.Start == a.End && b.Start == b.End && a.Start == b.Start;
                var isInside = a.Start == a.End && b.Start < a.Start && a.Start < b.End;
                if (isOverlapping || isSamePlace || isInside)
                    return null;
            }
        }

        // Applied in order, words written before a change at the place it starts
        var text = new System.Text.StringBuilder();
        var at = 0;
        foreach (var edit in edits.OrderBy(e => e.Start).ThenBy(e => e.End - e.Start))
        {
            for (; at < edit.Start; at++)
                text.Append(pieces[at]);
            text.Append(edit.Text);
            at = edit.End;
        }
        for (; at < pieces.Count; at++)
            text.Append(pieces[at]);
        return text.ToString();
    }

    // A change of the ancestor's words: the ones from Start to End, before End, replaced by Text; none
    // replaced, Start and End the same, is words written there
    record Edit(int Start, int End, string Text);

    static IEnumerable<Edit> Edits(string ancestor, string side)
    {
        var result = differ.CreateDiffs(ancestor, side, false, false, LineWords.Chunker);
        return result.DiffBlocks.Select(b => new Edit(
            b.DeleteStartA,
            b.DeleteStartA + b.DeleteCountA,
            string.Concat(result.PiecesNew.Skip(b.InsertStartB).Take(b.InsertCountB))
        ));
    }
}
