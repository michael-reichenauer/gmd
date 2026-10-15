using gmd.Git.Private;

namespace gmd.Git;

// Whether a line of a hunk is in both versions of the file, or in the old or the new one alone
public enum PatchLineKind
{
    Context,
    Removed,
    Added,
}

// A line of a hunk as git wrote it, its text without the ' ', '-' or '+' before it and without the
// '\n' that ends every line of a patch, so the '\r' of a file's '\r\n' is kept in it. Where the
// file has no newline at its end, git follows its last line with '\ No newline at end of file',
// which is kept with the line, NoNewline, so that it goes wherever the line goes.
//
// The line numbers are of the old and the new version, 0 on the side the line is not in, numbered
// as the diff view numbers its rows (DiffRow), which is what a choice of lines is matched by.
public record PatchLine(PatchLineKind Kind, string Text, int OldLineNbr, int NewLineNbr, string NoNewline = "")
{
    public bool IsChange => Kind != PatchLineKind.Context;

    public override string ToString() =>
        $"{(Kind == PatchLineKind.Context ? ' ' : Kind == PatchLineKind.Removed ? '-' : '+')}{Text}";
}

// One '@@ -a,b +c,d @@' region of a patch: where it starts and how many lines it has in the old and
// the new version, the lines, and what git writes after the second '@@', e.g. the function the lines
// are in, kept as it is
public record PatchHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    string Section,
    IReadOnlyList<PatchLine> Lines
)
{
    public override string ToString() => $"@@ -{OldStart},{OldCount} +{NewStart},{NewCount} @@";
}

// The patch of one file as 'git diff' writes it: the header lines as they are, from 'diff --git' to
// '+++', and the hunks. PatchParser reads it so that ToText gives back the very text it was read
// from, byte for byte, which is what lets a patch of some of its lines be applied by git: committing
// or discarding part of a file's changes, as 'git add -p' and 'git checkout -p' do.
public record FilePatch(IReadOnlyList<string> Header, IReadOnlyList<PatchHunk> Hunks)
{
    public bool IsNew => Header.Any(l => l.StartsWith("new file mode "));
    public bool IsDeleted => Header.Any(l => l.StartsWith("deleted file mode "));

    // No hunks, e.g. a patch of which no change was chosen, which there is nothing to apply of
    public bool IsEmpty => Hunks.Count == 0;

    public IEnumerable<PatchLine> Changes => Hunks.SelectMany(h => h.Lines).Where(l => l.IsChange);

    // Why lines of the patch cannot be chosen, the file taken whole only, or "" when they can be. A
    // binary file has no lines; a renamed file, a changed mode and a submodule's commit are changes
    // of the header, which a part of the lines would leave out or take with them unseen.
    public string WhyNotChoose =>
        Header.Any(l => l.StartsWith("Binary files ") || l.StartsWith("GIT binary patch")) ? "It is a binary file"
        : Header.Any(l => l.StartsWith("rename from ") || l.StartsWith("copy from ")) ? "It was renamed"
        : Header.Any(l => l.StartsWith("old mode ")) ? "Its mode changed too"
        : Header.Any(l => l.EndsWith(" 160000")) ? "It is a submodule" // Its mode, on 'index' or 'file mode'
        : IsEmpty ? "It has no lines that changed"
        : "";

    public string ToText() => PatchParser.ToText(this);

    // The patch of the chosen changes alone, to apply to the old version, e.g. an index of HEAD, which
    // then has those changes and none of the others: a removed line not chosen stays, as an unchanged
    // line, and an added line not chosen is left out. No change chosen is an empty patch.
    //
    // A change is its removed lines and then its added ones, as git writes it, and they are taken in
    // pairs, the first removed line with the first added one and so on, as the diff view shows them
    // side by side: a removed line that stays then stays where it is among the lines added. Taken as
    // git writes them, of '-two -three +TWO +three' with two made TWO alone, three would stay before
    // TWO and the file would read 'three TWO'.
    public Result<FilePatch> ForApply(Func<PatchLine, bool> isChosen) => Choose(isChosen, isReverse: false);

    // The patch of the chosen changes alone, to apply in reverse to the new version, e.g. the working
    // tree, which then has those changes taken back and keeps the others: an added line not chosen
    // stays, as an unchanged line, and a removed line not chosen is left out. Paired as ForApply pairs
    // them.
    public Result<FilePatch> ForReverseApply(Func<PatchLine, bool> isChosen) => Choose(isChosen, isReverse: true);

    Result<FilePatch> Choose(Func<PatchLine, bool> isChosen, bool isReverse)
    {
        if (WhyNotChoose != "")
            return new Error($"Its lines cannot be chosen: {WhyNotChoose.ToLowerInvariant()}");

        List<PatchHunk> hunks = [];
        var delta = 0; // The new version's lines less the old version's, of the hunks before
        foreach (var hunk in Hunks)
        {
            var lines = Chosen(hunk.Lines, isChosen, isReverse);
            if (!lines.Any(l => l.IsChange))
                continue; // Unchanged lines alone, the same number in both versions

            // The version the patch is applied to keeps where the hunk is, and the other one's start
            // follows from the lines the hunks before add or take away. A side with no lines starts
            // at the line before the hunk, as git counts it, e.g. '-0,0' for a new file.
            var oldCount = lines.Count(l => l.Kind != PatchLineKind.Added);
            var newCount = lines.Count(l => l.Kind != PatchLineKind.Removed);
            var (oldStart, newStart) = isReverse
                ? (Start(Before(hunk.NewStart, newCount) - delta, oldCount), hunk.NewStart)
                : (hunk.OldStart, Start(Before(hunk.OldStart, oldCount) + delta, newCount));
            delta += newCount - oldCount;

            hunks.Add(
                new PatchHunk(oldStart, oldCount, newStart, newCount, hunk.Section, Numbered(lines, oldStart, newStart))
            );
        }

        if (hunks.Count > 0 && !IsEndWhole(hunks[^1].Lines))
            return new Error(
                "The changes at the end of the file, whose last line has no newline, can only be chosen together"
            );

        return new FilePatch(ChosenHeader(hunks), hunks);
    }

