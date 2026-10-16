using gmd.Server;

namespace gmd.Cui.Diff;

// A diff read with whitespace left out ('git diff -w') has no file whose changes are all whitespace:
// git leaves it out, as it has nothing to show of it. Left out of the view as well, it would be gone
// from the file list, from Scroll to and from Discard, and nothing would say there was more, which in
// the diff of the uncommitted changes, where they are committed, hides a change about to be made. So
// such a file is put back from the diff read with whitespace, with no lines and marked, as GitHub
// lists a file with 'Whitespace-only changes'. Pure, with no view, so it is unit testable.
static class DiffWhitespace
{
    // The diff with whitespace left out, 'shown', with the files of the diff with whitespace, 'all',
    // that it does not have: in all's order, and of all's commits, matched by id, or in order for
    // diffs of no commit, e.g. the uncommitted changes
    public static CommitDiff[] WithWhitespaceOnlyFiles(CommitDiff[] shown, CommitDiff[] all) =>
        all.Select((diff, i) => WithWhitespaceOnlyFiles(Matching(shown, diff, i), diff)).ToArray();

    static CommitDiff? Matching(CommitDiff[] shown, CommitDiff diff, int index) =>
        diff.Id != "" ? shown.FirstOrDefault(d => d.Id == diff.Id)
        : index < shown.Length ? shown[index]
        : null;

    // A file with no lines in all, a rename or a binary file, is the same in both, which git keeps
    static CommitDiff WithWhitespaceOnlyFiles(CommitDiff? shown, CommitDiff all) =>
        (shown ?? all) with
        {
            FileDiffs = all
                .FileDiffs.Select(file =>
                    shown?.FileDiffs.FirstOrDefault(f => f.PathAfter == file.PathAfter)
                    ?? (file.SectionDiffs.Count == 0 ? file : file with { SectionDiffs = [], IsWhitespaceOnly = true })
                )
                .ToList(),
        };
}
