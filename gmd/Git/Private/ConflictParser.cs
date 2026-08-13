using System.Text;

namespace gmd.Git.Private;

// Turns the text of a conflicted working tree file into segments and back again.
//
// Pure: no ICmd, no File, no state — so it is tested with raw string literals and no fakes at all.
// The test that matters most is that ToText(Parse(x)) is x, byte for byte, for every shape a file
// can have: both line endings and a mixture of them, a missing final newline, a BOM, all three
// conflict styles. Everything the resolver writes is built by changing a hunk and calling ToText,
// so if that identity holds, resolving one conflict cannot rewrite the rest of the file.
static class ConflictParser
{
    // Git's default marker size. '.gitattributes' can set 'conflict-marker-size' higher, so a run
    // of at least this many is what is matched, and the marker line is then kept verbatim.
    const int MinMarkerSize = 7;

    public static ConflictFile Parse(string path, ConflictKind kind, string text, bool hasBom = false)
    {
        var lines = ToLines(text);
        var segments = new List<ConflictSegment>();
        var stable = new List<FileLine>();
        var hunkIndex = 0;

        for (int i = 0; i < lines.Count; )
        {
            // A start marker only begins a conflict if the rest of one follows it. Anything else —
            // a marker at the end of the file, two starts in a row, a missing '=======' — is text
            // that happens to look like a marker, and is kept as text so the file still round trips
            if (!IsMarker(lines[i], '<') || !TryParseHunk(lines, i, hunkIndex, out var hunk, out var next))
            {
                stable.Add(lines[i++]);
                continue;
            }

            if (stable.Count > 0)
            {
                segments.Add(new ConflictSegment(stable));
                stable = [];
            }
            segments.Add(new ConflictSegment([], hunk));
            hunkIndex++;
            i = next;
        }

        if (stable.Count > 0)
            segments.Add(new ConflictSegment(stable));

        return new ConflictFile(path, kind, false, hasBom, segments);
    }

    // The file as it would be written now: stable stretches verbatim, each conflict as whatever was
    // chosen for it, and an unchosen conflict as the markers and blocks it came in with.
    public static string ToText(ConflictFile file)
    {
        var text = new StringBuilder();
        foreach (var segment in file.Segments)
        {
            if (segment.Hunk == null)
                Append(text, segment.Lines);
            else
                AppendHunk(text, segment.Hunk);
        }

        return text.ToString();
    }

    public static ConflictFile SetChoice(
        ConflictFile file,
        int hunkIndex,
        HunkChoice choice,
        IReadOnlyList<FileLine>? manual = null
    ) =>
        file with
        {
            Segments = file
                .Segments.Select(s =>
                    s.Hunk == null || s.Hunk.Index != hunkIndex
                        ? s
                        : s with
                        {
                            Hunk = s.Hunk with { Choice = choice, Manual = manual },
                        }
                )
                .ToList(),
        };

    // Fills in the common ancestor of each conflict from a separately computed diff3 merge, for the
    // usual case where the working tree file has no '|||||||' sections of its own. Matched by
    // position, so a mismatched count means the file has been edited since and the bases are left
    // alone rather than paired with the wrong conflicts.
    //
    // False for that mismatch rather than the file unchanged: an ancestor that could not be mapped
    // and one that is genuinely empty look identical afterwards, and the difference matters — the
    // second is a real answer the user can resolve to, the first would silently delete the region.
    public static bool TrySetBases(
        ConflictFile file,
        IReadOnlyList<IReadOnlyList<FileLine>> bases,
        out ConflictFile withBases
    )
    {
        withBases = file;
        if (bases.Count != file.Hunks.Count)
            return false;

        withBases = file with
        {
            Segments = file
                .Segments.Select(s =>
                    s.Hunk == null
                        ? s
                        : s with
                        {
                            Hunk = s.Hunk with
                            {
                                Base = bases[s.Hunk.Index],
                                BaseMarker = s.Hunk.BaseMarker ?? new FileLine("|||||||", s.Hunk.StartMarker.Eol),
                            },
                        }
                )
                .ToList(),
        };

        return true;
    }

