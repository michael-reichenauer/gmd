using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;

namespace gmd.Cui.RepoView;


interface IBranchCommands
{
    void ShowBranch(string name, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified, int count = 1);
    void ShowBranch(string name, string showCommitId);
    void HideBranch(string name, bool hideAllBranches = false);

    void SwitchTo(string branchName);
    void SwitchToCommit();

    void DiffWithOtherBranch(string name, bool isFromCurrentCommit, bool isSwitchOrder);
    void DiffBranchesBranch(string branchName1, string branchName2);

    void CreateBranch();
    void CreateBranchFromBranch(string name);
    void CreateBranchFromCommit();
    void DeleteBranch(string name);
    void MergeBranch(string name);
    void RebaseBranchOnto(string onto);

    void PushCurrentBranch();
    void PushBranch(string name);
    void PushAllBranches();
    void PublishCurrentBranch();
    void PullCurrentBranch();
    void PullBranch(string name);
    void PullAllBranches();
    bool CanPushCurrentBranch();
    bool CanPush();
    bool CanPull();
    bool CanPullCurrentBranch();

    void SetBranchManuallyAsync();
    void MoveBranch(string commonName, string otherCommonName, int delta);
    void ChangeBranchColor(string brandName);
}


class BranchCommands : IBranchCommands
{
    readonly IViewRepo repo;
    readonly IProgress progress;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly IDiffView diffView;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IDeleteBranchDlg deleteBranchDlg;
    readonly IBranchColorService branchColorService;
    readonly ISetBranchDlg setBranchDlg;
    readonly IRepoConfig repoConfig;

    public BranchCommands(
        IViewRepo repo,
        IProgress progress,
        IRepoView repoView,
        IServer server,
        IDiffView diffView,
        ICreateBranchDlg createBranchDlg,
        IDeleteBranchDlg deleteBranchDlg,
        IBranchColorService branchColorService,
        ISetBranchDlg setBranchDlg,
        IRepoConfig repoConfig)
    {
        this.repo = repo;
        this.progress = progress;
        this.repoView = repoView;
        this.server = server;
        this.diffView = diffView;
        this.createBranchDlg = createBranchDlg;
        this.deleteBranchDlg = deleteBranchDlg;
        this.branchColorService = branchColorService;
        this.setBranchDlg = setBranchDlg;
        this.repoConfig = repoConfig;
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Server.Commit>? commits = null) =>
         repoView.RefreshAndCommit(addName, commitId, commits);

    public void RefreshAndFetch(string addName = "", string commitId = "") => repoView.RefreshAndFetch(addName, commitId);



    public void ShowBranch(string name, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified, int count = 1)
    {
        var totalCount = 0;
        if (show == ShowBranches.AllActive) totalCount = repo.Repo.AllBranches.Count(b => b.IsGitBranch);
        if (show == ShowBranches.AllActiveAndDeleted) totalCount = repo.Repo.AllBranches.Count;

        if (totalCount > 20)
        {
            if (UI.InfoMessage("Show Branches", $"Do you want to show {totalCount} branches?", 1, new[] { "Yes", "No" }) != 0)
            {
                return;
            }
        }

        Repo newRepo = server.ShowBranch(repo.Repo, name, includeAmbiguous, show, count);
        SetRepo(newRepo, name);
    }

    public void ShowBranch(string name, string showCommitId)
    {
        Repo newRepo = server.ShowBranch(repo.Repo, name, false);
        SetRepoAttCommit(newRepo, showCommitId);
    }

    public void HideBranch(string name, bool hideAllBranches = false)
    {
        Repo newRepo = server.HideBranch(repo.Repo, name, hideAllBranches);
        SetRepo(newRepo);
    }

