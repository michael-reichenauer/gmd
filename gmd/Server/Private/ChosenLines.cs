using gmd.Git;

namespace gmd.Server.Private;

// The lines of a file's patch that were chosen in the diff, found by version, number and text. The
// patch is read again when they are acted on, and the file may have changed since the diff was shown,
// by the user in an editor or by an agent: a line not found as it was shown stops it, rather than
// another line being discarded in its place.
static class ChosenLines
{
    public static Result<IReadOnlySet<PatchLine>> Find(FilePatch patch, IReadOnlyList<ChosenLine> chosen, string path)
    {
        if (chosen.Count == 0)
            return new Error("No lines are chosen");

        var changes = patch.Changes.ToDictionary(l => (l.Kind == PatchLineKind.Added, NumberOf(l)));
        HashSet<PatchLine> found = [];
        foreach (var line in chosen)
        {
            if (!changes.TryGetValue((line.IsAdded, line.LineNbr), out var change) || AsShown(change.Text) != line.Text)
                return new Error($"{path} has changed since the diff was read: r reads it again");
            found.Add(change);
        }

        return found;
    }

    static int NumberOf(PatchLine line) => line.Kind == PatchLineKind.Added ? line.NewLineNbr : line.OldLineNbr;

    // The diff the view shows has a tab as three spaces (DiffService.AsLine), so a line is compared
    // as it was shown
    static string AsShown(string text) => text.Replace("\t", "   ");
}
