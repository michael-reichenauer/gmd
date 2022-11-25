using gmd.Common;
using gmd.Git;
using gmd.Installation;
using gmd.Server;
using Terminal.Gui;


namespace gmd.Cui.Common;


interface IRepo
{
    IRepoCommands Cmd { get; }
    Server.Repo Repo { get; }
    Graph Graph { get; }
    int TotalRows { get; }
    int CurrentIndex { get; }
    Server.Commit CurrentIndexCommit { get; }
    Server.Branch? CurrentBranch { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    bool HasUncommittedChanges { get; }

    void ToggleDetails();
    void ShowAbout();
    void Refresh(string addBranchName = "", string commitId = "");
    void ShowRepo(string path);
    void ShowBrowseDialog();
    void Filter();

    // void ShowBranch(string name, bool includeAmbiguous);
    // void HideBranch(string name);
    IReadOnlyList<Server.Branch> GetAllBranches();
    IReadOnlyList<Server.Branch> GetShownBranches();
    Server.Branch GetCurrentBranch();
    IReadOnlyList<Server.Branch> GetCommitBranches();
    Task<R<IReadOnlyList<string>>> GetFilesAsync();
    IReadOnlyList<string> GetUncommittedFiles();

    Server.Branch ViewedBranchByName(string name);
    Server.Branch AllBranchByName(string name);

    void SwitchTo(string branchName);
    void Commit();
    void CommitFromMenu();
    void PushCurrentBranch();
    void PushBranch(string name);
    bool CanPush();
    bool CanPushCurrentBranch();
    void PullCurrentBranch();
    void PullBranch(string name);
    bool CanPull();
    bool CanPullCurrentBranch();
    void ShowUncommittedDiff();
    void ShowCurrentRowDiff();
    void ShowFileHistory();

    void MergeBranch(string name);
    void CreateBranch();
    void CreateBranchFromCommit();
    void DeleteBranch(string name);
    void UpdateRelease();
    bool CanUndoUncommitted();
    void UndoUncommittedFile(string path);
    void UndoAllUncommittedChanged();
    void CleanWorkingFolder();
    void UndoCommit(string id);
    bool CanUndoCommit();
    void UncommitLastCommit();
    bool CanUncommitLastCommit();
    void ResolveAmbiguity(Server.Branch branch, string parentName);
    void UnresolveAmbiguity(string commitId);
}

class RepoImpl : IRepo
{
    readonly IRepoView repoView;
    readonly IGraphService graphService;
    readonly Server.IServer server;
    readonly ICommitDlg commitDlg;
    readonly IDiffView diffView;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IProgress progress;
    readonly IFilterDlg filterDlg;
    readonly IStates states;
    readonly IGit git;
    readonly IUpdater updater;
    readonly IRepoCommands repoCommands;

    internal RepoImpl(
        Func<IRepo, Server.Repo, IRepoView, IRepoCommands> newRepoCommands,
        IRepoView repoView,
        Server.Repo serverRepo,
        IGraphService graphService,
        Server.IServer server,
        ICommitDlg commitDlg,
        IDiffView diffView,
        ICreateBranchDlg createBranchDlg,
        IProgress progress,
        IFilterDlg filterDlg,
        IStates states,
        IGit git,
        IUpdater updater)
    {
        this.repoView = repoView;
        Repo = serverRepo;
        repoCommands = newRepoCommands(this, serverRepo, repoView);
        this.graphService = graphService;
        this.server = server;
        this.commitDlg = commitDlg;
        this.diffView = diffView;
        this.createBranchDlg = createBranchDlg;
        this.progress = progress;
        this.filterDlg = filterDlg;
        this.states = states;
        this.git = git;
        this.updater = updater;
        Graph = graphService.CreateGraph(serverRepo);
    }

    public IRepoCommands Cmd => repoCommands;

    public Server.Repo Repo { get; init; }
    public Server.Commit CurrentIndexCommit => Repo.Commits[CurrentIndex];
    public bool HasUncommittedChanges => !Repo.Status.IsOk;
    public Server.Branch? CurrentBranch => Repo.Branches.FirstOrDefault(b => b.IsCurrent);
    public Server.Branch ViewedBranchByName(string name) => Repo.BranchByName[name];
    public Server.Branch AllBranchByName(string name) => server.AllBanchByName(Repo, name);


    public Graph Graph { get; init; }

