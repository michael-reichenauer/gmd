using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;

namespace gmd.Cui.RepoView;

// The way back from the commands that move branches, which the reflog makes possible: undoing the
// last change of a branch, undoing that undo, and bringing back the work no branch has any more
interface IUndoCommands
{
    void UndoLastChange(string branchName);
    void RecoverLostCommits();
}

class UndoCommands : IUndoCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly ILostWorkDlg lostWorkDlg;
    readonly IDiffView diffView;

    public UndoCommands(
        IViewRepo repo,
        IProgress progress,
        IStatusLine status,
        IRepoView repoView,
        IServer server,
        ILostWorkDlg lostWorkDlg,
        IDiffView diffView
    )
    {
        this.repo = repo;
        this.progress = progress;
        this.status = status;
        this.repoView = repoView;
        this.server = server;
        this.lostWorkDlg = lostWorkDlg;
        this.diffView = diffView;
    }

    // Asks nothing first, since undoing again redoes it, and says on the status line what it did
    public void UndoLastChange(string branchName) =>
        Do(async () =>
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
        Do(async () =>
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
                        repo.BranchCmds.CreateBranchFromLostCommit(choice.Work.TipId, choice.Work.BranchName);
                        return Result.Ok;
                }
            }
        });

    // The diff of the whole line of work, from where it left the history that is still there, or of
    // its one commit. Shown modally, so the list comes back when it is closed.
    async Task<Result> ShowDiffAsync(LostWork work)
    {
        var count = work.CommitIds.Count;
        var reload =
            count == 1
                ? DiffReloads.Single(n => server.GetCommitDiffAsync(work.TipId, n, repo.Path))
                : DiffReloads.Single(n =>
                    server.GetDiffRangeAsync(
                        work.OldestId,
                        work.TipId,
                        $"{count} lost commits, the newest: {work.Subject}",
                        n,
                        repo.Path
                    )
                );

        var diffsResult = await reload(DiffContext.Default);
        if (diffsResult is not CommitDiff[] diffs)
            return new Error("Failed to diff the lost commits", diffsResult.Error);

        diffView.Show(diffs[0], work.TipId, repo.Path, reload, ConflictState.None);
        return Result.Ok;
    }

    void Do(Func<Task<Result>> action) => CommandRunner.Do(progress, status, repo, action);
}
