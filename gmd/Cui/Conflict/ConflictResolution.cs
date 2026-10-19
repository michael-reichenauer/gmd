using gmd.Server;

namespace gmd.Cui.Conflict;

// What the user has decided for each conflict of one file, and what those decisions resolve to.
//
// No view and no Terminal.Gui, so it is unit testable — the same reason ContentScroll, Hoover and
// MenuDimensions exist. The file itself is never modified here: the decisions are what goes back
// down to be applied, so nothing in the Cui layer builds file text.
class ConflictResolution
{
    readonly ConflictFile file;
    readonly Dictionary<int, HunkChoice> choices = [];
    readonly Dictionary<int, string> manualTexts = [];
    readonly Dictionary<int, IReadOnlyList<CombinedLine>> combined = [];

    public ConflictResolution(ConflictFile file)
    {
        this.file = file;
    }

    // The file the decisions are of, as it was read
    public ConflictFile File => file;

    public int HunkCount => file.Hunks.Count;
    public int UnresolvedCount => file.Hunks.Count(h => ChoiceOf(h.Index) == HunkChoice.None);
    public bool IsFullyResolved => UnresolvedCount == 0;
    public bool IsChanged => choices.Count > 0;

    public HunkChoice ChoiceOf(int hunkIndex) => choices.GetValueOrDefault(hunkIndex, HunkChoice.None);

    public string ManualTextOf(int hunkIndex) => manualTexts.GetValueOrDefault(hunkIndex, "");

    public void Set(int hunkIndex, HunkChoice choice, string manualText = "")
    {
        combined.Remove(hunkIndex);
        if (choice == HunkChoice.None)
        {
            choices.Remove(hunkIndex);
            manualTexts.Remove(hunkIndex);
            return;
        }

        choices[hunkIndex] = choice;
        if (choice == HunkChoice.Manual)
            manualTexts[hunkIndex] = manualText;
        else
            manualTexts.Remove(hunkIndex);
    }

    // Both sides' changes in one (ConflictCombine), taken as text the user wrote, as an edit by hand
    // is, and kept as lines with where each came from, which is how the result draws them
    public void SetCombined(int hunkIndex, IReadOnlyList<CombinedLine> lines)
    {
        Set(hunkIndex, HunkChoice.Manual, ToText(lines.Select(l => l.Text)));
        combined[hunkIndex] = lines;
    }

    // A copy with one conflict as text being typed by hand, which the result pane shows while it is
    // typed (ConflictView): the decision is made once the typing is done
    public ConflictResolution WithTyped(int hunkIndex, string text)
    {
        var copy = CopyFor(file);
        copy.Set(hunkIndex, HunkChoice.Manual, text);
        return copy;
    }

    // The decisions for the file read again, e.g. as the resolver goes back to it, null where it no
    // longer has the conflicts they were made in, changed on disk meanwhile: they are held by number,
    // so they would be applied to other conflicts. The text between the conflicts and the sides of
    // each must be as they were; the ancestor need not, which is read where it can be.
    public ConflictResolution? For(ConflictFile reread)
    {
        if (reread.Path != file.Path || reread.Segments.Count != file.Segments.Count)
            return null;

        var isSame = file
            .Segments.Zip(reread.Segments)
            .All(s =>
                IsSame(s.First.Lines, s.Second.Lines)
                && (s.First.Hunk, s.Second.Hunk) switch
                {
                    (null, null) => true,
                    (ConflictHunk a, ConflictHunk b) => IsSame(a.Ours, b.Ours) && IsSame(a.Theirs, b.Theirs),
                    _ => false,
                }
            );
        if (!isSame)
            return null;

        return CopyFor(reread);
    }

    ConflictResolution CopyFor(ConflictFile of)
    {
        var copy = new ConflictResolution(of);
        choices.ToList().ForEach(c => copy.choices[c.Key] = c.Value);
        manualTexts.ToList().ForEach(t => copy.manualTexts[t.Key] = t.Value);
        combined.ToList().ForEach(c => copy.combined[c.Key] = c.Value);
        return copy;
    }

    static bool IsSame(IReadOnlyList<FileLine> a, IReadOnlyList<FileLine> b) =>
        a.Select(l => l.Text).SequenceEqual(b.Select(l => l.Text));

    // The combination a conflict was decided with, null where it was not
    public IReadOnlyList<CombinedLine>? CombinedOf(int hunkIndex) => combined.GetValueOrDefault(hunkIndex);

    // Every conflict, in order, whether decided or not — the count is what the Git layer checks the
    // file against, so a file that changed underneath is caught rather than resolved by position
    public IReadOnlyList<HunkResolution> ToResolutions() =>
        file.Hunks.Select(h => new HunkResolution(h.Index, ChoiceOf(h.Index), ManualTextOf(h.Index))).ToList();

    // What a conflict currently resolves to, i.e. what the result pane shows. An undecided one
    // resolves to nothing yet, which the pane says in words rather than drawing as empty.
    public IReadOnlyList<FileLine> ResultOf(ConflictHunk hunk) =>
        ChoiceOf(hunk.Index) switch
        {
            HunkChoice.Ours => hunk.Ours,
            HunkChoice.Theirs => hunk.Theirs,
            HunkChoice.OursThenTheirs => [.. hunk.Ours, .. hunk.Theirs],
            HunkChoice.TheirsThenOurs => [.. hunk.Theirs, .. hunk.Ours],
            // Empty when the ancestor had nothing here, i.e. both sides added lines
            HunkChoice.Base => hunk.Base,
            HunkChoice.Manual => ToLines(ManualTextOf(hunk.Index)),
            _ => [],
        };

    // Hand edited text as lines, however the editor it came from ended them. The Git layer gives
    // them the file's own ending when it writes; here they are only drawn.
    //
    // Split by the same rule the Git layer writes by — a terminator ends a line rather than
    // starting an empty one, so no text is no lines at all. See ConflictParser.ToLines, which this
    // cannot call: it is below the Server layer, and every line of it carries a terminator this
    // side of the model has been narrowed of. ConflictResolutionTest pins the two together.
    //
    // Without the same rule, the result pane previewed one blank line where nothing would be
    // written, so emptying the edit box — which is how a conflicted region is dropped — looked
    // like it would leave an empty line behind.
    public static IReadOnlyList<FileLine> ToLines(string text)
    {
        if (text == "")
            return [];

        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => new FileLine(l)).ToList();
        if (lines.Count > 1 && lines[^1].Text == "")
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    // Lines as text, which ToLines reads back as the same lines, e.g. both sides to start typing
    // from, or a combination taken as text. Joined by '\n', and with one more after an empty last
    // line: by the rule above a '\n' at the end only ends the line before it, so joining alone lost
    // a blank last line, and a combination of one blank line wrote nothing.
    public static string ToText(IEnumerable<string> lines)
    {
        var list = lines.ToList();
        var text = string.Join('\n', list);
        return list.Count > 0 && list[^1] == "" ? text + "\n" : text;
    }
}
