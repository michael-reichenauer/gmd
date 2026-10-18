using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Cui.Recovery;
using gmd.Server;

namespace gmd.Cui.RepoView;

// The way back from the commands that move branches, which the reflog makes possible: undoing the
// last change of a branch, undoing that undo, and bringing back the work no branch has any more. And
// the way back from deleting a branch, whose reflog git deletes with it, and from discarding changes,
// which git never had, both of which gmd records itself.
interface IUndoCommands
{
    void UndoLastChange(string branchName);
    void RecoverLostCommits();
    void RestoreDeletedBranch();
    void RestoreDiscarded();
}

class UndoCommands : IUndoCommands
{
    readonly CommandContext context;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly ILostWorkDlg lostWorkDlg;
    readonly IDiffView diffView;
    readonly IDeletedBranchesDlg deletedBranchesDlg;
    readonly IRestoreBranchDlg restoreBranchDlg;
    readonly IDiscardedDlg discardedDlg;

    public UndoCommands(
        CommandContext context,
        ILostWorkDlg lostWorkDlg,
        IDiffView diffView,
        IDeletedBranchesDlg deletedBranchesDlg,
        IRestoreBranchDlg restoreBranchDlg,
        IDiscardedDlg discardedDlg
    )
    {
        this.discardedDlg = discardedDlg;
        this.context = context;
        this.progress = context.Progress;
        this.status = context.Status;
        this.repoView = context.View;
        this.server = context.Server;
        this.lostWorkDlg = lostWorkDlg;
        this.diffView = diffView;
        this.deletedBranchesDlg = deletedBranchesDlg;
        this.restoreBranchDlg = restoreBranchDlg;
    }

    // Asks nothing first, since undoing again redoes it, and says on the status line what it did
    public void UndoLastChange(string branchName) =>
        Do(async repo =>
        {
            if (!repo.Repo.BranchByName.TryGetValue(branchName, out var branch))
                return new Notice($"There is no branch '{branchName}'");
            var step = BranchUndo.StepOf(repo.Repo, branch);
            var whyNot = BranchUndo.WhyNot(repo.Repo, branch, step);
            if (step == null || whyNot != "")
                return new Notice(whyNot);

            using (status.Progress(BranchUndo.Doing(step)))
            {
                if (await server.UndoStepAsync(repo.Repo, step) is Error e)
                    return new Error($"{BranchUndo.Label(step)} failed", e);
            }

            repoView.Refresh();
            status.Info(BranchUndo.Done(step));
            return Result.Ok;
        });

    // The lines of work no branch, tag or stash has any more, in a list to diff them from and to
    // bring one back with a branch at it. The list is read once, since nothing in it changes
    // while it is looked at, and shown again after each diff.
    public void RecoverLostCommits() =>
        Do(async repo =>
        {
            IReadOnlyList<LostWork> works;
            using (status.Progress("Looking for lost commits"))
            {
                var worksResult = await server.GetLostWorkAsync(repo.Repo);
                if (worksResult is not IReadOnlyList<LostWork> found)
                    return new Error("Failed to look for lost commits", worksResult.Error);
                works = found;
            }
            if (works.Count == 0)
                return new Notice("No lost commits: every commit the reflogs know of is on a branch");

            var selected = 0;
            while (true)
            {
                if (lostWorkDlg.Show(works, selected) is not LostWorkChoice choice)
                    return Result.Ok;
                selected = works.ToList().IndexOf(choice.Work);

                switch (choice.Action)
                {
                    case LostWorkAction.Diff:
                        if (await ShowDiffAsync(choice.Work) is Error diffError)
                            return diffError;
                        break;
                    case LostWorkAction.CreateBranch:
                        context.CreateCmds.CreateBranchFromLostCommit(choice.Work.TipId, choice.Work.BranchName);
                        return Result.Ok;
                }
            }
        });

