using gmd.Common;
using gmd.Cui.Diff;
using gmd.Git;
using gmd.Installation;
using gmd.Server;
//using Terminal.Gui;

namespace gmd.Cui.Common;

interface IRepoCommands
{
    void ShowBranch(string name, bool includeAmbiguous);
    void HideBranch(string name);
    void SwitchTo(string branchName);
    void SwitchToCommit();

    void ToggleDetails();

    void ShowAbout();
    void ShowHelp();
    void ShowFileHistory();
    void Filter();
    void ShowBrowseDialog();
    void ChangeBranchColor();
    void UpdateRelease();
    void Clone();

    void ShowRepo(string path);
    void Refresh(string addName = "", string commitId = "");
    void RefreshAndFetch(string addName = "", string commitId = "");

    void ShowUncommittedDiff(bool isFromDiff = false);
    void ShowCurrentRowDiff();
    void DiffWithOtherBranch(string name, bool isFromCurrentCommit, bool isSwitchOrder);

    void Commit(bool isAmend, IReadOnlyList<Server.Commit>? commits = null);
    void CommitFromMenu(bool isAmend);

    void CreateBranch();
    void CreateBranchFromCommit();
    void DeleteBranch(string name);
    void MergeBranch(string name);
    void CherryPic(string id);

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

    void UndoCommit(string id);
    void UncommitLastCommit();
    void UndoAllUncommittedChanged();
    void UndoUncommittedFile(string path);
    void UndoUncommittedFiles(IReadOnlyList<string> paths);
    void CleanWorkingFolder();
    bool CanUndoUncommitted();
    bool CanUndoCommit();
    bool CanUncommitLastCommit();

    void ResolveAmbiguity(Server.Branch branch, string humanName);
    void UndoSetBranch(string commitId);
    void SetBranchManuallyAsync();
    void MoveBranch(string commonName, string otherCommonName, int delta);

    void Stash();
    void StashPop(string name);
    void StashDiff(string name);
    void StashDrop(string name);

    void AddTag();
    void DeleteTag(string name);
    void CopyCommitId();
    void CopyCommitMessage();
}

class RepoCommands : IRepoCommands
{
    readonly IServer server;
    readonly IProgress progress;
    readonly IFilterDlg filterDlg;
    readonly ICommitDlg commitDlg;
    readonly ICloneDlg cloneDlg;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IDeleteBranchDlg deleteBranchDlg;
    readonly IAddTagDlg addTagDlg;
    readonly ISetBranchDlg setBranchDlg;
    readonly IAboutDlg aboutDlg;
    readonly IHelpDlg helpDlg;
    readonly IDiffView diffView;
    readonly IState states;
    readonly IUpdater updater;
    readonly IGit git;
    readonly IRepoState repoState;
    readonly IBranchColorService branchColorService;
    readonly IRepo repo;
    readonly Repo serverRepo;
    readonly IRepoView repoView;

    string repoPath => serverRepo.Path;
    Server.Status status => serverRepo.Status;