    // Takes the common ancestor of each conflict from a separately computed diff3 merge of the same
    // three versions, and maps it onto this file's conflicts.
    //
    // Not by position, which is the obvious way and is wrong: '--diff3' changes how git *groups*
    // conflicts, not only whether it records the ancestor. Two changes a line or two apart come back
    // from 'git merge' as one conflict and from 'merge-file --diff3' as two, split at the common line
    // between them — so the second conflict's ancestor would be shown against the first, and nothing
    // would look amiss.
    //
    // What is the same in both is the *ours* text: choosing our side at every conflict reconstructs
    // our version of the file, whichever way the conflicts were grouped. So each conflict is located
    // by the lines it occupies in that reconstruction, and its ancestor is whatever the diff3 merge
    // has over the same lines — an ancestor split across two of its conflicts is joined back up,
    // together with the common text between them, which is part of the ancestor too.
    public static bool TrySetBasesFrom(ConflictFile file, ConflictFile merged, out ConflictFile withBases)
    {
        withBases = file;

        var pieces = ToPieces(merged);
        if (pieces.Sum(p => p.Ours.Count) != OursLineCount(file))
            return false; // Not the same three versions, so there is nothing safe to map

        var bases = new List<IReadOnlyList<FileLine>>();
        var at = 0;
        foreach (var segment in file.Segments)
        {
            if (segment.Hunk == null)
            {
                at += segment.Lines.Count;
                continue;
            }

            bases.Add(BaseOver(pieces, at, at + segment.Hunk.Ours.Count));
            at += segment.Hunk.Ours.Count;
        }

        return TrySetBases(file, bases, out withBases);
    }

    // One stretch of the diff3 merge: the lines it contributes to our version of the file, and the
    // lines the ancestor had there. Outside a conflict the two are the same text, since text neither
    // side touched is the ancestor's.
    record Piece(IReadOnlyList<FileLine> Ours, IReadOnlyList<FileLine> Base, bool IsConflict);

    static IReadOnlyList<Piece> ToPieces(ConflictFile merged) =>
        merged
            .Segments.Select(s =>
                s.Hunk == null ? new Piece(s.Lines, s.Lines, false) : new Piece(s.Hunk.Ours, s.Hunk.Base, true)
            )
            .ToList();

    static int OursLineCount(ConflictFile file) =>
        file.Segments.Sum(s => s.Hunk == null ? s.Lines.Count : s.Hunk.Ours.Count);

    // The ancestor of the lines our version has in [from, to)
    static IReadOnlyList<FileLine> BaseOver(IReadOnlyList<Piece> pieces, int from, int to)
    {
        var lines = new List<FileLine>();
        var at = 0;

        foreach (var piece in pieces)
        {
            var end = at + piece.Ours.Count;
            if (end > from && at < to)
            {
                // A conflict is taken whole: its ancestor does not line up with our side line for
                // line, so there is no meaningful part of it to take
                if (piece.IsConflict)
                    lines.AddRange(piece.Base);
                else
                    lines.AddRange(piece.Base.Take(Math.Min(end, to) - at).Skip(Math.Max(from, at) - at));
            }

            at = end;
            if (at >= to)
                break;
        }

        return lines;
    }

    // Splits text into lines that each carry the terminator that followed them, so that joining
    // them back gives the original text whatever mixture of endings it had. An empty text is no
    // lines at all, which is how an empty file stays empty.
    public static IReadOnlyList<FileLine> ToLines(string text)
    {
        var lines = new List<FileLine>();
        var start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;

            var isCrLf = i > start && text[i - 1] == '\r';
            var end = isCrLf ? i - 1 : i;
            lines.Add(new FileLine(text[start..end], isCrLf ? "\r\n" : "\n"));
            start = i + 1;
        }

        if (start < text.Length)
            lines.Add(new FileLine(text[start..], ""));

