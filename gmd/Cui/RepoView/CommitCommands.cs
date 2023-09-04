using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;


namespace gmd.Cui.RepoView;


interface ICommitCommands
{
    void Commit(bool isAmend, IReadOnlyList<Server.Commit>? commits = null);
    void CommitFromMenu(bool isAmend);

    void ShowUncommittedDiff(bool isFromCommit = false);
    void ShowCurrentRowDiff();
    void ShowDiff(string commitId, bool isFromCommit = false);
    void ShowFileHistory();

    void Stash();
    void StashPop(string name);
    void StashDiff(string name);
    void StashDrop(string name);

    void UndoCommit(string id);
    void UncommitLastCommit();
    void UndoUncommittedFile(string path);
    void UndoUncommittedFiles(IReadOnlyList<string> paths);

    void AddTag();
    void DeleteTag(string name);
    bool CanUncommitLastCommit();
    bool CanUndoUncommitted();
    void ToggleDetails();
}



class CommitCommands : ICommitCommands
{
    private readonly IProgress progress;
    private readonly IViewRepo repo;
    private readonly IServer server;
    private readonly ICommitDlg commitDlg;
    private readonly IDiffView diffView;
    private readonly IAddTagDlg addTagDlg;
    private readonly IRepoView repoView;

    public CommitCommands(
        IProgress progress,
        IViewRepo repo,
        IServer server,
        ICommitDlg commitDlg,
        IDiffView diffView,
        IAddTagDlg addTagDlg,
        IRepoView repoView)
    {
        this.progress = progress;
        this.repo = repo;
        this.server = server;
        this.commitDlg = commitDlg;
        this.diffView = diffView;
        this.addTagDlg = addTagDlg;
        this.repoView = repoView;
    }

    string RepoPath => repo.Repo.Path;

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Server.Commit>? commits = null) =>
         repoView.RefreshAndCommit(addName, commitId, commits);

    public void RefreshAndFetch(string addName = "", string commitId = "") => repoView.RefreshAndFetch(addName, commitId);

    public bool CanUndoUncommitted() => !repo.Repo.Status.IsOk;

    public void ToggleDetails() => repoView.ToggleDetails();

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

    public bool CanUndoCommit() => repo.Repo.Status.IsOk;


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
        if (!repo.Repo.ViewCommits.Any()) return false;

        var c = repo.Repo.ViewCommits[0];
        var b = repo.Repo.BranchByName[repo.Repo.ViewCommits[0].BranchName];
        return repo.Repo.Status.IsOk && c.IsAhead || (!b.IsRemote && b.RemoteName == "");
    }

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


    public void ShowFileHistory() => Do(async () =>
    {
        var commit = repo.RowCommit;
        var reference = commit.IsUncommitted ? commit.BranchName : commit.Id;

        if (!Try(out var files, out var e, await server.GetFileAsync(reference, RepoPath)))
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
        var addFiles = repo.Repo.Status.AddedFiles.ToList();
        var addAndModified = addFiles.Concat(repo.Repo.Status.ModifiedFiles)
            .Concat(repo.Repo.Status.RenamedTargetFiles).ToList();

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
}
