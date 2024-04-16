using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;


namespace gmd.Cui.RepoView;


interface ICommitCommands
{
    void Commit(bool isAmend, IReadOnlyList<Commit>? commits = null);
    void CommitFromMenu(bool isAmend);

    void ShowUncommittedDiff(bool isFromCommit = false);
    void ShowCurrentRowDiff();
    void ShowDiff(string commitId, string commitId2, bool isFromCommit = false);
    void ShowFileHistory();

    void Stash();
    void StashPop(string name);
    void StashDiff(string name);
    void StashDrop(string name);

    void UndoCommit(string id);
    void UncommitLastCommit();
    void UncommitUntilCommit(string id);
    void UndoUncommittedFile(string path);
    void UndoUncommittedFiles(IReadOnlyList<string> paths);
    void SquashCommits(string id1, string id2);
    void CherryPick();

    void AddTag();
    void DeleteTag(string name);
    bool CanUncommitLastCommit();
    bool CanUndoUncommitted();
    void ToggleDetails();
}


class CommitCommands : ICommitCommands
{
    readonly IProgress progress;
    readonly IViewRepo repo;
    readonly IServer server;
    readonly ICommitDlg commitDlg;
    readonly ISquashDlg squashDlg;
    readonly IDiffView diffView;
    readonly IAddTagDlg addTagDlg;
    readonly IAddStashDlg addStashDlg;
    readonly IRepoView repoView;

    public CommitCommands(
        IProgress progress,
        IViewRepo repo,
        IServer server,
        ICommitDlg commitDlg,
        ISquashDlg squashDlg,
        IDiffView diffView,
        IAddTagDlg addTagDlg,
        IAddStashDlg addStashDlg,
        IRepoView repoView)
    {
        this.progress = progress;
        this.repo = repo;
        this.server = server;
        this.commitDlg = commitDlg;
        this.squashDlg = squashDlg;
        this.diffView = diffView;
        this.addTagDlg = addTagDlg;
        this.addStashDlg = addStashDlg;
        this.repoView = repoView;
    }


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

