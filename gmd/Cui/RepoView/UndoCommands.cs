using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// The way back from the commands that move branches, which the reflog makes possible: undoing the
// last change of a branch, and undoing that undo
interface IUndoCommands
{
    void UndoLastChange(string branchName);
}

class UndoCommands : IUndoCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly IRepoView repoView;
    readonly IServer server;

    public UndoCommands(IViewRepo repo, IProgress progress, IStatusLine status, IRepoView repoView, IServer server)
    {
        this.repo = repo;
        this.progress = progress;
        this.status = status;
        this.repoView = repoView;
        this.server = server;
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

    void Do(Func<Task<Result>> action) => CommandRunner.Do(progress, status, repo, action);
}
