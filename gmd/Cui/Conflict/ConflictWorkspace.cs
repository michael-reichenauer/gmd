using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

// What comes once every conflict of an operation is resolved, as the user chose: the commit that
// finishes a merge, the continue of a rebase, or nothing yet
enum ResolveNext
{
    None,
    Commit,
    Continue,
}

interface IConflictWorkspace
{
    // Resolves the conflicts of the operation stopped on them, file by file, from the one at the path
    // or else the first, and says what the user chose to do next once none is left
    Task<ResolveNext> ResolveAsync(string repoPath, string path = "");
}

// The resolver of every conflicted file of an operation, one after the other: it opens a file in the
// resolver (ConflictView), with the list of them beside it, and goes on from how that ends, to the
// file asked for, or once one is resolved to the next one still conflicted. A file with no text to
// merge, binary or deleted on one side, is asked about instead. Once none is left, it asks whether
// to commit or continue, which the caller does: it was a box saying 'Resolve them in the diff', and
// then the diff, the resolver of one file and back, and a commit from the log.
class ConflictWorkspace : IConflictWorkspace
{
    readonly IServer server;
    readonly IProgress progress;
    readonly IConflictView conflictView;

    internal ConflictWorkspace(IServer server, IProgress progress, IConflictView conflictView)
    {
        this.server = server;
        this.progress = progress;
        this.conflictView = conflictView;
    }

    public async Task<ResolveNext> ResolveAsync(string repoPath, string path = "")
    {
        if (await ReadStateAsync(repoPath) is not ConflictState state || state.Files.Count == 0)
            return ResolveNext.None;

        // The files the operation stopped on, which the list keeps, ticking those resolved since, and
        // those whose question was cancelled, passed over until the workspace is opened again
        var files = state.Files;
        HashSet<string> passed = [];
        var sides = await server.GetConflictSidesAsync(repoPath) is ConflictSides known ? known : ConflictSides.None;
        var open = files.Any(f => f.Path == path) ? path : files[0].Path;

        while (open != "")
        {
            var list = new ConflictFileList(files, ResolvedOf(files, state), open);
            var outcome = await ShowAsync(list, state, sides, repoPath);
            if (outcome.End == ConflictEnd.Skipped)
                passed.Add(open);

            if (await ReadStateAsync(repoPath) is not ConflictState now)
                return ResolveNext.None;
            state = now;
            if (state.Files.Count == 0)
                return AskWhatNext(state);

            open = (list with { Resolved = ResolvedOf(files, state) }).After(outcome, passed);
        }
        return ResolveNext.None;
    }

    // The file opened in the resolver, or asked about where it has no text to merge
    async Task<ConflictOutcome> ShowAsync(
        ConflictFileList list,
        ConflictState state,
        ConflictSides sides,
        string repoPath
    )
    {
        var conflicted = state.Files.FirstOrDefault(f => f.Path == list.Open);
        if (conflicted == null)
            return new(ConflictEnd.Done);

        ConflictFile file;
        using (progress.Show())
        {
            // With the common ancestor, which the sides are drawn against, and without it where it
            // cannot be had, e.g. the file changed since git wrote the conflicts into it
            var read = await server.GetConflictFileAsync(conflicted.Path, conflicted.Kind, true, repoPath);
            if (read is Error)
                read = await server.GetConflictFileAsync(conflicted.Path, conflicted.Kind, false, repoPath);
            if (read is not ConflictFile conflictFile)
            {
                UI.ErrorMessage($"Failed to open {conflicted.Path}\n{read.Error.AllMessages()}");
                return new(ConflictEnd.Closed);
            }
            file = conflictFile;
        }

        if (!file.IsBinary && file.Hunks.Count > 0)
            return conflictView.Show(file, sides, state.Operation, repoPath, list);

        // Cancelled, it is passed over for the next one, as long as there is another
        if (conflictView.AskWholeFile(file, repoPath) is not Func<Task<Result>> action)
            return new(ConflictEnd.Skipped);

        using (progress.Show())
        {
            if (await action() is Error e)
            {
                UI.ErrorMessage($"Failed to resolve {file.Path}\n{e.AllMessages()}");
                return new(ConflictEnd.Closed);
            }
        }
        return new(ConflictEnd.Done);
    }

    async Task<ConflictState?> ReadStateAsync(string repoPath)
    {
        using (progress.Show())
        {
            if (await server.GetConflictStateAsync(repoPath) is ConflictState state)
                return state;
        }
        return null;
    }

    static HashSet<string> ResolvedOf(IReadOnlyList<ConflictedFile> files, ConflictState state) =>
        files.Where(f => !state.Files.Any(c => c.Path == f.Path)).Select(f => f.Path).ToHashSet();

    // Every conflict is resolved: what finishes the operation, asked rather than done, since the
    // commit takes a message and a continue may stop on the next commit's conflicts
    static ResolveNext AskWhatNext(ConflictState state)
    {
        var finish = FinishOf(state);
        if (finish == ResolveNext.None)
            return ResolveNext.None;

        var name = OperationName(state.Operation);
        var choice = UI.InfoMessage(
            "Conflicts Resolved",
            $"Every conflict of the {name.ToLower()} is resolved.\n\n"
                + (finish == ResolveNext.Commit ? "A commit finishes it." : "Continuing goes on with it."),
            0,
            [finish == ResolveNext.Commit ? "Commit" : $"Continue {name}", "Later"]
        );
        return choice == 0 ? finish : ResolveNext.None;
    }

    // What finishes the operation once its conflicts are resolved: a commit or a continue, or nothing
    // where none is in progress, e.g. the conflicts of a stash popped, which a commit finished nothing of
    internal static ResolveNext FinishOf(ConflictState state) =>
        state.Operation == GitOperation.None ? ResolveNext.None
        : state.IsFinishedByCommit ? ResolveNext.Commit
        : ResolveNext.Continue;

    static string OperationName(GitOperation operation) =>
        operation switch
        {
            GitOperation.Merge => "Merge",
            GitOperation.CherryPick => "Cherry Pick",
            GitOperation.Revert => "Revert",
            GitOperation.Rebase => "Rebase",
            GitOperation.Am => "Patch",
            _ => "Operation",
        };
}