    public void Commit(bool isAmend, IReadOnlyList<Commit>? commits = null) => Do(async () =>
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

        if (!Try(out var e, await server.CommitAllChangesAsync(message, isAmend, repo.Path)))
        {
            return R.Error($"Failed to commit", e);
        }

        Refresh();
        return R.Ok;
    });



    public void ShowUncommittedDiff(bool isFromCommit = false) => ShowDiff(Repo.UncommittedId, "", isFromCommit);

    public void ShowCurrentRowDiff()
    {
        var id1 = repo.RowCommit.Id;
        var id2 = "";
        var selection = repo.RepoView.Selection;
        var (i1, i2) = (selection.I1, selection.I2);
        if (i2 - i1 > 0)
        {   // User has selected multiple commits
            id1 = repo.Repo.ViewCommits[i1].Id;
            id2 = repo.Repo.ViewCommits[i2].Id;
            if (id1 == Repo.UncommittedId || id2 == Repo.UncommittedId)
            {
                UI.ErrorMessage("Selection start and end commit cannot be uncommitted row.");
                return;
            }
            if (repo.Repo.CommitById[id1].BranchPrimaryName != repo.Repo.CommitById[id1].BranchPrimaryName)
            {
                UI.ErrorMessage("Selection start and end commit not on same branch");
                return;
            }
        }

        ShowDiff(id1, id2);
    }

    public void ShowDiff(string commitId, string commitId2, bool isFromCommit = false) => Do(async () =>
    {
        if (commitId == Repo.EmptyRepoCommitId) return R.Ok;

        CommitDiff? diff;
        if (commitId2 == "")
        {
            if (!Try(out diff, out var e, await server.GetCommitDiffAsync(commitId, repo.Path)))
            {
                return R.Error($"Failed to get diff", e);
            }
        }
        else
        {
            repo.RepoView.ClearSelection();
            var msg = $"Diff between {commitId.Sid()} and {commitId2.Sid()}";
            if (!Try(out diff, out var e, await server.GetDiffRangeAsync(commitId2, commitId, msg, repo.Path)))
            {
                return R.Error($"Failed to get diff", e);
            }
        }

        UI.Post(() =>
        {
            var rsp = diffView.Show(diff, commitId, repo.Path);
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


    public void CherryPick() => Do(async () =>
    {
        var sha = repo.RowCommit.Id;
        var selection = repo.RepoView.Selection;
        var (i1, i2) = (selection.I1, selection.I2);
        if (i2 - i1 > 0)
        {   // User selected range of commits
            var c1 = repo.Repo.ViewCommits[i1];
            var c2 = repo.Repo.ViewCommits[i2];
            var commits = new List<Commit>();
            var current = c1;
            while (current != c2)
            {
                commits.Add(current);
                current = repo.Repo.CommitById[current.ParentIds[0]];
            }
            commits.Add(current);
            commits.Reverse();

            foreach (var commit in commits)
            {
                if (!Try(out var e, await server.CherryPickAsync(commit.Id, repo.Path)))
                {
                    return R.Error($"Failed to cherry pick", e);
                }
                if (!Try(out e, await server.CommitAllChangesAsync(commit.Message, false, repo.Path)))
                {
                    return R.Error($"Failed to commit", e);
                }
            }
        }
        else
        {   // User selected one commit
            if (!Try(out var e, await server.CherryPickAsync(sha, repo.Path)))
            {
                return R.Error($"Failed to cherry pick", e);
            }
        }

        repo.RepoView.ClearSelection();
        RefreshAndCommit();
        return R.Ok;
    });


    public void Stash() => Do(async () =>
    {
        if (repo.Repo.Status.IsOk) return R.Ok;
        var commitMsg = repo.Repo.CurrentCommit().Subject;
        if (!Try(out var msg, out var e, addStashDlg.Show())) return R.Ok;
        msg = msg == "" ? commitMsg : msg;

        if (!Try(out e, await server.StashAsync(msg, repo.Path)))
        {
            return R.Error($"Failed to stash changes", e);
        }

        Refresh();
        return R.Ok;
    });

    public void StashPop(string name) => Do(async () =>
    {
        if (!repo.Repo.Status.IsOk) return R.Ok;

        if (!Try(out var e, await server.StashPopAsync(name, repo.Path)))
        {
            return R.Error($"Failed to pop stash {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public void StashDiff(string name) => Do(async () =>
    {
        if (!Try(out var diff, out var e, await server.GetStashDiffAsync(name, repo.Path)))
        {
            return R.Error($"Failed to diff stash {name}", e);
        }

        diffView.Show(diff, name, repo.Path);
        return R.Ok;
    });

    public void StashDrop(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.StashDropAsync(name, repo.Path)))
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

        if (!Try(out var e, await server.UndoCommitAsync(id, parentIndex, repo.Path)))
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

        if (!Try(out var e, await server.UncommitLastCommitAsync(repo.Path)))
        {
            return R.Error($"Failed to undo commit", e);
        }

        Refresh();
        return R.Ok;
    });


    public void UncommitUntilCommit(string id) => Do(async () =>
    {
        var commit = repo.Repo.CommitById[id];
        var parentId = repo.Repo.CommitById[commit.ParentIds[0]].Id;
        if (!Try(out var e, await server.UncommitUntilCommitAsync(parentId, repo.Path)))
        {
            return R.Error($"Failed to undo commit", e);
        }

        Refresh();
        return R.Ok;
    });

    public void SquashCommits(string id1, string id2) => Do(async () =>
    {
        var c1 = repo.Repo.CommitById[id1];
        var c2 = repo.Repo.CommitById[id2];
        if (!c2.ParentIds.Any()) return R.Error("Last commit does not have a parent");
        if (c1.BranchName != c2.BranchName) return R.Error("Commits are not on the same branch");
        var branch = repo.Repo.BranchByName[c1.BranchName];
        if (!branch.IsLocalCurrent) return R.Error("Commits not on current branch");

        var commits = new List<Commit>();
        var c = c1;
        while (c.Id != c2.ParentIds[0])
        {
            commits.Add(c);
            if (!c.ParentIds.Any()) break;
            c = repo.Repo.CommitById[c.ParentIds[0]];
        }

        Log.Info($"Commits {commits.ToJson()}");
        if (!squashDlg.Show(repo, commits, out var message)) return R.Ok;

        if (!Try(out var e, await server.SquashCommits(repo.Repo, id1, id2, message)))
        {
            return R.Error($"Failed to undo commit", e);
        }
        repo.RepoView.ClearSelection();

        RefreshAndCommit();

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
           if (!Try(out var e, await server.UndoUncommittedFileAsync(path, repo.Path)))
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
            if (!Try(out var _, await server.UndoUncommittedFileAsync(path, repo.Path)))
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

        if (!Try(out var e, await server.AddTagAsync(tag, commit.Id, isPushable, repo.Path)))
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

        if (!Try(out var e, await server.RemoveTagAsync(name, isPushable, repo.Path)))
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

        if (!Try(out var files, out var e, await server.GetFileAsync(reference, repo.Path)))
        {
            return R.Error($"Failed to get files", e);
        }

        var browser = new FileBrowseDlg();
        if (!Try(out var path, browser.Show(files))) return R.Ok;

        if (!Try(out var diffs, out e, await server.GetFileDiffAsync(path, repo.Path)))
        {
            return R.Error($"Failed to show file history", e);
        }

        diffView.Show(diffs);
        return R.Ok;
    });


    // public void SquashHeadTo(string id) => Do(async () =>
    // {
    //     // if (!Try(out var e, await server.RebaseBranchAsync(repo.Repo, branchName)))
    //     //     return R.Error($"Failed to rebase branch {branchName}", e);


    //     RefreshAndFetch();
    //     return R.Ok;
    // });



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

        var binaryFiles = addAndModified.Where(f => !Files.IsText(Path.Join(repo.Path, f))).ToList();

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
            .Where(f => Files.IsLarger(Path.Join(repo.Path, f), 100 * 1000))
            .Select(f => $"{f} ({Files.FileSize(f).FileSize()})").ToList();

        if (largeFiles.Any())
        {
            var msg = $"There are {largeFiles.Count} added large files:\n"
            + $"  {string.Join("\n  ", largeFiles)}" +
            "\n\nDo you want to continue?";
            if (0 != UI.InfoMessage("Large Files Detected !", msg, 1, new[] { "Yes", "No" }))
            {
                return false;
            }
        }

        return true;
    }
}