    // The lines of a hunk with only the chosen changes: of each pair of a removed and an added line,
    // a line not chosen of the version the patch is applied to stays as it is there, unchanged, and
    // one of the other version is not made
    static List<PatchLine> Chosen(IReadOnlyList<PatchLine> lines, Func<PatchLine, bool> isChosen, bool isReverse)
    {
        List<PatchLine> chosen = [];
        for (var i = 0; i < lines.Count; )
        {
            if (!lines[i].IsChange)
            {
                chosen.Add(lines[i++]);
                continue;
            }

            var change = lines.Skip(i).TakeWhile(l => l.IsChange).ToList();
            var removed = change.Where(l => l.Kind == PatchLineKind.Removed).ToList();
            var added = change.Where(l => l.Kind == PatchLineKind.Added).ToList();
            for (var row = 0; row < Math.Max(removed.Count, added.Count); row++)
            {
                if (row < removed.Count)
                    Add(removed[row], isStaying: !isReverse);
                if (row < added.Count)
                    Add(added[row], isStaying: isReverse);
            }
            i += change.Count;
        }
        return chosen;

        // A line of the version the patch is applied to, which stays there unless chosen
        void Add(PatchLine line, bool isStaying)
        {
            if (isChosen(line))
                chosen.Add(line);
            else if (isStaying)
                chosen.Add(line with { Kind = PatchLineKind.Context });
        }
    }

    static int Before(int start, int count) => count == 0 ? start : start - 1;

    static int Start(int before, int count) => count == 0 ? before : before + 1;

    static IReadOnlyList<PatchLine> Numbered(IReadOnlyList<PatchLine> lines, int oldStart, int newStart)
    {
        var (oldNbr, newNbr) = (oldStart, newStart);
        return lines
            .Select(l =>
                l.Kind switch
                {
                    PatchLineKind.Context => l with { OldLineNbr = oldNbr++, NewLineNbr = newNbr++ },
                    PatchLineKind.Removed => l with { OldLineNbr = oldNbr++, NewLineNbr = 0 },
                    _ => l with { OldLineNbr = 0, NewLineNbr = newNbr++ },
                }
            )
            .ToList();
    }

    // A line with no newline after it is the last line of its version, so nothing of that version
    // can come after it. A change at the end of the file that only both together make, e.g. a line
    // added after one that gets its newline, breaks that once one of the two is left out.
    static bool IsEndWhole(IReadOnlyList<PatchLine> lines) =>
        lines
            .Select((line, i) => (line, after: lines.Skip(i + 1)))
            .Where(l => l.line.NoNewline != "")
            .All(l =>
                l.line.Kind switch
                {
                    PatchLineKind.Context => !l.after.Any(),
                    PatchLineKind.Removed => l.after.All(a => a.Kind == PatchLineKind.Added),
                    _ => l.after.All(a => a.Kind == PatchLineKind.Removed),
                }
            );

    // A new file of which some lines are not chosen, or a deleted one, is neither any longer, but a
    // change of a file both versions have: its '/dev/null' side takes the path of the other side. The
    // 'index' line goes, naming as it does the versions of the whole change, which a part is not.
    IReadOnlyList<string> ChosenHeader(IReadOnlyList<PatchHunk> hunks)
    {
        var header = Header.Where(l => !l.StartsWith("index ")).ToList();
        if (IsNew && hunks.Any(h => h.OldCount > 0))
            header = AsChange(header, "new file mode ", "--- /dev/null", "+++ ", "b/", "--- ", "a/");
        if (IsDeleted && hunks.Any(h => h.NewCount > 0))
            header = AsChange(header, "deleted file mode ", "+++ /dev/null", "--- ", "a/", "+++ ", "b/");
        return header;
    }

    // E.g. '+++ b/a.txt' gives '--- a/a.txt' in place of '--- /dev/null', a quoted path as well
    static List<string> AsChange(
        List<string> header,
        string modeLine,
        string nullLine,
        string pathStart,
        string pathPrefix,
        string newStart,
        string newPrefix
    )
    {
        var pathLine = header.First(l => l.StartsWith(pathStart))[pathStart.Length..];
        var quote = pathLine.StartsWith('"') ? "\"" : "";
        var path = $"{newStart}{quote}{newPrefix}{pathLine[(quote.Length + pathPrefix.Length)..]}";
        return header.Where(l => !l.StartsWith(modeLine)).Select(l => l == nullLine ? path : l).ToList();
    }

    public override string ToString() => $"{Header.FirstOrDefault()}: {Hunks.Count} hunks";
}