    internal RepoCommands(
        IRepo repo,
        Server.Repo serverRepo,
        IRepoView repoView,
        IServer server,
        IProgress progress,
        IFilterDlg filterDlg,
        ICommitDlg commitDlg,
        ICloneDlg cloneDlg,
        ICreateBranchDlg createBranchDlg,
        IDeleteBranchDlg deleteBranchDlg,
        IAddTagDlg addTagDlg,
        ISetBranchDlg setBranchDlg,
        IAboutDlg aboutDlg,
        IHelpDlg helpDlg,
        IDiffView diffView,
        IState states,
        IUpdater updater,
        IGit git,
        IRepoState repoState,
        IBranchColorService branchColorService)
    {
        this.repo = repo;
        this.serverRepo = serverRepo;
        this.repoView = repoView;
        this.server = server;
        this.progress = progress;
        this.filterDlg = filterDlg;
        this.commitDlg = commitDlg;
        this.cloneDlg = cloneDlg;
        this.createBranchDlg = createBranchDlg;
        this.deleteBranchDlg = deleteBranchDlg;
        this.addTagDlg = addTagDlg;
        this.setBranchDlg = setBranchDlg;
        this.aboutDlg = aboutDlg;
        this.helpDlg = helpDlg;
        this.diffView = diffView;
        this.states = states;
        this.updater = updater;
        this.git = git;
        this.repoState = repoState;
        this.branchColorService = branchColorService;
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);
    public void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Server.Commit>? commits = null) =>
        repoView.RefreshAndCommit(addName, commitId, commits);

    public void RefreshAndFetch(string addName = "", string commitId = "") => repoView.RefreshAndFetch(addName, commitId);



    public void ShowRepo(string path) => Do(async () =>
     {
         if (!Try(out var e, await repoView.ShowRepoAsync(path)))
         {
             return R.Error($"Failed to open repo at {path}", e);
         }
         return R.Ok;
     });


    public void ShowBrowseDialog() => Do(async () =>
   {
       // Parent folders to recent work folders, usually other repos there as well
       var recentFolders = states.Get().RecentParentFolders.Where(Files.DirExists).ToList();

       var browser = new FolderBrowseDlg();
       if (!Try(out var path, browser.Show(recentFolders))) return R.Ok;

       if (!Try(out var e, await repoView.ShowRepoAsync(path)))
       {
           return R.Error($"Failed to open repo at {path}", e);
       }
       return R.Ok;
   });

    public void ShowAbout() => aboutDlg.Show();
    public void ShowHelp() => helpDlg.Show();

    public void CommitFromMenu(bool isAmend)
    {
        // For some unknown reason, calling commit directly from the menu will
        // Show the commit dialog, but diff will not work since async/await does not seem to work
        // However wrapping with a timeout seems to work as desired.
        UI.AddTimeout(TimeSpan.FromMilliseconds(100), (_) =>
        {
            Commit(isAmend);
            return false;
        });
    }

    public void Commit(bool isAmend, IReadOnlyList<Server.Commit>? commits = null) => Do(async () =>
    {
        if (repo.CurrentBranch?.IsDetached == true)
        {
            UI.ErrorMessage("Cannot commit in detached head state.\nPlease create/switch to a branch first.");
            return R.Ok;
        }

        if (!isAmend && repo.Status.IsOk) return R.Ok;
        if (isAmend && !repo.GetCurrentCommit().IsAhead) return R.Ok;

        if (!CheckBinaryOrLargeAddedFiles()) return R.Ok;

        if (!commitDlg.Show(repo, isAmend, commits, out var message)) return R.Ok;

        if (!Try(out var e, await server.CommitAllChangesAsync(message, isAmend, repoPath)))
        {
            return R.Error($"Failed to commit", e);
        }

        Refresh();
        return R.Ok;
    });


    public void Clone() => Do(async () =>
    {
        // Parent folders to recent work folders, usually other repos there as well
        var recentFolders = states.Get().RecentParentFolders.Where(Files.DirExists).ToList();

        if (!Try(out var r, out var e, cloneDlg.Show(recentFolders))) return R.Ok;
        (var uri, var path) = r;

        if (!Try(out e, await server.CloneAsync(uri, path, repoPath)))
        {
            return R.Error($"Failed to clone", e);
        }

        if (!Try(out e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });


    public void ShowBranch(string name, bool includeAmbiguous)
    {
        Server.Repo newRepo = server.ShowBranch(serverRepo, name, includeAmbiguous);
        SetRepo(newRepo, name);

    }

    public void HideBranch(string name)
    {
        Server.Repo newRepo = server.HideBranch(serverRepo, name);
        SetRepo(newRepo);
    }

    public void SwitchTo(string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.SwitchToAsync(serverRepo, branchName)))
        {
            return R.Error($"Failed to switch to {branchName}", e);
        }
        Refresh();
        return R.Ok;
    });


    public void Filter() => Do(async () =>
     {
         if (!Try(out var commit, out var e, filterDlg.Show(repo))) return R.Ok;
         await Task.Delay(0);

         Refresh(commit.BranchName, commit.Id);
         return R.Ok;
     });


    public void ToggleDetails() => repoView.ToggleDetails();


    public bool CanUndoUncommitted() => !serverRepo.Status.IsOk;


    public void UndoUncommittedFile(string path) => Do(async () =>
    {
        if (!Try(out var e, await server.UndoUncommittedFileAsync(path, repoPath)))
        {
            return R.Error($"Failed to undo {path}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UndoUncommittedFiles(IReadOnlyList<string> paths) => Do(async () =>
    {
        foreach (var path in paths)
        {
            if (!Try(out var e, await server.UndoUncommittedFileAsync(path, repoPath)))
            {
                Refresh();
                return R.Error($"Failed to undo {path}", e);
            }
        }

        Refresh();
        return R.Ok;
    });

    public void UndoAllUncommittedChanged() => Do(async () =>
    {
        if (!Try(out var e, await server.UndoAllUncommittedChangesAsync(repoPath)))
        {
            return R.Error($"Failed to undo all changes", e);
        }

        Refresh();
        return R.Ok;
    });

    public void CleanWorkingFolder() => Do(async () =>
    {
        if (UI.InfoMessage("Clean Working Folder",
            "Do you want to reset folder\nand delete all untracked files and folders?", 1, new[] { "Yes", "No" })
            != 0)
        {
            return R.Ok;
        }

        if (!Try(out var e, await server.CleanWorkingFolderAsync(repoPath)))
        {
            return R.Error($"Failed to clean working folder", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UndoCommit(string id) => Do(async () =>
    {
        if (!CanUndoCommit()) return R.Ok;
        var commit = repo.Commit(id);
        var parentIndex = commit.ParentIds.Count == 1 ? 0 : 1;

        if (!Try(out var e, await server.UndoCommitAsync(id, parentIndex, repoPath)))
        {
            return R.Error($"Failed to undo commit", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanUndoCommit() => serverRepo.Status.IsOk;


    public void UncommitLastCommit() => Do(async () =>
    {
        if (!CanUncommitLastCommit()) return R.Ok;

        if (!Try(out var e, await server.UncommitLastCommitAsync(repoPath)))
        {
            return R.Error($"Failed to undo commit", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanUncommitLastCommit()
    {
        if (!serverRepo.Commits.Any()) return false;

        var c = serverRepo.Commits[0];
        var b = serverRepo.BranchByName[serverRepo.Commits[0].BranchName];
        return status.IsOk && c.IsAhead || (!b.IsRemote && b.RemoteName == "");
    }

    public void ResolveAmbiguity(Server.Branch branch, string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.ResolveAmbiguityAsync(serverRepo, branch.Name, branchName)))
        {
            return R.Error($"Failed to resolve ambiguity", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UndoSetBranch(string commitId) => Do(async () =>
    {
        if (!Try(out var e, await server.UnresolveAmbiguityAsync(serverRepo, commitId)))
        {
            return R.Error($"Failed to unresolve ambiguity", e);
        }

        Refresh();
        return R.Ok;
    });

    public void ShowUncommittedDiff(bool isFromDiff = false) => ShowDiff(Server.Repo.UncommittedId, isFromDiff);

    public void ShowCurrentRowDiff() => ShowDiff(repo.RowCommit.Id);

    public void ShowDiff(string commitId, bool isFromDiff = false) => Do(async () =>
    {
        if (!Try(out var diff, out var e, await server.GetCommitDiffAsync(commitId, repoPath)))
        {
            return R.Error($"Failed to get diff", e);
        }

        UI.Post(() =>
        {
            if (diffView.Show(diff, commitId, repoPath))
            {
                if (!isFromDiff)
                {
                    RefreshAndCommit();
                }

            };
        });
        return R.Ok;
    });

    public void ShowFileHistory() => Do(async () =>
    {
        if (!Try(out var files, out var e, await repo.GetFilesAsync()))
        {
            return R.Error($"Failed to get files", e);
        }

        var browser = new FileBrowseDlg();
        if (!Try(out var path, browser.Show(files))) return R.Ok;

        if (!Try(out var diffs, out e, await server.GetFileDiffAsync(path, repoPath)))
        {
            return R.Error($"Failed to show file history", e);
        }

        diffView.Show(diffs);
        return R.Ok;
    });


    public void MergeBranch(string branchName) => Do(async () =>
    {
        if (!Try(out var commits, out var e, await server.MergeBranchAsync(serverRepo, branchName)))
            return R.Error($"Failed to merge branch {branchName}", e);


        RefreshAndCommit("", "", commits);
        return R.Ok;
    });


    public void DiffWithOtherBranch(string branchName, bool isFromCurrentCommit, bool isSwitchOrder) => Do(async () =>
    {
        if (repo.CurrentBranch == null) return R.Ok;
        string message = "";
        var sha1 = repo.Branch(branchName).TipId;
        var sha2 = isFromCurrentCommit ? repo.RowCommit.Sid : repo.CurrentBranch.TipId;
        if (sha2 == Repo.UncommittedId) return R.Error("Cannot diff while uncommitted changes");

        if (isSwitchOrder)
        {
            var sh = sha1;
            sha1 = sha2;
            sha2 = sh;
            message = $"Diff '{branchName}' with '{repo.CurrentBranch.CommonName}'";
        }
        else
        {
            message = $"Diff '{repo.CurrentBranch.CommonName}' with '{branchName}'";
        }

        if (!Try(out var diff, out var e, await server.GetPreviewMergeDiffAsync(sha1, sha2, message, repoPath)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, sha1, repoPath);
        return R.Ok;
    });


    public void CherryPic(string id) => Do(async () =>
    {
        if (!Try(out var e, await server.CherryPickAsync(id, repoPath)))
        {
            return R.Error($"Failed to cherry pic {id.Sid()}", e);
        }

        RefreshAndCommit();
        return R.Ok;
    });


    public void PushCurrentBranch() => Do(async () =>
    {
        var branch = serverRepo.Branches.FirstOrDefault(b => b.IsCurrent);

        if (!serverRepo.Status.IsOk) return R.Error("Commit changes before pushing");
        if (branch == null) return R.Error("No current branch to push");
        if (!branch.HasLocalOnly) return R.Error("No local changes to push on current branch");

        if (branch.RemoteName != "")
        {   // Cannot push local branch if remote needs to be pulled first
            var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
            if (remoteBranch != null && remoteBranch.HasRemoteOnly)
                return R.Error("Pull current remote branch first before pushing");
        }

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, repoPath)))
        {
            return R.Error($"Failed to push branch {branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PublishCurrentBranch() => Do(async () =>
    {
        var branch = serverRepo.Branches.First(b => b.IsCurrent);

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, repoPath)))
        {
            return R.Error($"Failed to publish branch {branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PushBranchAsync(name, repoPath)))
        {
            return R.Error($"Failed to push branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public bool CanPush() => serverRepo.Status.IsOk &&
         repo.Branches.Any(b => b.HasLocalOnly && !b.HasRemoteOnly);

    public bool CanPushCurrentBranch()
    {
        var branch = serverRepo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null) return false;

        if (branch.RemoteName != "")
        {   // Cannot push local branch if remote needs to be pulled first
            var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
            if (remoteBranch != null && remoteBranch.HasRemoteOnly) return false;
        }

        return serverRepo.Status.IsOk && branch != null && branch.HasLocalOnly;
    }


    public void PullCurrentBranch() => Do(async () =>
    {
        var branch = serverRepo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (!serverRepo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (branch == null) return R.Error("No current branch to pull");
        if (branch.RemoteName == "") return R.Error("No current remote branch to pull");

        var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
        if (remoteBranch == null || !remoteBranch.HasRemoteOnly) return R.Error(
            "No remote changes on current branch to pull");

        if (!Try(out var e, await server.PullCurrentBranchAsync(repoPath)))
        {
            return R.Error($"Failed to pull current branch", e);
        }

        Refresh();
        return R.Ok;
    });

    public void PullBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PullBranchAsync(name, repoPath)))
        {
            return R.Error($"Failed to pull branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanPull() => status.IsOk && repo.Branches.Any(b => b.HasRemoteOnly);

    public bool CanPullCurrentBranch()
    {
        var branch = serverRepo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null) return false;

        if (branch.RemoteName == "") return false;  // No remote branch to pull

        var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
        return status.IsOk && remoteBranch != null && remoteBranch.HasRemoteOnly;
    }

    public void PushAllBranches() => Do(async () =>
    {
        if (!serverRepo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (!CanPush()) return R.Error("No local changes to push");

        var branches = repo.Branches.Where(b => b.HasLocalOnly && !b.HasRemoteOnly)
            .DistinctBy(b => b.CommonName);

        foreach (var b in branches)
        {
            if (!Try(out var e, await server.PushBranchAsync(b.Name, repoPath)))
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
        if (!serverRepo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (!CanPull()) return R.Error("No remote changes to pull");

        var currentRemoteName = "";
        if (CanPullCurrentBranch())
        {
            Log.Info("Pull current");
            // Need to treat current branch separately
            if (!Try(out var e, await server.PullCurrentBranchAsync(repoPath)))
            {
                return R.Error($"Failed to pull current branch", e);
            }
            currentRemoteName = repo.CurrentBranch?.RemoteName ?? "";
        }

        var branches = repo.Branches
            .Where(b => b.Name != currentRemoteName && b.IsRemote && !b.IsCurrent && b.HasRemoteOnly)
            .DistinctBy(b => b.CommonName);

        Log.Info($"Pull {string.Join(", ", branches)}");
        foreach (var b in branches)
        {
            if (!Try(out var e, await server.PullBranchAsync(b.Name, repoPath)))
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
        var currentBranchName = repo.GetCurrentBranch().Name;
        if (!Try(out var rsp, createBranchDlg.Show(currentBranchName, ""))) return R.Ok;

        if (!Try(out var e, await server.CreateBranchAsync(serverRepo, rsp.Name, rsp.IsCheckout, repoPath)))
        {
            return R.Error($"Failed to create branch {rsp.Name}", e);
        }

        if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(rsp.Name, repoPath)))
        {
            return R.Error($"Failed to push branch {rsp.Name} to remote server", e);
        }

        Refresh(rsp.Name);
        return R.Ok;
    });


    public void CreateBranchFromCommit() => Do(async () =>
    {
        var commit = repo.RowCommit;
        var branchName = commit.BranchName;

        if (!Try(out var rsp, createBranchDlg.Show(branchName, commit.Sid))) return R.Ok;

        if (!Try(out var e,
            await server.CreateBranchFromCommitAsync(serverRepo, rsp.Name, commit.Id, rsp.IsCheckout, repoPath)))
        {
            return R.Error($"Failed to create branch {rsp.Name}", e);
        }

        if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(rsp.Name, repoPath)))
        {
            return R.Error($"Failed to push branch {rsp.Name} to remote server", e);
        }

        Refresh(rsp.Name);
        return R.Ok;
    });

    public void Stash() => Do(async () =>
    {
        if (repo.Status.IsOk) return R.Ok;

        if (!Try(out var e, await server.StashAsync(repoPath)))
        {
            return R.Error($"Failed to stash changes", e);
        }

        Refresh();
        return R.Ok;
    });

    public void StashPop(string name) => Do(async () =>
    {
        if (!repo.Status.IsOk) return R.Ok;

        if (!Try(out var e, await server.StashPopAsync(name, repoPath)))
        {
            return R.Error($"Failed to pop stash {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void StashDiff(string name) => Do(async () =>
    {
        if (!Try(out var diff, out var e, await server.GetStashDiffAsync(name, repoPath)))
        {
            return R.Error($"Failed to diff stash {name}", e);
        }

        diffView.Show(diff, name, repoPath);
        return R.Ok;
    });

    public void StashDrop(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.StashDropAsync(name, repoPath)))
        {
            return R.Error($"Failed to drop stash {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void DeleteBranch(string name) => Do(async () =>
    {
        var allBranches = repo.GetAllBranches();
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
            var tip = repo.Commit(remoteBranch.TipId);
            if (!tip.ChildIds.Any() && !rsp.IsForce && tip.BranchName == remoteBranch.Name)
            {
                return R.Error($"Branch {remoteBranch.Name}\nnot fully merged, use force option to delete.");
            }

            if (!Try(out var e, await server.DeleteRemoteBranchAsync(remoteBranch.Name, repoPath)))
            {
                return R.Error($"Failed to delete remote branch {remoteBranch.Name}", e);
            }
            newName = $"{remoteBranch.CommonName}:{remoteBranch.TipId.Sid()}";
        }

        if (rsp.IsLocal && localBranch != null)
        {
            var tip = repo.Commit(localBranch.TipId);
            if (!tip.ChildIds.Any() && !rsp.IsForce && tip.BranchName == localBranch.Name)
            {
                return R.Error($"Branch {localBranch.Name}\nnot fully merged, use force option to delete.");
            }
            if (!Try(out var e, await server.DeleteLocalBranchAsync(localBranch.Name, rsp.IsForce, repoPath)))
            {
                return R.Error($"Failed to delete local branch {localBranch.Name}", e);
            }
            newName = $"{localBranch.CommonName}:{localBranch.TipId.Sid()}";
        }

        Refresh(newName);
        return R.Ok;
    });


    public void UpdateRelease() => Do(async () =>
    {
        await Task.Yield();

        var releases = states.Get().Releases;
        var typeText = releases.IsPreview ? "(preview)" : "";
        string msg = $"A new release is available:\n" +
            $"New Version:     {releases.LatestVersion} {typeText}\n" +
            $"\nCurrent Version: {Build.Version()}\n\n" +
            "Do you want to update?";
        var button = UI.InfoMessage("New Release", msg, new[] { "Yes", "No" });
        if (button != 0)
        {
            Log.Info($"Skip update");
            return R.Ok;
        }
        Log.Info($"Updating release ...");
        if (!Try(out var _, out var e, await updater.UpdateAsync())) return e;

        UI.InfoMessage("Restart Required", "A program restart is required,\nplease start gmd again.");
        UI.Shutdown();

        return R.Ok;
    });


    public void ChangeBranchColor()
    {
        var b = repo.Branch(repo.RowCommit.BranchName);
        if (b.IsMainBranch) return;

        branchColorService.ChangeColor(repo.Repo, b);
        Refresh();
    }


    void SetRepo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);

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

    bool CheckBinaryOrLargeAddedFiles()
    {
        var addFiles = serverRepo.Status.AddedFiles.ToList();
        var addAndModified = addFiles.Concat(serverRepo.Status.ModifiedFiles).ToList();

        var binaryFiles = addAndModified.Where(f => !Files.IsText(Path.Join(repoPath, f))).ToList();

        if (binaryFiles.Any())
        {
            var msg = $"There are {binaryFiles.Count} binary mdified files:\n" +
            $"  {string.Join("\n  ", binaryFiles)}" +
            "\n\nDo you want to commit them as they are\nor first undo/revert them and then commit?";
            if (0 != UI.InfoMessage("Binary Files Detected !", msg, 1, new[] { "Commit", "Undo" }))
            {
                UI.Post(() =>
                {
                    UndoUncommittedFiles(binaryFiles);
                    RefreshAndCommit();
                });
                return false;
            }
        }

        var largeFiles = addFiles
            .Where(f => !binaryFiles.Contains(f))
            .Where(f => Files.IsLarger(Path.Join(repoPath, f), 100 * 1000)).ToList();

        if (largeFiles.Any())
        {
            var msg = $"There are {largeFiles.Count} modified large files:\n"
            + $" ({largeFiles.Count}):  \n{string.Join("\n  ", largeFiles)}" +
            "\n\nDo you want to continue?";
            if (0 != UI.InfoMessage("Large Files Detected !", msg, 1, new[] { "Yes", "No" }))
            {
                return false;
            }
        }

        return true;
    }

    public void AddTag() => Do(async () =>
    {
        var commit = repo.RowCommit;
        var branch = repo.Branch(commit.BranchName);
        var isPushable = branch.IsRemote || branch.RemoteName != "";

        if (commit.IsUncommitted) return R.Ok;

        if (!Try(out var tag, addTagDlg.Show())) return R.Ok;

        if (!Try(out var e, await server.AddTagAsync(tag, commit.Id, isPushable, repoPath)))
        {
            return R.Error($"Failed to add tag {tag}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void DeleteTag(string name) => Do(async () =>
    {
        var commit = repo.RowCommit;
        var branch = repo.Branch(commit.BranchName);
        var isPushable = branch.IsRemote || branch.RemoteName != "";

        if (!Try(out var e, await server.RemoveTagAsync(name, isPushable, repoPath)))
        {
            return R.Error($"Failed to delete tag {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void SetBranchManuallyAsync() => Do(async () =>
    {
        if (repo.RowCommit.ChildIds.Count() <= 1) return R.Ok;

        var commit = repo.RowCommit;

        if (!Try(out var name, setBranchDlg.Show())) return R.Ok;

        if (!Try(out var e, await server.SetBranchManuallyAsync(serverRepo, commit.Id, name)))
        {
            return R.Error($"Failed to set branch name manually", e);
        }

        Refresh();
        return R.Ok;
    });


    public void MoveBranch(string commonName, string otherCommonName, int delta) => Do(async () =>
    {
        await Task.Yield();

        repoState.Set(repoPath, s =>
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
        if (!Try(out var e, await server.SwitchToCommitAsync(commit.Id, repoPath)))
        {
            return R.Error($"Failed to switch to commit {commit.Id}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void CopyCommitId() => Do(async () =>
    {
        await Task.Yield();
        var commit = repo.RowCommit;
        if (!Try(out var e, Clipboard.Set(commit.Id)))
            return R.Error($"Clipboard copy not supported on this platform", e);

        return R.Ok;
    });

    public void CopyCommitMessage() => Do(async () =>
    {
        await Task.Yield();
        var commit = repo.RowCommit;
        if (!Try(out var e, Clipboard.Set(commit.Message.TrimEnd())))
            return R.Error($"Clipboard copy not supported on this platform", e);

        return R.Ok;
    });
}

