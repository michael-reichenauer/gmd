using gmd.Common;
using gmd.Git;
using gmd.Installation;
using gmd.Server;

namespace gmd.Cui.Common;

interface IRepoCommands
{
    void ShowBranch(string name, bool includeAmbiguous);
    void HideBranch(string name);
    void SwitchTo(string branchName);
    void ToggleDetails();
    void ShowAbout();
    void ShowHelp();
    void ShowBrowseDialog();
    void Filter();
    void ShowRepo(string path);
    void Refresh();
    void ShowUncommittedDiff();
    void Commit();
    void CreateBranch();
    void ShowCurrentRowDiff();
    void PushCurrentBranch();
    void PullCurrentBranch();
    void UpdateRelease();
    void CommitFromMenu();
    bool CanPush();
    bool CanPull();
    void CreateBranchFromCommit();
    void ShowFileHistory();
    void UnresolveAmbiguity(string commitId);
    void ResolveAmbiguity(Server.Branch branch, string displayName);
    bool CanPushCurrentBranch();
    void PushBranch(string name);
    void PushAllBranches();
    bool CanPullCurrentBranch();
    void PullBranch(string name);
    void PullAllBranches();
    void DeleteBranch(string name);
    void MergeBranch(string name);
    void PreviewMergeBranch(string name, bool isFromCurrentCommit);
    bool CanUndoUncommitted();
    bool CanUndoCommit();
    void UndoCommit(string id);
    void UncommitLastCommit();
    bool CanUncommitLastCommit();
    void UndoAllUncommittedChanged();
    void CleanWorkingFolder();
    void UndoUncommittedFile(string path);
    void Clone();
    void CherryPic(string id);
    void ChangeBranchColor();
}

