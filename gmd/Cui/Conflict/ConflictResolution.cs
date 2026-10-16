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
        Set(hunkIndex, HunkChoice.Manual, string.Join('\n', lines.Select(l => l.Text)));
        combined[hunkIndex] = lines;
    }

    // A copy with one conflict as text being typed by hand, which the result pane shows while it is
    // typed (ConflictView): the decision is made once the typing is done
    public ConflictResolution WithTyped(int hunkIndex, string text)
    {
        var copy = new ConflictResolution(file);
        choices.ToList().ForEach(c => copy.choices[c.Key] = c.Value);
        manualTexts.ToList().ForEach(t => copy.manualTexts[t.Key] = t.Value);
        combined.ToList().ForEach(c => copy.combined[c.Key] = c.Value);
        copy.Set(hunkIndex, HunkChoice.Manual, text);
        return copy;
    }

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
}
