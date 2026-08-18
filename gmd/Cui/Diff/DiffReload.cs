using gmd.Server;

namespace gmd.Cui.Diff;

// How the diff view gets its diff again, at a given number of context lines. The view is opened
// from six places using five different git commands, so it has to be told how to re-fetch rather
// than assuming it is always a commit id — which is what it did before, and why refreshing a
// stash, a range or a file history diff quietly fetched the wrong thing.
delegate Task<R<CommitDiff[]>> DiffReload(int contextLines);

static class DiffReloads
{
    // Wraps a server call returning a single diff, which is all of them except full file history.
    public static DiffReload Single(Func<int, Task<R<CommitDiff>>> getAsync) =>
        async contextLines =>
        {
            if (!Try(out var diff, out var e, await getAsync(contextLines)))
                return e;

            return new[] { diff };
        };
}
