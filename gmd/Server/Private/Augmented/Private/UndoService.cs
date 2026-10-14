using gmd.Common;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// See IUndoService
class UndoService : IUndoService
{
    readonly IGit git;
    readonly IRepoConfigService repoConfig;

    internal UndoService(IGit git, IRepoConfigService repoConfig)
    {
        this.git = git;
        this.repoConfig = repoConfig;
    }

    // Takes back the last change of a branch, or redoes the change an undo took back. The current
    // branch is reset in the way that loses nothing uncommitted, see UndoStep.Mode, and any other is
    // just moved, unless it is checked out in another worktree, whose files would no longer match.
    // Either only from where the step found it, so a commit made since is not taken back with it.
    // The reset is an entry in the branch's reflog like any other, so the change it took back is
    // recorded with it, and Undo again names that change and redoes it, rather than undoing "a reset".
    public async Task<Result> UndoStepAsync(Repo repo, UndoStep step)
    {
        if (!repo.BranchByName.TryGetValue(step.BranchName, out var branch) || branch.IsRemote)
            return new Error($"There is no local branch '{step.BranchName}'");
        if (branch.WorktreePath != "")
            return new Error($"'{step.BranchName}' is checked out in another worktree, so it is undone there");

        if (branch.IsCurrent)
        {
            var isKeep = step.Mode == UndoMode.Keep || (step.Mode == UndoMode.KeepWhenClean && repo.Status.IsOk);
            if (await git.ResetBranchAsync(step.BranchName, step.TargetId, step.TipId, isKeep, repo.Path) is Error e)
                return e;
        }
        else if (await MoveBranchAsync(step, repo.Path) is Error moveError)
        {
            return moveError;
        }

        RecordStep(
            repo,
            step.BranchName,
            new RecordedStep
            {
                Kind = step.Kind.ToString(),
                Name = step.Name,
                BeforeId = step.TipId,
                AfterId = step.TargetId,
                Moves = 1,
                IsRedo = !step.IsRedo,
            }
        );
        return Result.Ok;
    }

    // Pulls a branch whose remote branch a force push rewrote: its own commits are moved onto the new
    // version, and the old version's commits are left out (Recover Lost Commits finds them), rather
    // than merged in, which would put every commit in twice. The current branch is rebased, and the
    // pull is recorded, so that Undo names it a pull; a branch that is not checked out can only take
    // the new version, i.e. when it has no commits of its own, which moves it only if it is still
    // where it was read.
    public async Task<Result> PullRewrittenAsync(Repo repo, RemoteRewrite rewrite)
    {
        if (!repo.BranchByName.TryGetValue(rewrite.BranchName, out var branch) || branch.IsRemote)
            return new Error($"There is no local branch '{rewrite.BranchName}'");
        if (branch.WorktreePath != "")
            return new Error($"'{rewrite.BranchName}' is checked out in another worktree, so it is pulled there");

        if (branch.IsCurrent)
        {
            if (await git.RebaseOntoRemoteAsync(rewrite.RemoteName, rewrite.ForkPointId, repo.Path) is Error e)
                return e;

            RecordStep(
                repo,
                rewrite.BranchName,
                new RecordedStep
                {
                    Kind = nameof(StepKind.Pull),
                    BeforeId = rewrite.LocalTipId,
                    Moves = 1,
                }
            );
            return Result.Ok;
        }

        if (rewrite.OwnCount > 0)
            return new Error($"'{rewrite.BranchName}' has commits of its own, so it is pulled when checked out");

        var message = $"pull: rewritten {rewrite.RemoteName}";
        var moved = await git.MoveBranchAsync(
            rewrite.BranchName,
            rewrite.NewTipId,
            rewrite.LocalTipId,
            message,
            repo.Path
        );
        return moved is CmdError moveError && moveError.ErrorOutput.Contains("but expected")
            ? new Error($"'{rewrite.BranchName}' has moved since it was read, so it was left where it is", moveError)
            : moved;
    }

    // A branch that is not checked out has no files to take back, so it is just moved, and only if
    // it is still where the step found it, which git checks as it moves it
    async Task<Result> MoveBranchAsync(UndoStep step, string wd)
    {
        var message = $"undo: moving to {step.TargetId}";
        var result = await git.MoveBranchAsync(step.BranchName, step.TargetId, step.TipId, message, wd);
        return result is CmdError e && e.ErrorOutput.Contains("but expected")
            ? new Error($"'{step.BranchName}' has moved since it was read, so it was left where it is", e)
            : result;
    }

    void RecordStep(Repo repo, string branchName, RecordedStep step) => Record(repoConfig, repo, branchName, step);

    // Records a change gmd made to a branch as its latest, see RepoConfig.UndoSteps, and forgets the
    // records of branches that are gone. Static, since the commit rewrites record theirs too.
    internal static void Record(IRepoConfigService repoConfig, Repo repo, string branchName, RecordedStep step) =>
        repoConfig.Set(
            repo.Path,
            c =>
            {
                foreach (var name in c.UndoSteps.Keys.Where(n => !repo.BranchByName.ContainsKey(n)).ToList())
                {
                    c.UndoSteps.Remove(name);
                }
                c.UndoSteps[branchName] = step;
            }
        );
}