    // The branches gmd deleted, in a list to bring one back from. A branch that was only deleted here
    // is restored when picked, since that only makes a branch, which deleting again takes back; one
    // deleted on origin asks which sides to restore first, since restoring origin's is a push.
    public void RestoreDeletedBranch() =>
        Do(async repo =>
        {
            IReadOnlyList<DeletedBranch> branches;
            using (status.Progress("Looking for deleted branches"))
            {
                var branchesResult = await server.GetDeletedBranchesAsync(repo.Repo);
                if (branchesResult is not IReadOnlyList<DeletedBranch> found)
                    return new Error("Failed to look for deleted branches", branchesResult.Error);
                branches = found;
            }
            if (branches.Count == 0)
                return new Notice(
                    "No deleted branch to restore: gmd keeps the branches it deleted, until they are back"
                );

            if (deletedBranchesDlg.Show(branches) is not DeletedBranch deleted)
                return Result.Ok;

            var (isLocal, isRemote) = (deleted.IsLocal, false);
            if (deleted.IsRemote)
            {
                if (restoreBranchDlg.Show(deleted) is not RestoreBranchResult rsp)
                    return Result.Ok;
                (isLocal, isRemote) = (rsp.IsLocal, rsp.IsRemote);
                if (!isLocal && !isRemote)
                    return Result.Ok;
            }

            Result restored;
            using (status.Progress(DeletedBranchRows.Restoring(deleted, isLocal)))
            {
                restored = await server.RestoreBranchAsync(repo.Repo, deleted, isLocal, isRemote);
            }

            // Shown even when a part failed, since a side can be back then, which the error says
            repoView.Refresh(isLocal ? deleted.Name : deleted.RemoteName);
            if (restored is Error e)
                return e;
            status.Info(DeletedBranchRows.Restored(deleted, isLocal, isRemote));
            return Result.Ok;
        });

    // What the discards threw away, and the stashes dropped, in a list to diff and to put one back
    // from, which gmd keeps for about 30 days (Server.DiscardedChanges). Read once, and shown again
    // after a diff. A discard is put back in the working tree as it was, which git refuses, whole,
    // if its files have changed since; then its diff is still there to copy from.
    public void RestoreDiscarded() =>
        Do(async repo =>
        {
            IReadOnlyList<DiscardedChange> changes;
            using (status.Progress("Looking for discarded changes"))
            {
                var changesResult = await server.GetDiscardedAsync(repo.Path);
                if (changesResult is not IReadOnlyList<DiscardedChange> found)
                    return new Error("Failed to look for discarded changes", changesResult.Error);
                changes = found;
            }
            if (changes.Count == 0)
                return new Notice("Nothing discarded to restore: what gmd discards, it keeps for about 30 days");

            var selected = 0;
            while (true)
            {
                if (discardedDlg.Show(changes, selected) is not DiscardedChoice choice)
                    return Result.Ok;
                selected = changes.ToList().IndexOf(choice.Change);

                if (choice.Action == DiscardedAction.Diff)
                {
                    if (await ShowDiscardedDiffAsync(choice.Change) is Error diffError)
                        return diffError;
                    continue;
                }

                Result restored;
                using (status.Progress(DiscardedRows.Restoring(choice.Change)))
                {
                    restored = await server.RestoreDiscardedAsync(choice.Change, repo.Path);
                }
                if (restored is Error e)
                    return new Error(
                        $"Could not restore {choice.Change.What}: its files have changed since it was discarded.\n"
                            + "Diff shows what it was, to copy from.",
                        e
                    );

                repoView.Refresh();
                status.Info(DiscardedRows.Restored(choice.Change));
                return Result.Ok;
            }
        });

    // What Restore would put back, shown modally, so the list comes back when it is closed
    async Task<Result> ShowDiscardedDiffAsync(DiscardedChange change)
    {
        var repo = context.ViewRepo;
        var source = DiffSource.Single(n => server.GetDiscardedDiffAsync(change, n, repo.Path));
        var diffsResult = await source.Reload(DiffContext.Default);
        if (diffsResult is not CommitDiff[] diffs)
            return new Error("Failed to diff what was discarded", diffsResult.Error);

        diffView.Show(diffs, source, repo.Repo);
        return Result.Ok;
    }

    // The diff of the whole line of work, from where it left the history that is still there, or of
    // its one commit. Shown modally, so the list comes back when it is closed.
    async Task<Result> ShowDiffAsync(LostWork work)
    {
        var repo = context.ViewRepo;
        var count = work.CommitIds.Count;
        var source =
            count == 1
                ? DiffSource.Single(n => server.GetCommitDiffAsync(work.TipId, n, repo.Path))
                : DiffSource.Single(n =>
                    server.GetDiffRangeAsync(
                        work.OldestId,
                        work.TipId,
                        $"{count} lost commits, the newest: {work.Subject}",
                        n,
                        repo.Path
                    )
                ) with
                {
                    Sides = new($"{work.OldestId}~", work.TipId),
                };

        var diffsResult = await source.Reload(DiffContext.Default);
        if (diffsResult is not CommitDiff[] diffs)
            return new Error("Failed to diff the lost commits", diffsResult.Error);

        diffView.Show(diffs, source, repo.Repo);
        return Result.Ok;
    }

    void Do(Func<IViewRepo, Task<Result>> action) => context.Do(action);
}
