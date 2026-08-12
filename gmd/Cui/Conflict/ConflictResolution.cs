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
            HunkChoice.Neither => [], // neither
            HunkChoice.Manual => ManualTextOf(hunk.Index).Split('\n').Select(l => new FileLine(l)).ToList(), // Manual
            _ => [],
        };

    // The conflict at or after a row's, walking in the given direction, or -1 if there is none that
    // way. A row between conflicts has -1 as its own index, so it starts from the one after it.
    public int NextHunk(int fromHunkIndex, int step)
    {
        var next = fromHunkIndex + step;
        return next >= 0 && next < HunkCount ? next : -1;
    }
}