        return lines;
    }

    public static string ToText(IEnumerable<FileLine> lines)
    {
        var text = new StringBuilder();
        Append(text, lines);
        return text.ToString();
    }

    static void Append(StringBuilder text, IEnumerable<FileLine> lines)
    {
        foreach (var line in lines)
            text.Append(line.Text).Append(line.Eol);
    }

    static void AppendHunk(StringBuilder text, ConflictHunk hunk)
    {
        switch (hunk.Choice)
        {
            case HunkChoice.None:
                text.Append(hunk.StartMarker.Text).Append(hunk.StartMarker.Eol);
                Append(text, hunk.Ours);
                if (hunk.BaseMarker != null)
                {
                    text.Append(hunk.BaseMarker.Text).Append(hunk.BaseMarker.Eol);
                    Append(text, hunk.Base);
                }
                text.Append(hunk.SplitMarker.Text).Append(hunk.SplitMarker.Eol);
                Append(text, hunk.Theirs);
                text.Append(hunk.EndMarker.Text).Append(hunk.EndMarker.Eol);
                return;

            default:
                Append(text, Chosen(hunk));
                return;
        }
    }

    // What a resolved conflict becomes. The last line of a chosen block may have no terminator, if
    // it was the last line of the file when it was read, so it is given the one the end marker had
    // — otherwise resolving a conflict in the middle of a file would join two lines together.
    public static IReadOnlyList<FileLine> Chosen(ConflictHunk hunk)
    {
        var lines = hunk.Choice switch
        {
            HunkChoice.Ours => hunk.Ours,
            HunkChoice.Theirs => hunk.Theirs,
            HunkChoice.OursThenTheirs => [.. hunk.Ours, .. hunk.Theirs],
            HunkChoice.TheirsThenOurs => [.. hunk.Theirs, .. hunk.Ours],
            // Empty when the ancestor had nothing here, i.e. both sides added lines: the region then
            // resolves to nothing, which is exactly what taking the ancestor means
            HunkChoice.Base => hunk.Base,
            HunkChoice.Manual => Reterminate(hunk.Manual ?? [], hunk),
            _ => (IReadOnlyList<FileLine>)[],
        };

        if (lines.Count == 0 || lines[^1].Eol != "")
            return lines;

        return [.. lines.Take(lines.Count - 1), lines[^1] with { Eol = hunk.EndMarker.Eol }];
    }

    // Hand edited text arrives with whatever line ending the editor it came from used, which on
    // Windows is CRLF regardless of the file — so every line is given the file's own ending instead.
    // The '<<<<<<<' line is what that is read from: it always has a terminator, since the conflict
    // continues after it, where the '>>>>>>>' line has none when the conflict ends the file.
    static IReadOnlyList<FileLine> Reterminate(IReadOnlyList<FileLine> lines, ConflictHunk hunk)
    {
        var eol = hunk.StartMarker.Eol;
        var terminated = lines.Select(l => l with { Eol = eol }).ToList();

        // ... and if the conflict ended the file without a trailing newline, it still does
        if (terminated.Count > 0 && hunk.EndMarker.Eol == "")
            terminated[^1] = terminated[^1] with { Eol = "" };

        return terminated;
    }

    // A complete '<<<<<<< ... >>>>>>>' starting at 'start', or false if what follows is not one
    static bool TryParseHunk(IReadOnlyList<FileLine> lines, int start, int index, out ConflictHunk hunk, out int next)
    {
        hunk = null!;
        next = start;

        var ours = new List<FileLine>();
        var theirs = new List<FileLine>();
        var baseLines = new List<FileLine>();
        FileLine? baseMarker = null;
        FileLine? splitMarker = null;

        for (int i = start + 1; i < lines.Count; i++)
        {
            var line = lines[i];

            // A second start marker before this one is finished means the first was not a marker
            if (IsMarker(line, '<'))
                return false;

            if (IsMarker(line, '|') && splitMarker == null && baseMarker == null)
            {
                baseMarker = line;
                continue;
            }

            if (IsMarker(line, '=') && splitMarker == null)
            {
                splitMarker = line;
                continue;
            }

            if (IsMarker(line, '>'))
            {
                if (splitMarker == null)
                    return false; // '>>>>>>>' with no '=======' before it is not a conflict

                hunk = new ConflictHunk(index, lines[start], baseMarker, splitMarker, line, ours, baseLines, theirs);
                next = i + 1;
                return true;
            }

            var block =
                splitMarker != null ? theirs
                : baseMarker != null ? baseLines
                : ours;
            block.Add(line);
        }

        return false; // Ran off the end without an end marker
    }

    // A marker line is a run of at least seven of the same character at the start of the line,
    // followed by the end of the line or a space — which is how git writes them and how
    // 'git diff --check' recognises them.
    static bool IsMarker(FileLine line, char marker)
    {
        var text = line.Text;
        if (text.Length < MinMarkerSize)
            return false;

        for (int i = 0; i < MinMarkerSize; i++)
        {
            if (text[i] != marker)
                return false;
        }

        var i2 = MinMarkerSize;
        while (i2 < text.Length && text[i2] == marker)
            i2++;

        return i2 == text.Length || text[i2] == ' ';
    }
}
