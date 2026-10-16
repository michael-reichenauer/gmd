using DiffPlex;

namespace gmd.Cui.Diff;

// A run of a line's text, changed or as it is on the other side
record WordRun(string Text, bool IsChanged);

// The words two lines of a change differ in, as the diff marks them: a line compared word by word
// rather than character by character, whose marks fell on scattered letters of a changed word
// ('const' to 'readonly' marked 'co' and 'st' apart). No view, so it is unit testable.
record LineWords(IReadOnlyList<WordRun> Left, IReadOnlyList<WordRun> Right, double Likeness)
{
    // How much of two lines has to be the same for their words to be marked: below it the lines are
    // mostly rewritten, and marking what little they share reads as noise rather than as a diff
    public const double MinLikeness = 0.5;

    public bool IsAlike => Likeness >= MinLikeness;

    static readonly Differ differ = new();
    static readonly WordChunker chunker = new();

    // The two lines word by word. With whitespace ignored, a run of it that differs is not changed.
    // Likeness is the share of the two lines' text, not counting whitespace, that is the same on both.
    public static LineWords Of(string left, string right, bool isIgnoreWhitespace = false)
    {
        var result = differ.CreateDiffs(left, right, isIgnoreWhitespace, false, chunker);
        var leftRuns = Runs(result.PiecesOld, result.DiffBlocks.Select(b => (b.DeleteStartA, b.DeleteCountA)));
        var rightRuns = Runs(result.PiecesNew, result.DiffBlocks.Select(b => (b.InsertStartB, b.InsertCountB)));
        if (isIgnoreWhitespace)
        {
            leftRuns = leftRuns.Select(r => r.IsChanged && r.Text.Trim() == "" ? r with { IsChanged = false } : r);
            rightRuns = rightRuns.Select(r => r.IsChanged && r.Text.Trim() == "" ? r with { IsChanged = false } : r);
        }

        var (leftList, rightList) = (Tidied(Joined(leftRuns)), Tidied(Joined(rightRuns)));
        var total = Visible(leftList, _ => true) + Visible(rightList, _ => true);
        var same = Visible(leftList, r => !r.IsChanged) + Visible(rightList, r => !r.IsChanged);
        return new LineWords(leftList, rightList, total == 0 ? 1 : (double)same / total);
    }

    // The pieces, each changed when a block of the diff covers it
    static IEnumerable<WordRun> Runs(IReadOnlyList<string> pieces, IEnumerable<(int Start, int Count)> blocks)
    {
        var changed = new bool[pieces.Count];
        foreach (var (start, count) in blocks)
        {
            for (var i = start; i < start + count && i < pieces.Count; i++)
                changed[i] = true;
        }

        return pieces.Select((p, i) => new WordRun(p, changed[i]));
    }

    // Neighbouring pieces alike in being changed or not as one run, the fewer for the view to draw
    static IReadOnlyList<WordRun> Joined(IEnumerable<WordRun> runs)
    {
        List<WordRun> joined = [];
        foreach (var run in runs)
        {
            if (joined.Count > 0 && joined[^1].IsChanged == run.IsChanged)
                joined[^1] = joined[^1] with { Text = joined[^1].Text + run.Text };
            else
                joined.Add(run);
        }
        return joined;
    }

    // The differ matches a space wherever one serves as well as another, so a change came out split
    // around a space, and with a space it did not make marked at its edge. A space between two changes
    // is part of them, and a change of words starts and ends with a word; a change of space alone,
    // e.g. two spaces made one, stays marked.
    static IReadOnlyList<WordRun> Tidied(IReadOnlyList<WordRun> runs)
    {
        var bridged = runs.Select(
                (r, i) =>
                    !r.IsChanged
                    && IsSpace(r.Text)
                    && i > 0
                    && i < runs.Count - 1
                    && runs[i - 1].IsChanged
                    && runs[i + 1].IsChanged
                        ? r with
                        {
                            IsChanged = true,
                        }
                        : r
            )
            .ToList();

        List<WordRun> tidied = [];
        foreach (var run in Joined(bridged))
        {
            if (!run.IsChanged || IsSpace(run.Text))
            {
                tidied.Add(run);
                continue;
            }

            var text = run.Text.Trim();
            var start = run.Text.Length - run.Text.TrimStart().Length;
            tidied.Add(new WordRun(run.Text[..start], false));
            tidied.Add(new WordRun(text, true));
            tidied.Add(new WordRun(run.Text[(start + text.Length)..], false));
        }

        return Joined(tidied.Where(r => r.Text != ""));
    }

    static bool IsSpace(string text) => text.Trim() == "";

    static int Visible(IEnumerable<WordRun> runs, Func<WordRun, bool> isCounted) =>
        runs.Where(isCounted).Sum(r => r.Text.Count(c => !char.IsWhiteSpace(c)));

    // A line as words, letters and digits running together, as whitespace does, and every other
    // character on its own, so that 'Math.Round(sum' is five pieces and a change to one is one piece
    class WordChunker : IChunker
    {
        public IReadOnlyList<string> Chunk(string text)
        {
            List<string> pieces = [];
            var start = 0;
            for (var i = 1; i <= text.Length; i++)
            {
                if (i == text.Length || !IsSameKind(text[i - 1], text[i]))
                {
                    pieces.Add(text[start..i]);
                    start = i;
                }
            }
            return pieces;
        }

        static bool IsSameKind(char a, char b) =>
            (IsWord(a) && IsWord(b)) || (char.IsWhiteSpace(a) && char.IsWhiteSpace(b));

        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