    public void SwitchTo(string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.SwitchToAsync(repo.Repo, branchName)))
        {
            return R.Error($"Failed to switch to {branchName}", e);
        }

        Refresh(branchName);
        return R.Ok;
    });


    public void MergeBranch(string branchName) => Do(async () =>
    {
        if (!Try(out var commits, out var e, await server.MergeBranchAsync(repo.Repo, branchName)))
            return R.Error($"Failed to merge branch {branchName}", e);

        RefreshAndCommit("", "", commits);
        return R.Ok;
    });


    public void RebaseBranchOnto(string onto) => Do(async () =>
    {
        if (!Try(out var e, await server.RebaseBranchAsync(repo.Repo, onto)))
            return R.Error($"Failed to rebase branch {onto}", e);

        Refresh();
        return R.Ok;
    });


    public void DiffBranchesBranch(string branchName1, string branchName2) => Do(async () =>
    {
        string message = "";
        var branch1 = repo.Repo.BranchByName[branchName1];
        var branch2 = repo.Repo.BranchByName[branchName2];

        var sha1 = branch1.TipId;
        var sha2 = branch2.TipId;
        if (sha1 == Repo.UncommittedId || sha2 == Repo.UncommittedId) return R.Error("Cannot diff while uncommitted changes");

        message = $"Diff '{branch1.NiceNameUnique}' to '{branch2.NiceNameUnique}'";

        if (!Try(out var diff, out var e, await server.GetPreviewMergeDiffAsync(sha2, sha1, message, repo.Path)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, sha1, repo.Path);
        return R.Ok;
    });


    public void DiffWithOtherBranch(string branchName, bool isFromCurrentCommit, bool isSwitchOrder) => Do(async () =>
    {
        string message = "";
        var branch = repo.Repo.BranchByName[branchName];
        var sha1 = branch.TipId;
        var sha2 = isFromCurrentCommit ? repo.RowCommit.Sid : repo.Repo.CurrentBranch().TipId;
        if (sha2 == Repo.UncommittedId) return R.Error("Cannot diff while uncommitted changes");

        if (isSwitchOrder)
        {
            (sha2, sha1) = (sha1, sha2);
            message = $"Diff '{branch.NiceName}' with '{repo.Repo.CurrentBranch().NiceName}'";
        }
        else
        {
            message = $"Diff '{repo.Repo.CurrentBranch().NiceName}' with '{branch.NiceName}'";
        }

        if (!Try(out var diff, out var e, await server.GetPreviewMergeDiffAsync(sha1, sha2, message, repo.Path)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, sha1, repo.Path);
        return R.Ok;
    });


    public void PushCurrentBranch() => Do(async () =>
    {
        var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);

        if (!repo.Repo.Status.IsOk) return R.Error("Commit changes before pushing");
        if (branch == null) return R.Error("No current branch to push");
        if (!branch.HasLocalOnly) return R.Error($"No local changes to push on current branch:\n{branch.NiceNameUnique}");

        if (branch.RemoteName != "")
        {   // Cannot push local branch if remote needs to be pulled first
            var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
            if (remoteBranch != null && remoteBranch.HasRemoteOnly)
            {
                if (0 != UI.ErrorMessage("Push Warning",
                $"""
                Branch '{branch.Name}' 
                has remote commits not yet pulled.
                Pull current remote branch first before pushing,
                or do you want to force push?
                NOTE: be careful!
                """,
                1, "Force Push", "Cancel"))
                {
                    RefreshAndFetch();
                    return R.Ok;
                }
            }

            if (!Try(out var ee, await server.PushCurrentBranchAsync(true, repo.Path)))
            {
                return R.Error($"Failed to push branch:\n{branch.Name}", ee);
            }
        }

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, repo.Path)))
        {
            return R.Error($"Failed to push branch:\n{branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PublishCurrentBranch() => Do(async () =>
    {
        var branch = repo.Repo.ViewBranches.First(b => b.IsCurrent);

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, repo.Path)))
        {
            return R.Error($"Failed to publish branch:\n{branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PushBranchAsync(name, repo.Path)))
        {
            return R.Error($"Failed to push branch:\n{name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public bool CanPush() => repo.Repo.Status.IsOk &&
         repo.Repo.ViewBranches.Any(b => b.HasLocalOnly && !b.HasRemoteOnly);

    public bool CanPushCurrentBranch()
    {
        var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null) return false;

        if (branch.RemoteName != "")
        {   // Cannot push local branch if remote needs to be pulled first
            var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
            if (remoteBranch != null && remoteBranch.HasRemoteOnly) return false;
        }

        return repo.Repo.Status.IsOk && branch != null && branch.HasLocalOnly;
    }


    public void PullCurrentBranch() => Do(async () =>
    {
        var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (!repo.Repo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (branch == null) return R.Error("No current branch to pull");
        if (branch.RemoteName == "") return R.Error("No current remote branch to pull");

        var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
        if (remoteBranch == null || !remoteBranch.HasRemoteOnly) return R.Error(
            "No remote changes on current branch to pull");

        if (!Try(out var e, await server.PullCurrentBranchAsync(repo.Path)))
        {
            return R.Error($"Failed to pull current branch", e);
        }

        Refresh();
        return R.Ok;
    });

    public void PullBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PullBranchAsync(name, repo.Path)))
        {
            return R.Error($"Failed to pull branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanPull() => repo.Repo.Status.IsOk && repo.Repo.ViewBranches.Any(b => b.HasRemoteOnly);

    public bool CanPullCurrentBranch()
    {
        var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null) return false;

        if (branch.RemoteName == "") return false;  // No remote branch to pull

        var remoteBranch = repo.Repo.BranchByName[branch.RemoteName];
        return repo.Repo.Status.IsOk && remoteBranch != null && remoteBranch.HasRemoteOnly;
    }

    public void PushAllBranches() => Do(async () =>
    {
        if (!repo.Repo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (!CanPush()) return R.Error("No local changes to push");

        var branches = repo.Repo.ViewBranches.Where(b => b.HasLocalOnly && !b.HasRemoteOnly)
            .DistinctBy(b => b.PrimaryName);

        foreach (var b in branches)
        {
            if (!Try(out var e, await server.PushBranchAsync(b.Name, repo.Path)))
            {
                Refresh();
                return R.Error($"Failed to push branch {b.Name}", e);
            }
        }

        Refresh();
        return R.Ok;
    });

    public void PullAllBranches() => Do(async () =>
    {
        if (!repo.Repo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (!CanPull()) return R.Error("No remote changes to pull");

        var currentRemoteName = "";
        if (CanPullCurrentBranch())
        {
            Log.Info("Pull current");
            // Need to treat current branch separately
            if (!Try(out var e, await server.PullCurrentBranchAsync(repo.Path)))
            {
                return R.Error($"Failed to pull current branch", e);
            }
            currentRemoteName = repo.Repo.CurrentBranch()?.RemoteName ?? "";
        }

        var branches = repo.Repo.ViewBranches
            .Where(b => b.Name != currentRemoteName && b.IsRemote && !b.IsCurrent && b.HasRemoteOnly)
            .DistinctBy(b => b.PrimaryName);

        Log.Info($"Pull {string.Join(", ", branches)}");
        foreach (var b in branches)
        {
            if (!Try(out var e, await server.PullBranchAsync(b.Name, repo.Path)))
            {
                Refresh();
                return R.Error($"Failed to pull branch {b.Name}", e);
            }

        }

        Refresh();
        return R.Ok;
    });


    public void CreateBranch() => Do(async () =>
    {
        var branchName = "";
        try
        {
            var currentBranchName = repo.Repo.CurrentBranch().Name;
            if (!Try(out var rsp, createBranchDlg.Show(currentBranchName, ""))) return R.Ok;

            if (!Try(out var e, await server.CreateBranchAsync(repo.Repo, rsp.Name, rsp.IsCheckout, repo.Path)))
            {
                return R.Error($"Failed to create branch {rsp.Name}", e);
            }
            branchName = rsp.Name;

            if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(branchName, repo.Path)))
            {
                // The push error could be that repo has no remote origin, (local only)
                if (e.ErrorMessage.Contains("'origin' does not appear to be a git repository"))
                {   // The push error is that repo has no remote origin, (local repo only)
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


    public void CreateBranchFromBranch(string name) => Do(async () =>
    {
        var branchName = "";
        try
        {
            //var currentBranchName = repo.GetCurrentBranch().Name;
            var branch = repo.Repo.BranchByName[name];
            if (branch.LocalName != "") name = branch.LocalName;

            if (!Try(out var rsp, createBranchDlg.Show(name, ""))) return R.Ok;

            if (!Try(out var e, await server.CreateBranchFromBranchAsync(repo.Repo, rsp.Name, name, rsp.IsCheckout, repo.Path)))
            {
                return R.Error($"Failed to create branch {rsp.Name}", e);
            }
            branchName = rsp.Name;

            if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(branchName, repo.Path)))
            {   // The push error could be that repo has no remote origin, (local only)
                if (e.ErrorMessage.Contains("'origin' does not appear to be a git repository"))
                {   // The push error is that repo has no remote origin, (local repo only)
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


    public void CreateBranchFromCommit() => Do(async () =>
    {
        var branchName = "";
        try
        {
            var commit = repo.RowCommit;
            var commitBranchName = commit.BranchName;

            if (!Try(out var rsp, createBranchDlg.Show(commitBranchName, commit.Sid))) return R.Ok;

            if (!Try(out var e,
                await server.CreateBranchFromCommitAsync(repo.Repo, rsp.Name, commit.Id, rsp.IsCheckout, repo.Path)))
            {
                return R.Error($"Failed to create branch {rsp.Name}", e);
            }
            branchName = rsp.Name;

            if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(rsp.Name, repo.Path)))
            {   // The push error could be that repo has no remote origin, (local only)
                if (e.ErrorMessage.Contains("'origin' does not appear to be a git repository"))
                {   // The push error is that repo has no remote origin, (local repo only)
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


    public void DeleteBranch(string name) => Do(async () =>
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
            {    //with a corresponding remote branch
                remoteBranch = allBranches.First(b => b.Name == branch.RemoteName);
            }
        }
        else
        {   // Branch is a remote branch 
            remoteBranch = branch;
            if (branch.LocalName != "")
            {   // with a corresponding local branch
                localBranch = allBranches.First(b => b.Name == branch.LocalName);
            }
        }

        var isLocal = localBranch != null;
        var isRemote = remoteBranch != null;
        if (!Try(out var rsp, deleteBranchDlg.Show(name, isLocal, isRemote))) return R.Ok;

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

    public void ChangeBranchColor(string brandName)
    {
        var b = repo.Repo.BranchByName[brandName];
        if (b.IsMainBranch) return;

        branchColorService.ChangeColor(repo.Repo, b);
        Refresh();
    }

    public void SetBranchManuallyAsync() => Do(async () =>
    {
        var commit = repo.RowCommit;
        if (commit.IsUncommitted) return R.Error($"Not a valid commit");


        var branch = repo.Repo.BranchByName[commit.BranchName];

        var possibleBranches = server.GetPossibleBranchNames(repo.Repo, commit.Id, 20);

        if (!Try(out var name, setBranchDlg.Show(commit.Sid, commit.IsBranchSetByUser, branch.NiceName, possibleBranches))) return R.Ok;

        if (name != "")
        {
            if (!Try(out var e, await server.SetBranchManuallyAsync(repo.Repo, commit.Id, name ?? "")))
            {
                return R.Error($"Failed to set branch name manually", e);
            }
        }
        else if (commit.IsBranchSetByUser)
        {   // name is empty, lets unset name (if set)
            if (!Try(out var ee, await server.UnresolveAmbiguityAsync(repo.Repo, commit.Id)))
            {
                return R.Error($"Failed to unresolve ambiguity", ee);
            }
        }

        Refresh();
        return R.Ok;
    });



    public void MoveBranch(string commonName, string otherCommonName, int delta) => Do(async () =>
    {
        await Task.Yield();

        repoConfig.Set(repo.Path, s =>
        {
            // Filter existing branch orders for the two branches
            var branchOrders = s.BranchOrders.Where(b =>
                !(b.Branch == commonName && b.Other == otherCommonName)
                && !(b.Branch == otherCommonName && b.Other == commonName));

            // Add this branch order
            s.BranchOrders = branchOrders.Append(new BranchOrder()
            {
                Branch = commonName,
                Other = otherCommonName,
                Order = delta
            })
            .ToList();
        });

        Refresh();
        return R.Ok;
    });

    public void SwitchToCommit() => Do(async () =>
    {
        var commit = repo.RowCommit;
        if (!Try(out var e, await server.SwitchToCommitAsync(commit.Id, repo.Path)))
        {
            return R.Error($"Failed to switch to commit {commit.Id}", e);
        }

        Refresh();
        return R.Ok;
    });

    void SetRepo(Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);
    void SetRepoAttCommit(Server.Repo newRepo, string commitId) => repoView.UpdateRepoToAtCommit(newRepo, commitId);


    void Do(Func<Task<R>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await action()))
                {
                    UI.ErrorMessage($"{e.AllErrorMessages()}");
                }
            }
        });
    }
}
