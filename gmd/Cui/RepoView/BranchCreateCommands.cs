using gmd.Common;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// Creating, renaming and deleting branches, i.e. the commands that ask the user for a name or for
// what to delete first, and then run the git command for them.
interface IBranchCreateCommands
{
    void CreateBranch();
    void CreateBranchFromBranch(string name);
    void CreateBranchFromCommit();
    void RenameBranch(string name);
    void DeleteBranch(string name);
}

class BranchCreateCommands : IBranchCreateCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly IRepoConfig repoConfig;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IRenameBranchDlg renameBranchDlg;
    readonly IDeleteBranchDlg deleteBranchDlg;

    public BranchCreateCommands(
        IViewRepo repo,
        IProgress progress,
        IStatusLine status,
        IRepoView repoView,
        IServer server,
        IRepoConfig repoConfig,
        ICreateBranchDlg createBranchDlg,
        IRenameBranchDlg renameBranchDlg,
        IDeleteBranchDlg deleteBranchDlg
    )
    {
        this.repo = repo;
        this.progress = progress;
        this.status = status;
        this.repoView = repoView;
        this.server = server;
        this.repoConfig = repoConfig;
        this.createBranchDlg = createBranchDlg;
        this.renameBranchDlg = renameBranchDlg;
        this.deleteBranchDlg = deleteBranchDlg;
    }

    public void CreateBranch() =>
        Do(async () =>
        {
            var branchName = "";
            try
            {
                var currentBranchName = repo.Repo.CurrentBranch().Name;
                if (createBranchDlg.Show(currentBranchName, "") is not CreateBranchResult rsp)
                    return Result.Ok;

                if (await server.CreateBranchAsync(repo.Repo, rsp.Name, rsp.IsCheckout, repo.Path) is Error e)
                {
                    return new Error($"Failed to create branch {rsp.Name}", e);
                }
                branchName = rsp.Name;

                return rsp.IsPush ? await PushNewBranchAsync(branchName) : Result.Ok;
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

                if (createBranchDlg.Show(name, "") is not CreateBranchResult rsp)
                    return Result.Ok;

                var created = await server.CreateBranchFromBranchAsync(
                    repo.Repo,
                    rsp.Name,
                    name,
                    rsp.IsCheckout,
                    repo.Path
                );
                if (created is Error e)
                {
                    return new Error($"Failed to create branch {rsp.Name}", e);
                }
                branchName = rsp.Name;

                return rsp.IsPush ? await PushNewBranchAsync(branchName) : Result.Ok;
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

                if (createBranchDlg.Show(commitBranchName, commit.Sid) is not CreateBranchResult rsp)
                    return Result.Ok;

                var created = await server.CreateBranchFromCommitAsync(
                    repo.Repo,
                    rsp.Name,
                    commit.Id,
                    rsp.IsCheckout,
                    repo.Path
                );
                if (created is Error e)
                {
                    return new Error($"Failed to create branch {rsp.Name}", e);
                }
                branchName = rsp.Name;

                return rsp.IsPush ? await PushNewBranchAsync(branchName) : Result.Ok;
            }
            finally
            {
                Refresh(branchName);
            }
        });

    // Renames both the local and the remote branch, since renaming only the local branch would
    // hardly be visible: the local branch would still track the old remote branch, and gmd shows
    // the remote branch of a pair, i.e. the branch would still be shown with its old name.
    public void RenameBranch(string name) =>
        Do(async () =>
        {
            var newName = "";
            try
            {
                var (localBranch, remoteBranch) = LocalAndRemoteBranch(name);
                if (localBranch == null)
                {
                    return new Error($"Branch {name} has no local branch to rename");
                }

                var existingNames = repo.Repo.AllBranches.Where(b => b.IsGitBranch).Select(b => b.Name).ToList();
                var oldName = localBranch.NiceName;
                if (renameBranchDlg.Show(oldName, remoteBranch != null, existingNames) is not string rsp)
                    return Result.Ok;

                if (await server.RenameBranchAsync(localBranch.Name, rsp, repo.Path) is Error renameError)
                {
                    return new Error($"Failed to rename branch {oldName} to {rsp}", renameError);
                }
                newName = rsp;
                MigrateRepoConfigNames(oldName, rsp);

                if (remoteBranch == null)
                    return Result.Ok;

                // Renaming a remote branch is pushing the new name and then deleting the old one.
                // The push is also what makes the local branch track the new remote branch, since
                // git leaves the renamed branch tracking the old remote branch.
                if (await server.PushBranchAsync(rsp, repo.Path) is Error pushError)
                {
                    return new Error($"Renamed local branch, but failed to push {rsp} to remote server", pushError);
                }

                if (await server.DeleteRemoteBranchAsync(remoteBranch.Name, repo.Path) is Error deleteError)
                {
                    return new Error(
                        $"Renamed branch and pushed {rsp},\nbut failed to delete {remoteBranch.Name}",
                        deleteError
                    );
                }

                return Result.Ok;
            }
            finally
            {
                Refresh(newName);
            }
        });

    public void DeleteBranch(string name) =>
        Do(async () =>
        {
            var (localBranch, remoteBranch) = LocalAndRemoteBranch(name);

            var isLocal = localBranch != null;
            var isRemote = remoteBranch != null;
            if (deleteBranchDlg.Show(name, isLocal, isRemote) is not DeleteBranchResult rsp)
                return Result.Ok;

            var newName = "";

            if (rsp.IsRemote && remoteBranch != null)
            {
                if (!rsp.IsForce && repo.Repo.HasUnmergedCommits(remoteBranch))
                {
                    return new Error($"Branch {remoteBranch.Name}\nnot fully merged, use force option to delete.");
                }

                if (await server.DeleteRemoteBranchAsync(remoteBranch.Name, repo.Path) is Error e)
                {
                    return new Error($"Failed to delete remote branch {remoteBranch.Name}", e);
                }
                newName = remoteBranch.PrimaryBaseName;
            }

            if (rsp.IsLocal && localBranch != null)
            {
                if (!rsp.IsForce && repo.Repo.HasUnmergedCommits(localBranch))
                {
                    return new Error($"Branch {localBranch.Name}\nnot fully merged, use force option to delete.");
                }
                if (await server.DeleteLocalBranchAsync(localBranch.Name, rsp.IsForce, repo.Path) is Error e)
                {
                    return new Error($"Failed to delete local branch {localBranch.Name}", e);
                }
                newName = localBranch.PrimaryBaseName;
            }

            Refresh(newName);
            return Result.Ok;
        });

    // A branch name can name either the local or the remote branch of a pair, and a command usually
    // needs both of them, so resolve the name to the pair it is part of (either one can be missing)
    (Server.Branch? local, Server.Branch? remote) LocalAndRemoteBranch(string name)
    {
        var allBranches = repo.Repo.AllBranches;
        var branch = allBranches.First(b => b.Name == name);

        if (!branch.IsRemote)
        { // Branch is a local branch, which might have a corresponding remote branch
            var remote = branch.RemoteName != "" ? allBranches.First(b => b.Name == branch.RemoteName) : null;
            return (branch, remote);
        }

        // Branch is a remote branch, which might have a corresponding local branch
        var local = branch.LocalName != "" ? allBranches.First(b => b.Name == branch.LocalName) : null;
        return (local, branch);
    }

    // The branch name is a key in the repo config as well, which git knows nothing about, so a
    // renamed branch would lose its color and its order among the other branches unless the keys
    // are renamed too. Both the local and the remote spelling of the name are renamed, since which
    // of them is the key depends on whether the branch has a remote branch.
    void MigrateRepoConfigNames(string oldNiceName, string newNiceName)
    {
        var newNameByOldName = new Dictionary<string, string>
        {
            { oldNiceName, newNiceName },
            { $"origin/{oldNiceName}", $"origin/{newNiceName}" },
        };
        string Renamed(string name) => newNameByOldName.TryGetValue(name, out var newName) ? newName : name;

        repoConfig.Set(
            repo.Path,
            s =>
            {
                s.Branches = s.Branches.Select(Renamed).Distinct().ToList();
                s.BranchOrders = s
                    .BranchOrders.Select(b => new BranchOrder
                    {
                        Branch = Renamed(b.Branch),
                        Other = Renamed(b.Other),
                        Order = b.Order,
                    })
                    .ToList();

                // Remove and add, and not Select() as above, since a stale color of the new name
                // would otherwise make the dictionary have two equal keys
                foreach (var (oldName, newName) in newNameByOldName)
                {
                    if (s.BranchColors.Remove(oldName, out var colorId))
                    {
                        s.BranchColors[newName] = colorId;
                    }
                }
            }
        );
    }

    void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    // Pushes a branch just created. A repo with no 'origin' is a local only repo with nowhere to
    // push to, which is no failure to create the branch, so that push error is ignored.
    async Task<Result> PushNewBranchAsync(string branchName)
    {
        if (await server.PushBranchAsync(branchName, repo.Path) is not Error e)
            return Result.Ok;
        if (e.Message.Contains("'origin' does not appear to be a git repository"))
            return Result.Ok;
        return new Error($"Failed to push branch {branchName} to remote server", e);
    }

    void Do(Func<Task<Result>> action) => CommandRunner.Do(progress, status, repo, action);
}
