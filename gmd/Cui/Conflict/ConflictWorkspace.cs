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
// file asked for, or once one is resolved to the next one still conflicted, in the same view. A file
// with no text to merge, binary or deleted on one side, is asked about instead. Once none is left, it
// asks whether to commit or continue, which the caller does: it was a box saying 'Resolve them in
// the diff', and then the diff, the resolver of one file and back, and a commit from the log.
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

        var sides = await server.GetConflictSidesAsync(repoPath) is ConflictSides known ? known : ConflictSides.None;
        var session = new Session(this, state, repoPath);
        var first = await session.StartAsync(path);
        if (first.File is ConflictFile file)
            conflictView.Show(file, sides, state.Operation, repoPath, first.List, session.NextAsync);
        return session.Finish;
    }

    // One opening of the workspace, which the resolver asks for the file to go on to each time one
    // ends: the files the operation stopped on, which the list keeps, ticking those resolved since,
    // those whose question was cancelled, passed over until the workspace is opened again, and what
    // the user chose to do once none is left
    class Session
    {
        readonly ConflictWorkspace workspace;
        readonly IReadOnlyList<ConflictedFile> files;
        readonly string repoPath;
        readonly HashSet<string> passed = [];
        ConflictState state;
        ConflictFileList list = ConflictFileList.None;

        public ResolveNext Finish { get; private set; } = ResolveNext.None;

        public Session(ConflictWorkspace workspace, ConflictState state, string repoPath)
        {
            this.workspace = workspace;
            this.state = state;
            this.files = state.Files;
            this.repoPath = repoPath;
        }

        // The file at the path, or else the first, or the one after it it comes to, see NextAsync
        public async Task<ConflictNext> StartAsync(string path)
        {
            var open = files.Any(f => f.Path == path) ? path : files[0].Path;
            list = new ConflictFileList(files, ResolvedOf(files, state), open);
            var (file, asked) = await workspace.OpenAsync(list, state, repoPath);
            return file != null ? new(file, list) : await NextAsync(asked);
        }

        // The file to open once the open one ended as it did, those with no text to merge on the way
        // asked about as their turn comes, or none, when the resolver closes: once no conflict is
        // left, after asking what finishes the operation
        public async Task<ConflictNext> NextAsync(ConflictOutcome outcome)
        {
            while (true)
            {
                if (outcome.End == ConflictEnd.Skipped)
                    passed.Add(list.Open);

                if (await workspace.ReadStateAsync(repoPath) is not ConflictState now)
                    return ConflictNext.Close;
                state = now;
                if (state.Files.Count == 0)
                {
                    Finish = AskWhatNext(state);
                    return ConflictNext.Close;
                }

                list = list with { Resolved = ResolvedOf(files, state) };
                var open = list.After(outcome, passed);
                if (open == "")
                    return ConflictNext.Close;

                list = list with { Open = open };
                var (file, asked) = await workspace.OpenAsync(list, state, repoPath);
                if (file != null)
                    return new(file, list);
                outcome = asked;
            }
        }
    }

    // The open file of the list read, to be resolved in the resolver, or where it has no text to
    // merge asked about instead, and how that ended
    async Task<(ConflictFile? File, ConflictOutcome Asked)> OpenAsync(
        ConflictFileList list,
        ConflictState state,
        string repoPath
    )
    {
        var conflicted = state.Files.FirstOrDefault(f => f.Path == list.Open);
        if (conflicted == null)
            return (null, new(ConflictEnd.Done));

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
                return (null, new(ConflictEnd.Closed));
            }
            file = conflictFile;
        }

        if (!file.IsBinary && file.Hunks.Count > 0)
            return (file, new(ConflictEnd.Done));

        // Cancelled, it is passed over for the next one, as long as there is another
        if (conflictView.AskWholeFile(file, repoPath) is not Func<Task<Result>> action)
            return (null, new(ConflictEnd.Skipped));

        using (progress.Show())
        {
            if (await action() is Error e)
            {
                UI.ErrorMessage($"Failed to resolve {file.Path}\n{e.AllMessages()}");
                return (null, new(ConflictEnd.Closed));
            }
        }
        return (null, new(ConflictEnd.Done));
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

        var name = OperationText.Name(state.Operation);
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
}