    public int TotalRows => Repo.Commits.Count;
    public int CurrentIndex => repoView.CurrentIndex;
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;


    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);
    public void UpdateRepoTo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);
    public void ToggleDetails() => repoView.ToggleDetails();

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
        var recentFolders = states.Get().RecentParentFolders;

        var browser = new FolderBrowseDlg();
        if (!Try(out var path, browser.Show(recentFolders))) return R.Ok;

        if (!Try(out var e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });

    public async Task<R<IReadOnlyList<string>>> GetFilesAsync()
    {
        var commit = CurrentIndexCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;
        return await server.GetFileAsync(reference, Repo.Path);
    }


    public void UpdateRelease() => Do(async () =>
    {
        await Task.Yield();

        var releases = states.Get().Releases;
        var typeText = releases.IsPreview ? "(preview)" : "(stable)";
        string msg = $"A new release is available:\n" +
            $"New Version:     {releases.LatestVersion} {typeText}\n" +
            $"Current Version: {Build.Version()}\n\n" +
            "Do you want to update?";
        var button = UI.InfoMessage("New Release", msg, new[] { "Yes", "No" });
        if (button != 0)
        {
            Log.Info($"Skip udate");
            return R.Ok;
        }
        Log.Info($"Updating release ...");
        if (!Try(out var e, await updater.UpdateAsync())) return e;

        UI.InfoMessage("Restart Requiered", "A program restart is required,\nplease start gmd again.");
        UI.Shutdown();

        return R.Ok;
    });


    public Server.Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);

    public IReadOnlyList<Server.Branch> GetAllBranches() => server.GetAllBranches(Repo);
    public IReadOnlyList<Server.Branch> GetShownBranches() => Repo.Branches;

    public IReadOnlyList<Server.Branch> GetCommitBranches() =>
        server.GetCommitBranches(Repo, CurrentIndexCommit.Id);


    public void SwitchTo(string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.SwitchToAsync(branchName, Repo.Path)))
        {
            return R.Error($"Failed to switch to {branchName}", e);
        }
        Refresh();
        return R.Ok;
    });


    public void Filter() => Do(async () =>
     {
         if (!Try(out var commit, out var e, filterDlg.Show(this))) return R.Ok;
         await Task.Delay(0);

         Refresh(commit.BranchName, commit.Id);
         return R.Ok;
     });


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
        if (!HasUncommittedChanges) return R.Ok;
        if (!CheckBinaryOrLargeAddedFiles()) return R.Ok;

        if (!commitDlg.Show(this, out var message)) return R.Ok;

        if (!Try(out var e, await server.CommitAllChangesAsync(message, Repo.Path)))
        {
            return R.Error($"Failed to commit", e);
        }

        Refresh();
        return R.Ok;
    });



    public void ShowUncommittedDiff() => ShowDiff(Server.Repo.UncommittedId);

    public void ShowCurrentRowDiff() => ShowDiff(CurrentIndexCommit.Id);

    public void ShowDiff(string commitId) => Do(async () =>
    {
        if (!Try(out var diff, out var e, await server.GetCommitDiffAsync(commitId, Repo.Path)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, commitId);
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

        if (!Try(out var diffs, out e, await server.GetFileDiffAsync(path, Repo.Path)))
        {
            return R.Error($"Failed to show file history", e);
        }

        diffView.Show(diffs);
        return R.Ok;
    });


    public void MergeBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.MergeBranch(name, Repo.Path)))
        {
            return R.Error($"Failed to merge branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushCurrentBranch() => Do(async () =>
    {
        if (!CanPushCurrentBranch()) return R.Ok;

        var branch = Repo.Branches.First(b => b.IsCurrent);

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PushBranchAsync(name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public bool CanPush() => Repo.Status.IsOk && GetShownBranches().Any(b => b.HasLocalOnly && !b.HasRemoteOnly);

    public bool CanPushCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return Repo.Status.IsOk &&
            branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }


    public void PullCurrentBranch() => Do(async () =>
    {
        if (!CanPullCurrentBranch()) return R.Ok;

        if (!Try(out var e, await server.PullCurrentBranchAsync(Repo.Path)))
        {
            return R.Error($"Failed to pull current branch", e);
        }

        Refresh();
        return R.Ok;
    });

    public void PullBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PullBranchAsync(name, Repo.Path)))
        {
            return R.Error($"Failed to pull branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanPull() => Repo.Status.IsOk && GetShownBranches().Any(b => b.HasRemoteOnly);

    public bool CanPullCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        if (branch == null)
        {
            return false;
        }
        if (branch.RemoteName != "")
        {
            var remoteBranch = Repo.BranchByName[branch.RemoteName];
            return Repo.Status.IsOk && branch != null && branch.HasRemoteOnly;
        }
        return false;

    }


    public void CreateBranch() => Do(async () =>
     {
         var currentBranchName = GetCurrentBranch().Name;
         if (!Try(out var name, createBranchDlg.Show(currentBranchName, ""))) return R.Ok;

         if (!Try(out var e, await server.CreateBranchAsync(Repo, name, true, Repo.Path)))
         {
             return R.Error($"Failed to create branch {name}", e);
         }

         if (!Try(out e, await server.PushBranchAsync(name, Repo.Path)))
         {
             return R.Error($"Failed to push branch {name} to remote server", e);
         }

         Refresh(name);
         return R.Ok;
     });


    public void CreateBranchFromCommit() => Do(async () =>
    {
        var commit = CurrentIndexCommit;
        var branchName = commit.BranchName;

        if (!Try(out var name, createBranchDlg.Show(branchName, commit.Sid))) return R.Ok;

        if (!Try(out var e,
            await server.CreateBranchFromCommitAsync(Repo, name, commit.Id, true, Repo.Path)))
        {
            return R.Error($"Failed to create branch {name}", e);
        }

        if (!Try(out e, await server.PushBranchAsync(name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {name} to remote server", e);
        }

        Refresh(name);
        return R.Ok;
    });

    public void ShowAbout() => Do(async () =>
    {
        var gmdVersion = Build.Version();
        var gmdBuildTime = Build.Time().ToUniversalTime().Iso();
        var gmdSha = Build.Sha();
        if (!Try(out var gitVersion, out var e, await git.Version())) return e;

        var msg =
            $"Version: {gmdVersion}\n" +
            $"Built:   {gmdBuildTime}Z\n" +
            $"Git:     {gitVersion}\n" +
            $"Sha:     {gmdSha}\n";

        UI.InfoMessage("About", msg);
        return R.Ok;
    });


    public void DeleteBranch(string name) => Do(async () =>
    {
        var allBranches = GetAllBranches();
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
            {   // with a coresponding local branch
                localBranch = allBranches.First(b => b.Name == branch.LocalName);
            }
        }

        if (localBranch != null)
        {
            if (!Try(out var e,
                await server.DeleteLocalBranchAsync(branch.Name, false, Repo.Path)))
            {
                return R.Error($"Failed to delete branch {branch.Name}", e);
            }
        }

        if (remoteBranch != null)
        {
            if (!Try(out var e,
                await server.DeleteRemoteBranchAsync(remoteBranch.Name, Repo.Path)))
            {
                return R.Error($"Failed to delete remote branch {branch.Name}", e);
            }
        }

        Refresh();
        return R.Ok;
    });


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
        var addFiles = Repo.Status.AddedFiles.ToList();

        var binaryFiles = addFiles.Where(f => !Files.IsText(Path.Join(Repo.Path, f))).ToList();
        var largeFiles = addFiles
            .Where(f => !binaryFiles.Contains(f))
            .Where(f => Files.IsLarger(Path.Join(Repo.Path, f), 100 * 1000)).ToList();
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
            if (0 != UI.InfoMessage("Binay or Large Files Detected !", msg, 1, new[] { "Yes", "No" }))
            {
                return false;
            }
        }

        return true;
    }

    public bool CanUndoUncommitted() => !Repo.Status.IsOk;

    public IReadOnlyList<string> GetUncommittedFiles() =>
        Repo.Status.ModifiedFiles
        .Concat(Repo.Status.AddedFiles)
        .Concat(Repo.Status.DeleteddFiles)
        .Concat(Repo.Status.ConflictsFiles)
        .OrderBy(f => f)
        .ToList();

    public void UndoUncommittedFile(string path) => Do(async () =>
    {
        if (!Try(out var e, await server.UndoUncommittedFileAsync(path, Repo.Path)))
        {
            return R.Error($"Failed to undo {path}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UndoAllUncommittedChanged() => Do(async () =>
    {
        if (!Try(out var e, await server.UndoAllUncommittedChangesAsync(Repo.Path)))
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

        if (!Try(out var e, await server.CleanWorkingFolderAsync(Repo.Path)))
        {
            return R.Error($"Failed to clean workin folder", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UndoCommit(string id) => Do(async () =>
    {
        if (!CanUndoCommit()) return R.Ok;

        if (!Try(out var e, await server.UndoCommitAsync(id, Repo.Path)))
        {
            return R.Error($"Failed to undo commit", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanUndoCommit() => Repo.Status.IsOk;


    public void UncommitLastCommit() => Do(async () =>
    {
        if (!CanUncommitLastCommit()) return R.Ok;

        if (!Try(out var e, await server.UncommitLastCommitAsync(Repo.Path)))
        {
            return R.Error($"Failed to undo commit", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanUncommitLastCommit()
    {
        if (!Repo.Commits.Any()) return false;

        var c = Repo.Commits[0];
        var b = Repo.BranchByName[Repo.Commits[0].BranchName];
        return Repo.Status.IsOk && c.IsAhead || (!b.IsRemote && b.RemoteName == "");
    }

    public void ResolveAmbiguity(Server.Branch branch, string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.ResolveAmbiguityAsync(Repo, branch.Name, branchName)))
        {
            return R.Error($"Failed to resolve ambiguity", e);
        }

        Refresh();
        return R.Ok;
    });

    public void UnresolveAmbiguity(string commitId) => Do(async () =>
    {
        if (!Try(out var e, await server.UnresolveAmbiguityAsync(Repo, commitId)))
        {
            return R.Error($"Failed to unresolve ambiguity", e);
        }

        Refresh();
        return R.Ok;
    });
}

