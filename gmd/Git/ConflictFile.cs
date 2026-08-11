namespace gmd.Git;

// A line with its own terminator, so a file round trips byte for byte however it is written.
//
// One flag per file would not do: a merge of an LF file and a CRLF file really does produce a file
// with both, and a file whose last line has no newline must not gain one. Because every line keeps
// what followed it, writing back what was read needs no line ending logic at all — git's check-in
// conversion (core.autocrlf, .gitattributes) then applies exactly as it would to a hand edit.
//
// Eol is "\r\n", "\n", or "" on a last line that the file did not end with a newline.
public record FileLine(string Text, string Eol)
{
    public override string ToString() => Text;
}

// What the user chose for one conflict. None means it is still a conflict, i.e. the markers stay
// in the file, which is what keeps 'nothing chosen' and 'nothing written' the same thing.
public enum HunkChoice
{
    None,
    Ours,
    Theirs,
    OursThenTheirs,
    TheirsThenOurs,
    Neither,
    Manual,
}

// One '<<<<<<< ... >>>>>>>' region of a conflicted file.
//
// The marker lines are kept verbatim rather than regenerated, so an untouched hunk rebuilds to the
// exact bytes git wrote — including the marker size, which .gitattributes 'conflict-marker-size'
// can change, and the labels, which say which side is which.
//
// Base is empty unless the file was written in 'diff3' or 'zdiff3' conflict style, which is the
// only style that records the common ancestor in the file itself.
public record ConflictHunk(
    int Index,
    FileLine StartMarker, // '<<<<<<< HEAD'
    FileLine? BaseMarker, // '||||||| merged common ancestors', null in the default 'merge' style
    FileLine SplitMarker, // '======='
    FileLine EndMarker, // '>>>>>>> topic'
    IReadOnlyList<FileLine> Ours,
    IReadOnlyList<FileLine> Base,
    IReadOnlyList<FileLine> Theirs,
    HunkChoice Choice = HunkChoice.None,
    IReadOnlyList<FileLine>? Manual = null
)
{
    // The names git put on the two sides, e.g. 'HEAD' and 'topic', or 'HEAD' and
    // '4a15fb2 (Add gamma)' during a rebase. These are the only per conflict statement of which
    // side is which, which is why the view labels its columns from them rather than saying
    // 'ours' and 'theirs' — during a rebase those two mean the opposite of what is expected.
    public string OursLabel => LabelOf(StartMarker);
    public string TheirsLabel => LabelOf(EndMarker);
    public string BaseLabel => BaseMarker == null ? "" : LabelOf(BaseMarker);

    public bool HasBase => BaseMarker != null && Base.Count > 0;
    public bool IsResolved => Choice != HunkChoice.None;

    // The text after the marker run, e.g. '<<<<<<< HEAD' -> 'HEAD'
    static string LabelOf(FileLine marker) => marker.Text.TrimStart('<', '|', '=', '>').Trim();

    public override string ToString() => $"{Index}: {OursLabel} vs {TheirsLabel} ({Choice})";
}

// What the layer above decided for one conflict, sent back down by position rather than as a whole
// file — see ConflictService.ResolveAsync.
public record HunkResolution(int Index, HunkChoice Choice, string ManualText = "")
{
    public override string ToString() => $"{Index}: {Choice}";
}

// One part of a conflicted file: either text that is not in dispute, or a conflict. Exactly one of
// the two is set — Hunk is null for the stable stretches, and Lines is empty for a conflict, whose
// text lives in the hunk's three blocks.
public record ConflictSegment(IReadOnlyList<FileLine> Lines, ConflictHunk? Hunk = null)
{
    public override string ToString() => Hunk?.ToString() ?? $"{Lines.Count} lines";
}

// A conflicted file as the resolver works on it: the working tree file parsed into stable stretches
// and conflicts. The working tree file is the source of truth rather than the index stages, because
// it is the only artifact that has the text *between* the conflicts, and it is what git will take
// verbatim when the file is marked resolved.
public record ConflictFile(
    string Path,
    ConflictKind Kind,
    bool IsBinary,
    bool HasBom,
    IReadOnlyList<ConflictSegment> Segments,
    // Which sides the index actually holds, i.e. stages 2 and 3. Not every conflict has both: a
    // file one side deleted has only the other, and a rename/rename leaves a path with neither.
    // What can be offered for a file is decided from these rather than from its kind, so a command
    // that git would refuse is never put in front of the user.
    bool HasOurs = true,
    bool HasTheirs = true
)
{
    public IReadOnlyList<ConflictHunk> Hunks => Segments.Select(s => s.Hunk).OfType<ConflictHunk>().ToList();

    public int UnresolvedCount => Hunks.Count(h => !h.IsResolved);

    public override string ToString() => $"{Path} ({Kind}): {Hunks.Count} conflicts, {UnresolvedCount} unresolved";
}
