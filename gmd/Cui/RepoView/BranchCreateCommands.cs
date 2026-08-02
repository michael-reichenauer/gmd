using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// Creating and deleting branches, i.e. the commands that ask the user for a name or for what to
// delete first, and then run the git command for them.
interface IBranchCreateCommands
{
    void CreateBranch();
    void CreateBranchFromBranch(string name);
    void CreateBranchFromCommit();
    void DeleteBranch(string name);
}

class BranchCreateCommands : IBranchCreateCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IDeleteBranchDlg deleteBranchDlg;

    public BranchCreateCommands(
        IViewRepo repo,
        IProgress progress,
        IRepoView repoView,
        IServer server,
        ICreateBranchDlg createBranchDlg,
        IDeleteBranchDlg deleteBranchDlg
    )
    {
        this.repo = repo;
        this.progress = progress;
        this.repoView = repoView;
        this.server = server;
        this.createBranchDlg = createBranchDlg;
        this.deleteBranchDlg = deleteBranchDlg;
    }

    public void CreateBranch() =>
        Do(async () =>
        {
            var branchName = "";
            try
            {
                var currentBranchName = repo.Repo.CurrentBranch().Name;
                if (!Try(out var rsp, createBranchDlg.Show(currentBranchName, "")))
                    return R.Ok;

                if (!Try(out var e, await server.CreateBranchAsync(repo.Repo, rsp.Name, rsp.IsCheckout, repo.Path)))
                {
                    return R.Error($"Failed to create branch {rsp.Name}", e);
                }
                branchName = rsp.Name;

                if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(branchName, repo.Path)))
                {
                    // The push error could be that repo has no remote origin, (local only)
                    if (e.ErrorMessage.Contains("'origin' does not appear to be a git repository"))
                    { // The push error is that repo has no remote origin, (local repo only)
                        // I.e. no remote repo to push to, lets just ignore the push error
                        return R.Ok;
                    }

                    return R.Error($"Failed to push branch {branchName} to remote server", e);
                }

                return R.Ok;
            }
            finally
            {
                Refresh(branchName);
            }
        });

    public void CreateBranchFromBranch(string name) =>
        Do(async () =>
        {
            var branchName = "";
            try
            {
                //var currentBranchName = repo.GetCurrentBranch().Name;
                var branch = repo.Repo.BranchByName[name];
                if (branch.LocalName != "")
                    name = branch.LocalName;

                if (!Try(out var rsp, createBranchDlg.Show(name, "")))
                    return R.Ok;

                if (
                    !Try(
                        out var e,
                        await server.CreateBranchFromBranchAsync(repo.Repo, rsp.Name, name, rsp.IsCheckout, repo.Path)
                    )
                )
                {
                    return R.Error($"Failed to create branch {rsp.Name}", e);
                }
                branchName = rsp.Name;

                if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(branchName, repo.Path)))
                { // The push error could be that repo has no remote origin, (local only)
                    if (e.ErrorMessage.Contains("'origin' does not appear to be a git repository"))
                    { // The push error is that repo has no remote origin, (local repo only)
                        // I.e. no remote repo to push to, lets just ignore the push error
                        return R.Ok;
                    }

                    return R.Error($"Failed to push branch {branchName} to remote server", e);
                }

                return R.Ok;
            }
            finally
            {
                Refresh(branchName);
            }
        });

    public void CreateBranchFromCommit() =>
        Do(async () =>
        {
            var branchName = "";
            try
            {
                var commit = repo.RowCommit;
                var commitBranchName = commit.BranchName;

                if (!Try(out var rsp, createBranchDlg.Show(commitBranchName, commit.Sid)))
                    return R.Ok;

                if (
                    !Try(
                        out var e,
                        await server.CreateBranchFromCommitAsync(
                            repo.Repo,
                            rsp.Name,
                            commit.Id,
                            rsp.IsCheckout,
                            repo.Path
                        )
                    )
                )
                {
                    return R.Error($"Failed to create branch {rsp.Name}", e);
                }
                branchName = rsp.Name;

                if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(rsp.Name, repo.Path)))
                { // The push error could be that repo has no remote origin, (local only)
                    if (e.ErrorMessage.Contains("'origin' does not appear to be a git repository"))
                    { // The push error is that repo has no remote origin, (local repo only)
                        // I.e. no remote repo to push to, lets just ignore the push error
                        return R.Ok;
                    }
                    return R.Error($"Failed to push branch {rsp.Name} to remote server", e);
                }

                return R.Ok;
            }
            finally
            {
                Refresh(branchName);
            }
        });

    public void DeleteBranch(string name) =>
        Do(async () =>
        {
            var allBranches = repo.Repo.AllBranches;
            var branch = allBranches.First(b => b.Name == name);

            Server.Branch? localBranch = null;
            Server.Branch? remoteBranch = null;

            if (!branch.IsRemote)
            {
                // Branch is a local branch
                localBranch = branch;
                if (branch.RemoteName != "")
                { //with a corresponding remote branch
                    remoteBranch = allBranches.First(b => b.Name == branch.RemoteName);
                }
            }
            else
            { // Branch is a remote branch
                remoteBranch = branch;
                if (branch.LocalName != "")
                { // with a corresponding local branch
                    localBranch = allBranches.First(b => b.Name == branch.LocalName);
                }
            }

            var isLocal = localBranch != null;
            var isRemote = remoteBranch != null;
            if (!Try(out var rsp, deleteBranchDlg.Show(name, isLocal, isRemote)))
                return R.Ok;

            var newName = "";

            if (rsp.IsRemote && remoteBranch != null)
            {
                var tip = repo.Repo.CommitById[remoteBranch.TipId];
                if (!tip.AllChildIds.Any() && !rsp.IsForce && tip.BranchName == remoteBranch.Name)
                {
                    return R.Error($"Branch {remoteBranch.Name}\nnot fully merged, use force option to delete.");
                }

                if (!Try(out var e, await server.DeleteRemoteBranchAsync(remoteBranch.Name, repo.Path)))
                {
                    return R.Error($"Failed to delete remote branch {remoteBranch.Name}", e);
                }
                newName = remoteBranch.PrimaryBaseName;
            }

            if (rsp.IsLocal && localBranch != null)
            {
                var tip = repo.Repo.CommitById[localBranch.TipId];
                if (!tip.AllChildIds.Any() && !rsp.IsForce && tip.BranchName == localBranch.Name)
                {
                    return R.Error($"Branch {localBranch.Name}\nnot fully merged, use force option to delete.");
                }
                if (!Try(out var e, await server.DeleteLocalBranchAsync(localBranch.Name, rsp.IsForce, repo.Path)))
                {
                    return R.Error($"Failed to delete local branch {localBranch.Name}", e);
                }
                newName = localBranch.PrimaryBaseName;
            }

            Refresh(newName);
            return R.Ok;
        });

    void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    void Do(Func<Task<R>> action) => CommandRunner.Do(progress, action);
}
