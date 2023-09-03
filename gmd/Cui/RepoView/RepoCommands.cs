using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Git;
using gmd.Installation;
using gmd.Server;


namespace gmd.Cui.RepoView;

interface IRepoCommands
{
    void ShowBranch(string name, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified, int count = 1);
    void ShowBranch(string name, string showCommitId);
    void HideBranch(string name, bool hideAllBranches = false);
    void SwitchTo(string branchName);
    void SwitchToCommit();

    void ToggleDetails();

    void ShowAbout();
    void ShowHelp();
    void ShowFileHistory();
    void Filter();
    void ShowBrowseDialog();
    void ChangeBranchColor(string brandName);
    void UpdateRelease();
    void Clone();
    void InitRepo();

    void ShowRepo(string path);
    void Refresh(string addName = "", string commitId = "");
    void RefreshAndFetch(string addName = "", string commitId = "");

    void ShowUncommittedDiff(bool isFromCommit = false);
    void ShowCurrentRowDiff();
    void ShowDiff(string commitId, bool isFromCommit = false);
    void DiffWithOtherBranch(string name, bool isFromCurrentCommit, bool isSwitchOrder);
    void DiffBranchesBranch(string branchName1, string branchName2);

    void Commit(bool isAmend, IReadOnlyList<Server.Commit>? commits = null);
    void CommitFromMenu(bool isAmend);

    void CreateBranch();
    void CreateBranchFromBranch(string name);
    void CreateBranchFromCommit();
    void DeleteBranch(string name);
    void MergeBranch(string name);
    void CherryPick(string id);

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
    readonly ICommitDlg commitDlg;
    readonly ICloneDlg cloneDlg;
    readonly IInitRepoDlg initRepoDlg;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IDeleteBranchDlg deleteBranchDlg;
    readonly IAddTagDlg addTagDlg;
    readonly ISetBranchDlg setBranchDlg;
    readonly IAboutDlg aboutDlg;
    readonly IHelpDlg helpDlg;
    readonly IDiffView diffView;
    readonly Config config;
    readonly IUpdater updater;
    readonly IRepoConfig repoConfig;
    readonly IBranchColorService branchColorService;
    readonly IRepo repo;
    readonly Repo serverRepo;
    readonly IRepoView repoView;

    string RepoPath => serverRepo.Path;
    Server.Status Status => serverRepo.Status;

