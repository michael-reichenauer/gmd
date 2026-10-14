using gmd.Server;

namespace gmd.Cui.Diff;

// How the diff view gets its diff again, at a given number of context lines. The view is opened
// from six places using five different git commands, so it has to be told how to re-fetch rather
// than assuming it is always a commit id — which is what it did before, and why refreshing a
// stash, a range or a file history diff quietly fetched the wrong thing.
delegate Task<Result<CommitDiff[]>> DiffReload(int contextLines);

// Where the diff the view shows comes from: how to read it again, and what can be done with it. The
// diff alone does not say, so the view took the id of the uncommitted changes to mean they can be
// committed and discarded, and offered more and less context to every diff.
record DiffSource(DiffReload Reload)
{
    // Whether git can read it again with more or less context, which '+' and '-' step. A line
    // history cannot: 'git log -L' takes no context, so it read the same lines and the file's header
    // said the context had changed.
    public bool HasContext { get; init; } = true;

    // The uncommitted changes as the log opens them, which the view commits ('c'), discards ('u')
    // and resolves the conflicts of. Not from blame, two dialogs deep, which cannot act on a commit
    // and was left showing the lines a discard had changed; nor a range that ends in them, whose
    // files are not all uncommitted.
    public bool CanCommit { get; init; }

    // A source of one diff, which is all of them except the file and line histories
    public static DiffSource Single(Func<int, Task<Result<CommitDiff>>> getAsync) =>
        new(async contextLines =>
            await getAsync(contextLines) switch
            {
                CommitDiff diff => new[] { diff },
                Error e => e,
            }
        );
}
