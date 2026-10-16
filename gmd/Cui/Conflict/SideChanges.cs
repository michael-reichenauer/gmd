using DiffPlex;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

enum SideLineKind
{
    Same, // As the common ancestor has it
    Added, // A line the ancestor did not have
    Changed, // A line of the ancestor's with some of its words changed
    Removed, // A line of the ancestor's the side no longer has
}

// A line of a side of a conflict, as its change against the common ancestor: its text, the side's
// for all but a removed line, which is the ancestor's; the words of a changed line, which ones
// changed; and where it is in the side, -1 for a removed line
record SideLine(SideLineKind Kind, string Text, IReadOnlyList<WordRun> Words, int SideIndex);

// What a side of a conflict did to the common ancestor, which is what tells why the two sides
// conflict: e.g. one rewrote three lines and the other only added two above them. A line diff of the
// ancestor and the side, a removed line paired with the most alike added one, in order, and then
// compared word by word. The diff view pairs them by position, as a commit of part of a change
// does, but nothing is committed from these. No view, so it is unit testable.
static class SideChanges
{
    static readonly Differ differ = new();

    public static IReadOnlyList<SideLine> Of(IReadOnlyList<FileLine> ancestor, IReadOnlyList<FileLine> side)
    {
        var (old, now) = (ancestor.Select(l => l.Text).ToList(), side.Select(l => l.Text).ToList());
        var result = differ.CreateLineDiffs(string.Join('\n', old), string.Join('\n', now), false);
        if (old.Count == 0) // An empty text is one empty line to the differ
            return now.Select((t, i) => new SideLine(SideLineKind.Added, t, [], i)).ToList();
        if (now.Count == 0)
            return old.Select(t => new SideLine(SideLineKind.Removed, t, [], -1)).ToList();

        List<SideLine> lines = [];
        var (o, n) = (0, 0); // Where the ancestor and the side are up to
        foreach (var block in result.DiffBlocks)
        {
            for (; n < block.InsertStartB; o++, n++)
                lines.Add(new SideLine(SideLineKind.Same, now[n], [], n));

            // Each removed line paired with the most alike added line after the last one paired,
            // the added lines before it added, so that a line written above a changed one does not
            // take its place
            var next = block.InsertStartB;
            var end = block.InsertStartB + block.InsertCountB;
            for (int r = block.DeleteStartA; r < block.DeleteStartA + block.DeleteCountA; r++)
            {
                var match = Enumerable
                    .Range(next, end - next)
                    .Select(a => (Index: a, Words: LineWords.Of(old[r], now[a])))
                    .Where(m => m.Words.IsAlike)
                    .OrderByDescending(m => m.Words.Likeness)
                    .FirstOrDefault();
                if (match.Words == null)
                {
                    lines.Add(new SideLine(SideLineKind.Removed, old[r], [], -1));
                    continue;
                }

                for (; next < match.Index; next++)
                    lines.Add(new SideLine(SideLineKind.Added, now[next], [], next));
                lines.Add(new SideLine(SideLineKind.Changed, now[match.Index], match.Words.Right, match.Index));
                next = match.Index + 1;
            }
            for (; next < end; next++)
                lines.Add(new SideLine(SideLineKind.Added, now[next], [], next));

            (o, n) = (block.DeleteStartA + block.DeleteCountA, end);
        }
        for (; n < now.Count; n++)
            lines.Add(new SideLine(SideLineKind.Same, now[n], [], n));

        return lines;
    }
}