    internal RepoCommands(
        IRepo repo,
        Server.Repo serverRepo,
        IRepoView repoView,
        IServer server,
        IProgress progress,
        ICommitDlg commitDlg,
        ICloneDlg cloneDlg,
        IInitRepoDlg initRepoDlg,
        ICreateBranchDlg createBranchDlg,
        IDeleteBranchDlg deleteBranchDlg,
        IAddTagDlg addTagDlg,
        ISetBranchDlg setBranchDlg,
        IAboutDlg aboutDlg,
        IHelpDlg helpDlg,
        IDiffView diffView,
        Config config,
        IUpdater updater,
        IRepoConfig repoConfig,
        IBranchColorService branchColorService)
    {
        this.repo = repo;
        this.serverRepo = serverRepo;
        this.repoView = repoView;
        this.server = server;
        this.progress = progress;
        this.commitDlg = commitDlg;
        this.cloneDlg = cloneDlg;
        this.initRepoDlg = initRepoDlg;
        this.createBranchDlg = createBranchDlg;
        this.deleteBranchDlg = deleteBranchDlg;
        this.addTagDlg = addTagDlg;
        this.setBranchDlg = setBranchDlg;
        this.aboutDlg = aboutDlg;
        this.helpDlg = helpDlg;
        this.diffView = diffView;
        this.config = config;
        this.updater = updater;
        this.repoConfig = repoConfig;
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
       var browser = new FolderBrowseDlg();
       if (!Try(out var path, browser.Show(config.ResentParentFolders()))) return R.Ok;

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
        if (!isAmend && repo.Repo.Status.IsOk) return R.Ok;
        if (isAmend && !repo.Repo.CurrentCommit().IsAhead) return R.Ok;

        if (repo.Repo.CurrentBranch().IsDetached == true)
        {
            UI.ErrorMessage("Cannot commit in detached head state.\nPlease create/switch to a branch first.");
            return R.Ok;
        }

        if (!await CheckBinaryOrLargeAddedFilesAsync()) return R.Ok;

        if (!commitDlg.Show(repo, isAmend, commits, out var message)) return R.Ok;

        if (!Try(out var e, await server.CommitAllChangesAsync(message, isAmend, RepoPath)))
        {
            return R.Error($"Failed to commit", e);
        }

        Refresh();
        return R.Ok;
    });


    public void Clone() => Do(async () =>
    {
        if (!Try(out var r, out var e, cloneDlg.Show(config.ResentParentFolders()))) return R.Ok;
        (var uri, var path) = r;

        if (!Try(out e, await server.CloneAsync(uri, path, RepoPath)))
        {
            return R.Error($"Failed to clone", e);
        }

        if (!Try(out e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });

    public void InitRepo() => Do(async () =>
    {
        if (!Try(out var path, out var e, initRepoDlg.Show(config.ResentParentFolders()))) return R.Ok;

        if (!Try(out e, await server.InitRepoAsync(path, RepoPath)))
        {
            return R.Error($"Failed to init repo", e);
        }

        if (!Try(out e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });


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

        Repo newRepo = server.ShowBranch(serverRepo, name, includeAmbiguous, show, count);
        SetRepo(newRepo, name);
    }

    public void ShowBranch(string name, string showCommitId)
    {
        Repo newRepo = server.ShowBranch(serverRepo, name, false);
        SetRepoAttCommit(newRepo, showCommitId);
    }

    public void HideBranch(string name, bool hideAllBranches = false)
    {
        Repo newRepo = server.HideBranch(serverRepo, name, hideAllBranches);
        SetRepo(newRepo);
    }

    public void SwitchTo(string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.SwitchToAsync(serverRepo, branchName)))
        {
            return R.Error($"Failed to switch to {branchName}", e);
        }

        Refresh(branchName);
        return R.Ok;
    });


    public void Filter() => Do(async () =>
     {
         await Task.CompletedTask;
         repoView.ShowFilter();
         return R.Ok;
     });


    public void ToggleDetails() => repoView.ToggleDetails();


    public bool CanUndoUncommitted() => !serverRepo.Status.IsOk;


    public void UndoUncommittedFile(string path) => Do(async () =>
    {
        if (!Try(out var e, await server.UndoUncommittedFileAsync(path, RepoPath)))
        {
            return R.Error($"Failed to undo {path}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UndoUncommittedFiles(IReadOnlyList<string> paths) => Do(async () =>
    {
        await UndoUncommittedFilesAsync(paths);
        Refresh();
        return R.Ok;
    });


    public async Task UndoUncommittedFilesAsync(IReadOnlyList<string> paths)
    {
        var failedPath = new List<string>();
        foreach (var path in paths)
        {
            if (!Try(out var _, await server.UndoUncommittedFileAsync(path, RepoPath)))
            {
                failedPath.Add(path);
            }
        }
        if (failedPath.Any())
        {
            UI.ErrorMessage($"Failed to undo {failedPath.Count} files:\n{string.Join("\n", failedPath)}");
        }
    }


    public void UndoAllUncommittedChanged() => Do(async () =>
    {
        if (!Try(out var e, await server.UndoAllUncommittedChangesAsync(RepoPath)))
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

        if (!Try(out var e, await server.CleanWorkingFolderAsync(RepoPath)))
        {
            return R.Error($"Failed to clean working folder", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UndoCommit(string id) => Do(async () =>
    {
        if (!CanUndoCommit()) return R.Ok;
        var commit = repo.Repo.CommitById[id];
        var parentIndex = commit.ParentIds.Count == 1 ? 0 : 1;

        if (!Try(out var e, await server.UndoCommitAsync(id, parentIndex, RepoPath)))
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

        if (!Try(out var e, await server.UncommitLastCommitAsync(RepoPath)))
        {
            return R.Error($"Failed to undo commit", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanUncommitLastCommit()
    {
        if (!serverRepo.ViewCommits.Any()) return false;

        var c = serverRepo.ViewCommits[0];
        var b = serverRepo.BranchByName[serverRepo.ViewCommits[0].BranchName];
        return Status.IsOk && c.IsAhead || (!b.IsRemote && b.RemoteName == "");
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

    public void ShowUncommittedDiff(bool isFromCommit = false) => ShowDiff(Repo.UncommittedId, isFromCommit);

    public void ShowCurrentRowDiff() => ShowDiff(repo.RowCommit.Id);

    public void ShowDiff(string commitId, bool isFromCommit = false) => Do(async () =>
    {
        if (commitId == Repo.EmptyRepoCommitId) return R.Ok;

        if (!Try(out var diff, out var e, await server.GetCommitDiffAsync(commitId, RepoPath)))
        {
            return R.Error($"Failed to get diff", e);
        }

        UI.Post(() =>
        {
            var rsp = diffView.Show(diff, commitId, RepoPath);
            if (rsp == DiffResult.Commit && !isFromCommit)
            {
                RefreshAndCommit();
            }
            else if (rsp == DiffResult.Refresh && !isFromCommit)
            {
                Refresh();
            }
        });
        return R.Ok;
    });

    public void ShowFileHistory() => Do(async () =>
    {
        if (!Try(out var files, out var e, await GetFilesAsync()))
        {
            return R.Error($"Failed to get files", e);
        }

        var browser = new FileBrowseDlg();
        if (!Try(out var path, browser.Show(files))) return R.Ok;

        if (!Try(out var diffs, out e, await server.GetFileDiffAsync(path, RepoPath)))
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


    public void DiffBranchesBranch(string branchName1, string branchName2) => Do(async () =>
    {
        string message = "";
        var branch1 = repo.Repo.BranchByName[branchName1];
        var branch2 = repo.Repo.BranchByName[branchName2];

        var sha1 = branch1.TipId;
        var sha2 = branch2.TipId;
        if (sha1 == Repo.UncommittedId || sha2 == Repo.UncommittedId) return R.Error("Cannot diff while uncommitted changes");

        message = $"Diff '{branch1.NiceNameUnique}' to '{branch2.NiceNameUnique}'";

        if (!Try(out var diff, out var e, await server.GetPreviewMergeDiffAsync(sha2, sha1, message, RepoPath)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, sha1, RepoPath);
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

        if (!Try(out var diff, out var e, await server.GetPreviewMergeDiffAsync(sha1, sha2, message, RepoPath)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, sha1, RepoPath);
        return R.Ok;
    });


    public void CherryPick(string id) => Do(async () =>
    {
        if (!Try(out var e, await server.CherryPickAsync(id, RepoPath)))
        {
            return R.Error($"Failed to cherry pick {id.Sid()}", e);
        }

        RefreshAndCommit();
        return R.Ok;
    });


    public void PushCurrentBranch() => Do(async () =>
    {
        var branch = serverRepo.ViewBranches.FirstOrDefault(b => b.IsCurrent);

        if (!serverRepo.Status.IsOk) return R.Error("Commit changes before pushing");
        if (branch == null) return R.Error("No current branch to push");
        if (!branch.HasLocalOnly) return R.Error($"No local changes to push on current branch:\n{branch.NiceNameUnique}");

        if (branch.RemoteName != "")
        {   // Cannot push local branch if remote needs to be pulled first
            var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
            if (remoteBranch != null && remoteBranch.HasRemoteOnly)
                return R.Error("Pull current remote branch first before pushing:\n" +
                    $"{remoteBranch.NiceNameUnique}");
        }

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, RepoPath)))
        {
            return R.Error($"Failed to push branch:\n{branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PublishCurrentBranch() => Do(async () =>
    {
        var branch = serverRepo.ViewBranches.First(b => b.IsCurrent);

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, RepoPath)))
        {
            return R.Error($"Failed to publish branch:\n{branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PushBranchAsync(name, RepoPath)))
        {
            return R.Error($"Failed to push branch:\n{name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public bool CanPush() => serverRepo.Status.IsOk &&
         repo.Repo.ViewBranches.Any(b => b.HasLocalOnly && !b.HasRemoteOnly);

    public bool CanPushCurrentBranch()
    {
        var branch = serverRepo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
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
        var branch = serverRepo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (!serverRepo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (branch == null) return R.Error("No current branch to pull");
        if (branch.RemoteName == "") return R.Error("No current remote branch to pull");

        var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
        if (remoteBranch == null || !remoteBranch.HasRemoteOnly) return R.Error(
            "No remote changes on current branch to pull");

        if (!Try(out var e, await server.PullCurrentBranchAsync(RepoPath)))
        {
            return R.Error($"Failed to pull current branch", e);
        }

        Refresh();
        return R.Ok;
    });

    public void PullBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PullBranchAsync(name, RepoPath)))
        {
            return R.Error($"Failed to pull branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanPull() => Status.IsOk && repo.Repo.ViewBranches.Any(b => b.HasRemoteOnly);

    public bool CanPullCurrentBranch()
    {
        var branch = serverRepo.ViewBranches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null) return false;

        if (branch.RemoteName == "") return false;  // No remote branch to pull

        var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
        return Status.IsOk && remoteBranch != null && remoteBranch.HasRemoteOnly;
    }

    public void PushAllBranches() => Do(async () =>
    {
        if (!serverRepo.Status.IsOk) return R.Error("Commit changes before pulling");
        if (!CanPush()) return R.Error("No local changes to push");

        var branches = repo.Repo.ViewBranches.Where(b => b.HasLocalOnly && !b.HasRemoteOnly)
            .DistinctBy(b => b.PrimaryName);

        foreach (var b in branches)
        {
            if (!Try(out var e, await server.PushBranchAsync(b.Name, RepoPath)))
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
            if (!Try(out var e, await server.PullCurrentBranchAsync(RepoPath)))
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
            if (!Try(out var e, await server.PullBranchAsync(b.Name, RepoPath)))
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

            if (!Try(out var e, await server.CreateBranchAsync(serverRepo, rsp.Name, rsp.IsCheckout, RepoPath)))
            {
                return R.Error($"Failed to create branch {rsp.Name}", e);
            }
            branchName = rsp.Name;

            if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(branchName, RepoPath)))
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

            if (!Try(out var e, await server.CreateBranchFromBranchAsync(serverRepo, rsp.Name, name, rsp.IsCheckout, RepoPath)))
            {
                return R.Error($"Failed to create branch {rsp.Name}", e);
            }
            branchName = rsp.Name;

            if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(branchName, RepoPath)))
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
                await server.CreateBranchFromCommitAsync(serverRepo, rsp.Name, commit.Id, rsp.IsCheckout, RepoPath)))
            {
                return R.Error($"Failed to create branch {rsp.Name}", e);
            }
            branchName = rsp.Name;

            if (rsp.IsPush && !Try(out e, await server.PushBranchAsync(rsp.Name, RepoPath)))
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


    public void Stash() => Do(async () =>
    {
        if (repo.Repo.Status.IsOk) return R.Ok;

        if (!Try(out var e, await server.StashAsync(RepoPath)))
        {
            return R.Error($"Failed to stash changes", e);
        }

        Refresh();
        return R.Ok;
    });

    public void StashPop(string name) => Do(async () =>
    {
        if (!repo.Repo.Status.IsOk) return R.Ok;

        if (!Try(out var e, await server.StashPopAsync(name, RepoPath)))
        {
            return R.Error($"Failed to pop stash {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void StashDiff(string name) => Do(async () =>
    {
        if (!Try(out var diff, out var e, await server.GetStashDiffAsync(name, RepoPath)))
        {
            return R.Error($"Failed to diff stash {name}", e);
        }

        diffView.Show(diff, name, RepoPath);
        return R.Ok;
    });

    public void StashDrop(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.StashDropAsync(name, RepoPath)))
        {
            return R.Error($"Failed to drop stash {name}", e);
        }

        Refresh();
        return R.Ok;
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

            if (!Try(out var e, await server.DeleteRemoteBranchAsync(remoteBranch.Name, RepoPath)))
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
            if (!Try(out var e, await server.DeleteLocalBranchAsync(localBranch.Name, rsp.IsForce, RepoPath)))
            {
                return R.Error($"Failed to delete local branch {localBranch.Name}", e);
            }
            newName = localBranch.PrimaryBaseName;
        }

        Refresh(newName);
        return R.Ok;
    });


    public void UpdateRelease() => Do(async () =>
    {
        await Task.Yield();

        var releases = config.Releases;
        var typeText = releases.IsPreview ? "(preview)" : "";
        string msg = $"A new release is available.\n\n" +
            $"Current Version: {Build.Version()}\n" +
            $"Built:           {Build.Time().Iso()}\n\n" +
            $"New Version:     {releases.LatestVersion} {typeText}\n" +
            $"Built:           {Build.GetBuildTime(releases.LatestVersion).Iso()}\n\n" +
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


    public void ChangeBranchColor(string brandName)
    {
        var b = repo.Repo.BranchByName[brandName];
        if (b.IsMainBranch) return;

        branchColorService.ChangeColor(repo.Repo, b);
        Refresh();
    }


    void SetRepo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);
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

    async Task<bool> CheckBinaryOrLargeAddedFilesAsync()
    {
        var addFiles = serverRepo.Status.AddedFiles.ToList();
        var addAndModified = addFiles.Concat(serverRepo.Status.ModifiedFiles)
            .Concat(serverRepo.Status.RenamedTargetFiles).ToList();

        var binaryFiles = addAndModified.Where(f => !Files.IsText(Path.Join(RepoPath, f))).ToList();

        if (binaryFiles.Any())
        {
            var msg = $"There are {binaryFiles.Count} binary modified files:\n" +
            $"  {string.Join("\n  ", binaryFiles)}" +
            "\n\nDo you want to commit them as they are\nor first undo/revert them and then commit?";
            var rsp = UI.InfoMessage("Binary Files Detected !", msg, 1, new[] { "Commit", "Undo", "Cancel" });
            if (rsp == 2 || rsp == -1) return false; // Cancel

            if (rsp == 1)
            {
                await UndoUncommittedFilesAsync(binaryFiles);
                UI.Post(() =>
                {
                    RefreshAndCommit();
                });
                return false;
            }
        }

        var largeFiles = addFiles
            .Where(f => !binaryFiles.Contains(f))
            .Where(f => Files.IsLarger(Path.Join(RepoPath, f), 100 * 1000)).ToList();

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
        var branch = repo.Repo.BranchByName[commit.BranchName];
        var isPushable = branch.IsRemote || branch.RemoteName != "";

        if (commit.IsUncommitted) return R.Ok;

        if (!Try(out var tag, addTagDlg.Show())) return R.Ok;

        if (!Try(out var e, await server.AddTagAsync(tag, commit.Id, isPushable, RepoPath)))
        {
            return R.Error($"Failed to add tag {tag}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void DeleteTag(string name) => Do(async () =>
    {
        var commit = repo.RowCommit;
        var branch = repo.Repo.BranchByName[commit.BranchName];
        var isPushable = branch.IsRemote || branch.RemoteName != "";

        if (!Try(out var e, await server.RemoveTagAsync(name, isPushable, RepoPath)))
        {
            return R.Error($"Failed to delete tag {name}", e);
        }

        RefreshAndFetch();
        return R.Ok;
    });

    public void SetBranchManuallyAsync() => Do(async () =>
    {
        var commit = repo.RowCommit;
        if (commit.IsUncommitted) return R.Error($"Not a valid commit");


        var branch = repo.Repo.BranchByName[commit.BranchName];

        var possibleBranches = server.GetPossibleBranchNames(serverRepo, commit.Id, 20);

        if (!Try(out var name, setBranchDlg.Show(commit.Sid, commit.IsBranchSetByUser, branch.NiceName, possibleBranches))) return R.Ok;

        if (name != "")
        {
            if (!Try(out var e, await server.SetBranchManuallyAsync(serverRepo, commit.Id, name ?? "")))
            {
                return R.Error($"Failed to set branch name manually", e);
            }
        }
        else if (commit.IsBranchSetByUser)
        {   // name is empty, lets unset name (if set)
            if (!Try(out var ee, await server.UnresolveAmbiguityAsync(serverRepo, commit.Id)))
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

        repoConfig.Set(RepoPath, s =>
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
        if (!Try(out var e, await server.SwitchToCommitAsync(commit.Id, RepoPath)))
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

    private async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = repo.RowCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, repo.Repo.Path);
    }
}