class RepoCommands : IRepoCommands
{
    readonly IServer server;
    readonly IProgress progress;
    readonly IFilterDlg filterDlg;
    readonly ICommitDlg commitDlg;
    readonly ICloneDlg cloneDlg;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IAboutDlg aboutDlg;
    readonly IHelpDlg helpDlg;
    readonly IDiffView diffView;
    readonly IState states;
    readonly IUpdater updater;
    readonly IGit git;
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
        IAboutDlg aboutDlg,
        IHelpDlg helpDlg,
        IDiffView diffView,
        IState states,
        IUpdater updater,
        IGit git,
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
        this.aboutDlg = aboutDlg;
        this.helpDlg = helpDlg;
        this.diffView = diffView;
        this.states = states;
        this.updater = updater;
        this.git = git;
        this.branchColorService = branchColorService;
    }

    public void Refresh() => repoView.Refresh();


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

    public void CommitFromMenu()
    {
        // For some unknown reason, calling commit directly from the menu will
        // Show the commit dialog, but diff will not work since async/await does not seem to work
        // However wrapping with a timeout seems to work as desired.
        UI.AddTimeout(TimeSpan.FromMilliseconds(100), (_) =>
        {
            Commit();
            return false;
        });
    }


    public void Commit() => Do(async () =>
    {
        if (repo.Status.IsOk) return R.Ok;
        if (!CheckBinaryOrLargeAddedFiles()) return R.Ok;

        if (!commitDlg.Show(repo, out var message)) return R.Ok;

        if (!Try(out var e, await server.CommitAllChangesAsync(message, repoPath)))
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

        if (!Try(out var e, await server.UndoCommitAsync(id, repoPath)))
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

    public void UnresolveAmbiguity(string commitId) => Do(async () =>
    {
        if (!Try(out var e, await server.UnresolveAmbiguityAsync(serverRepo, commitId)))
        {
            return R.Error($"Failed to unresolve ambiguity", e);
        }

        Refresh();
        return R.Ok;
    });




    public void ShowUncommittedDiff() => ShowDiff(Server.Repo.UncommittedId);

    public void ShowCurrentRowDiff() => ShowDiff(repo.RowCommit.Id);

    public void ShowDiff(string commitId) => Do(async () =>
    {
        if (!Try(out var diff, out var e, await server.GetCommitDiffAsync(commitId, repoPath)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, commitId);
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
        if (!Try(out var e, await server.MergeBranchAsync(serverRepo, branchName)))
        {
            return R.Error($"Failed to merge branch {branchName}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void PreviewMergeBranch(string branchName, bool isFromCurrentCommit) => Do(async () =>
    {
        if (repo.CurrentBranch == null) return R.Ok;
        var sha1 = repo.Branch(branchName).TipId;
        var sha2 = isFromCurrentCommit ? repo.RowCommit.Sid : repo.CurrentBranch.TipId;

        if (!Try(out var diff, out var e, await server.GetPreviewMergeDiffAsync(sha1, sha2, repoPath)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, sha1);
        return R.Ok;
    });

    public void CherryPic(string id) => Do(async () =>
    {
        if (!Try(out var e, await server.CherryPickAsync(id, repoPath)))
        {
            return R.Error($"Failed to cherry pic {id.Substring(0, 6)}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushCurrentBranch() => Do(async () =>
    {
        if (!CanPushCurrentBranch()) return R.Ok;

        var branch = serverRepo.Branches.First(b => b.IsCurrent);

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, repoPath)))
        {
            return R.Error($"Failed to push branch {branch.Name}", e);
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
        if (branch == null)
        {
            return false;
        }
        if (branch.RemoteName != "")
        {
            var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
            return status.IsOk && remoteBranch != null && !remoteBranch.HasRemoteOnly;
        }

        return serverRepo.Status.IsOk &&
            branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }


    public void PullCurrentBranch() => Do(async () =>
    {
        if (!CanPullCurrentBranch()) return R.Ok;

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
        if (branch == null)
        {
            return false;
        }
        if (branch.RemoteName != "")
        {
            var remoteBranch = serverRepo.BranchByName[branch.RemoteName];
            return status.IsOk && remoteBranch != null && remoteBranch.HasRemoteOnly;
        }
        return false;
    }

    public void PushAllBranches() => Do(async () =>
    {
        if (!CanPush()) return R.Ok;

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
        Log.Info("Pull all");

        if (!CanPull()) return R.Ok;

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

        if (localBranch != null)
        {
            if (!Try(out var e,
                await server.DeleteLocalBranchAsync(branch.Name, false, repoPath)))
            {
                return R.Error($"Failed to delete branch {branch.Name}", e);
            }
        }

        if (remoteBranch != null)
        {
            if (!Try(out var e,
                await server.DeleteRemoteBranchAsync(remoteBranch.Name, repoPath)))
            {
                return R.Error($"Failed to delete remote branch {branch.Name}", e);
            }
        }

        Refresh();
        return R.Ok;
    });


    public void UpdateRelease() => Do(async () =>
    {
        await Task.Yield();

        var releases = states.Get().Releases;
        var typeText = releases.IsPreview ? "(preview)" : "";
        string msg = $"A new release is available:\n" +
            $"New Version:     {releases.LatestVersion} {typeText}\n" +
            $"Current Version: {Build.Version()}\n\n" +
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
        branchColorService.ChangeColor(repo.Repo, repo.Branch(repo.RowCommit.BranchName));
        Refresh();
    }

    void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

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

        var binaryFiles = addFiles.Where(f => !Files.IsText(Path.Join(repoPath, f))).ToList();
        var largeFiles = addFiles
            .Where(f => !binaryFiles.Contains(f))
            .Where(f => Files.IsLarger(Path.Join(repoPath, f), 100 * 1000)).ToList();
        var msg = "";
        if (binaryFiles.Any())
        {
            msg += $"\nBinary files ({binaryFiles.Count}):\n  {string.Join("\n  ", binaryFiles)}";
        }
        if (largeFiles.Any())
        {
            msg += $"\nLarge files ({largeFiles.Count}):  \n{string.Join("\n  ", largeFiles)}";
        }

        if (msg != "")
        {
            msg = $"Som binary or large files seems to be included:{msg}" +
                "\n\nDo you want to continue?";
            if (0 != UI.InfoMessage("Binary or Large Files Detected !", msg, 1, new[] { "Yes", "No" }))
            {
                return false;
            }
        }

        return true;
    }
}

