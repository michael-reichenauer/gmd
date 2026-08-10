using gmd.Cui.Blame;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;

namespace gmd.Cui.RepoView;

// What CommitAsync did, for the commands that have more to do afterwards. Note that this is an
// enum and not a bool: R<bool> would be a trap, since R<T> converts implicitly both to and from
// its value, and for T = bool 'bool b = result' silently yields IsOk rather than the value.
enum CommitResult
{
    Committed,
    NothingToCommit, // The working folder was already clean, so nothing was staged to commit
    Cancelled, // The user backed out, so whatever was staged is still staged
}

interface ICommitCommands
{
    void Commit(bool isAmend, IReadOnlyList<Commit>? commits = null);
    Task<R<CommitResult>> CommitAsync(bool isAmend, IReadOnlyList<Commit>? commits = null);
    void CommitFromMenu(bool isAmend);

    void ShowUncommittedDiff(bool isFromCommit = false);
    void ShowCurrentRowDiff();
    void ShowDiff(string commitId, string commitId2, bool isFromCommit = false);
    void ShowFileHistory();
    void BlameFile();

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
    readonly IBlameView blameView;
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
        IBlameView blameView,
        IAddTagDlg addTagDlg,
        IAddStashDlg addStashDlg,
        IRepoView repoView
    )
    {
        this.progress = progress;
        this.repo = repo;
        this.server = server;
        this.commitDlg = commitDlg;
        this.squashDlg = squashDlg;
        this.diffView = diffView;
        this.blameView = blameView;
        this.addTagDlg = addTagDlg;
        this.addStashDlg = addStashDlg;
        this.repoView = repoView;
    }

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    public void RefreshAndCommit(
        string addName = "",
        string commitId = "",
        IReadOnlyList<Server.Commit>? commits = null
    ) => repoView.RefreshAndCommit(addName, commitId, commits);

    public void RefreshAndFetch(string addName = "", string commitId = "") =>
        repoView.RefreshAndFetch(addName, commitId);

    public bool CanUndoUncommitted() => !repo.Repo.Status.IsOk;

    public void ToggleDetails() => repoView.ToggleDetails();

    public void CommitFromMenu(bool isAmend)
    {
        // For some unknown reason, calling commit directly from the menu will
        // Show the commit dialog, but diff will not work since async/await does not seem to work
        // However wrapping with a timeout seems to work as desired.
        UI.AddTimeout(
            TimeSpan.FromMilliseconds(100),
            (_) =>
            {
                Commit(isAmend);
                return false;
            }
        );
    }

    public void Commit(bool isAmend, IReadOnlyList<Commit>? commits = null) =>
        Do(async () =>
        {
            if (!Try(out var result, out var e, await CommitAsync(isAmend, commits)))
                return e;

            if (result == CommitResult.Committed)
                Refresh();
            return R.Ok;
        });

    // The commit itself, without the refresh, so a command that has more to do after the commit
    // can await it and act on what the user did. MergeToBranch needs the difference: it can only
    // switch back off the target branch if the merge it staged there was actually committed.
    public async Task<R<CommitResult>> CommitAsync(bool isAmend, IReadOnlyList<Commit>? commits = null)
    {
        // Before the detached head check below, which a rebase would otherwise answer with
        // "create/switch to a branch first" — a rebase does leave HEAD detached, so that message is
        // both true and useless: the way out is to continue the rebase, not to make a branch.
        if (!OfferContinueInsteadOfCommit())
            return CommitResult.Cancelled;

        if (!isAmend && repo.Repo.Status.IsOk)
            return CommitResult.NothingToCommit;
        if (isAmend && !repo.Repo.CurrentCommit().IsAhead)
            return CommitResult.NothingToCommit;

        if (repo.Repo.CurrentBranch().IsDetached == true)
        {
            UI.ErrorMessage("Cannot commit in detached head state.\nPlease create/switch to a branch first.");
            return CommitResult.Cancelled;
        }

        // Ahead of the dialog, so the user is not asked for a message and then refused
        if (!await repo.Cmds.ConfirmConflictsResolvedAsync("Commit"))
            return CommitResult.Cancelled;

        if (!await CheckBinaryOrLargeAddedFilesAsync())
            return CommitResult.Cancelled;

        if (!commitDlg.Show(repo, isAmend, commits, out var message))
            return CommitResult.Cancelled;

        if (!Try(out var e, await server.CommitAllChangesAsync(message, isAmend, repo.Path)))
        {
            return R.Error($"Failed to commit", e);
        }

        return CommitResult.Committed;
    }

    // A rebase, an 'am', and a cherry pick or revert started outside gmd are finished by continuing
    // them, not by committing: 'git commit' makes the commit git stopped on but leaves the
    // operation mid flight, with its remaining commits never applied. So point at the command that
    // does finish it. Returns false when it has answered for the commit.
    //
    // A merge is not one of these — committing is exactly how a merge is finished — and neither is
    // gmd's own cherry pick, which runs '--no-commit' and so leaves no sequence to continue.
    bool OfferContinueInsteadOfCommit()
    {
        var operation = repo.Repo.Status.Operation;
        if (operation is not (GitOperation.Rebase or GitOperation.Am or GitOperation.CherryPick or GitOperation.Revert))
            return true;

        var name = repo.Cmds.OperationName();
        var choice = UI.InfoMessage(
            $"{name} In Progress",
            $"A {name.ToLower()} is in progress.\n\n"
                + $"It is finished with Continue {name}, not by committing:\n"
                + "committing here would leave it part way through.",
            0,
            [$"Continue {name}", "Cancel"]
        );
        if (choice == 0)
            repo.Cmds.ContinueOperation();

        return false;
    }

    public void ShowUncommittedDiff(bool isFromCommit = false) => ShowDiff(Repo.UncommittedId, "", isFromCommit);

    public void ShowCurrentRowDiff()
    {
        var id1 = repo.RowCommit.Id;
        var id2 = "";
        var selection = repo.RepoView.Selection;
        var (i1, i2) = (selection.I1, selection.I2);
        if (i2 - i1 > 0)
        { // User has selected multiple commits
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

    public void ShowDiff(string commitId, string commitId2, bool isFromCommit = false) =>
        Do(async () =>
        {
            if (commitId == Repo.EmptyRepoCommitId)
                return R.Ok;

            // How the diff view gets this same diff again, at whatever context it is asked for
            DiffReload reload;
            if (commitId2 == "")
            {
                reload = DiffReloads.Single(n => server.GetCommitDiffAsync(commitId, n, repo.Path));
            }
            else
            {
                repo.RepoView.ClearSelection();
                var msg = $"Diff between {commitId.Sid()} and {commitId2.Sid()}";
                reload = DiffReloads.Single(n => server.GetDiffRangeAsync(commitId2, commitId, msg, n, repo.Path));
            }

            if (!Try(out var diffs, out var e, await reload(DiffContext.Default)))
            {
                return R.Error($"Failed to get diff", e);
            }

            // Only the uncommitted diff can have conflicts, and reading them here keeps the await
            // off the main loop — see the note on IDiffView.Show
            var conflicts = ConflictState.None;
            if (commitId == Repo.UncommittedId && !repo.Repo.Status.IsOk)
            {
                if (Try(out var state, out var _, await server.GetConflictStateAsync(repo.Path)))
                    conflicts = state;
            }

            UI.Post(() =>
            {
                var rsp = diffView.Show(diffs[0], commitId, repo.Path, reload, conflicts);
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

    public void CherryPick() =>
        Do(async () =>
        {
            var sha = repo.RowCommit.Id;
            var selection = repo.RepoView.Selection;
            var (i1, i2) = (selection.I1, selection.I2);
            if (i2 - i1 > 0)
            { // User selected range of commits
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
            { // User selected one commit
                if (!Try(out var e, await server.CherryPickAsync(sha, repo.Path)))
                {
                    return R.Error($"Failed to cherry pick", e);
                }
            }

            repo.RepoView.ClearSelection();
            RefreshAndCommit();
            return R.Ok;
        });

    public void Stash() =>
        Do(async () =>
        {
            if (repo.Repo.Status.IsOk)
                return R.Ok;
            var commitMsg = repo.Repo.CurrentCommit().Subject;
            if (!Try(out var msg, out var e, addStashDlg.Show()))
                return R.Ok;
            msg = msg == "" ? commitMsg : msg;

            if (!Try(out e, await server.StashAsync(msg, repo.Path)))
            {
                return R.Error($"Failed to stash changes", e);
            }

            Refresh();
            return R.Ok;
        });

    public void StashPop(string name) =>
        Do(async () =>
        {
            if (!repo.Repo.Status.IsOk)
                return R.Ok;

            if (!Try(out var e, await server.StashPopAsync(name, repo.Path)))
            {
                return R.Error($"Failed to pop stash {name}", e);
            }

            Refresh();
            return R.Ok;
        });

    public void StashDiff(string name) =>
        Do(async () =>
        {
            var reload = DiffReloads.Single(n => server.GetStashDiffAsync(name, n, repo.Path));
            if (!Try(out var diffs, out var e, await reload(DiffContext.Default)))
            {
                return R.Error($"Failed to diff stash {name}", e);
            }

            diffView.Show(diffs[0], name, repo.Path, reload, ConflictState.None);
            return R.Ok;
        });

    public void StashDrop(string name) =>
        Do(async () =>
        {
            if (!Try(out var e, await server.StashDropAsync(name, repo.Path)))
            {
                return R.Error($"Failed to drop stash {name}", e);
            }

            Refresh();
            return R.Ok;
        });

    public void UndoCommit(string id) =>
        Do(async () =>
        {
            if (!CanUndoCommit())
                return R.Ok;
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

    public void UncommitLastCommit() =>
        Do(async () =>
        {
            if (!CanUncommitLastCommit())
                return R.Ok;

            if (!Try(out var e, await server.UncommitLastCommitAsync(repo.Path)))
            {
                return R.Error($"Failed to undo commit", e);
            }

            Refresh();
            return R.Ok;
        });

    public void UncommitUntilCommit(string id) =>
        Do(async () =>
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

    public void SquashCommits(string id1, string id2) =>
        Do(async () =>
        {
            var c1 = repo.Repo.CommitById[id1];
            var c2 = repo.Repo.CommitById[id2];
            if (!c2.ParentIds.Any())
                return R.Error("Last commit does not have a parent");
            if (c1.BranchName != c2.BranchName)
                return R.Error("Commits are not on the same branch");
            var branch = repo.Repo.BranchByName[c1.BranchName];
            if (!branch.IsLocalCurrent)
                return R.Error("Commits not on current branch");

            var commits = new List<Commit>();
            var c = c1;
            while (c.Id != c2.ParentIds[0])
            {
                commits.Add(c);
                if (!c.ParentIds.Any())
                    break;
                c = repo.Repo.CommitById[c.ParentIds[0]];
            }

            if (!squashDlg.Show(repo, commits, out var message))
                return R.Ok;

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
        if (!repo.Repo.ViewCommits.Any())
            return false;

        var c = repo.Repo.ViewCommits[0];
        var b = repo.Repo.BranchByName[repo.Repo.ViewCommits[0].BranchName];
        return repo.Repo.Status.IsOk && c.IsAhead || (!b.IsRemote && b.RemoteName == "");
    }

    public void UndoUncommittedFile(string path) =>
        Do(async () =>
        {
            if (!Try(out var e, await server.UndoUncommittedFileAsync(path, repo.Path)))
            {
                return R.Error($"Failed to undo {path}", e);
            }

            Refresh();
            return R.Ok;
        });

    public void UndoUncommittedFiles(IReadOnlyList<string> paths) =>
        Do(async () =>
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

    public void AddTag() =>
        Do(async () =>
        {
            var commit = repo.RowCommit;
            var branch = repo.Repo.BranchByName[commit.BranchName];
            var isPushable = branch.IsRemote || branch.RemoteName != "";

            if (commit.IsUncommitted)
                return R.Ok;

            if (!Try(out var tag, addTagDlg.Show()))
                return R.Ok;

            if (tag.message == "")
            {
                if (!Try(out var e, await server.AddTagAsync(tag.name, commit.Id, isPushable, repo.Path)))
                    return R.Error($"Failed to add tag {tag.name}", e);
            }
            else
            {
                if (
                    !Try(
                        out var e,
                        await server.AddAnnotatedTagAsync(tag.name, tag.message, commit.Id, isPushable, repo.Path)
                    )
                )
                    return R.Error($"Failed to add tag {tag.name} '{tag.message}'", e);
            }

            Refresh();
            return R.Ok;
        });

    public void DeleteTag(string name) =>
        Do(async () =>
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

    public void ShowFileHistory() =>
        Do(async () =>
        {
            var commit = repo.RowCommit;
            // The files to pick from are the ones the reference has, while the history then shown
            // for the picked one is its full history, so the title names where the list came from
            var (reference, title) = commit.IsUncommitted
                ? (commit.BranchName, $"Select File of Branch {commit.BranchName}")
                : (commit.Id, $"Select File of Commit {commit.Id.Sid()}");

            if (!Try(out var files, out var e, await server.GetFileAsync(reference, repo.Path)))
            {
                return R.Error($"Failed to get files", e);
            }

            var browser = new FileBrowseDlg();
            if (!Try(out var path, browser.Show(files, title)))
                return R.Ok;

            DiffReload reload = n => server.GetFileDiffAsync(path, n, repo.Path);
            if (!Try(out var diffs, out e, await reload(DiffContext.Default)))
            {
                return R.Error($"Failed to show file history", e);
            }

            diffView.Show(diffs, repo.Path, reload);
            return R.Ok;
        });

    public void BlameFile() =>
        Do(async () =>
        {
            var commit = repo.RowCommit;
            // The uncommitted row has no tree of its own, so its branch supplies the file list. It
            // then blames the working tree, so the uncommitted lines are part of the answer.
            var (reference, title) = commit.IsUncommitted
                ? (commit.BranchName, $"Blame File of Branch {commit.BranchName}")
                : (commit.Id, $"Blame File of Commit {commit.Id.Sid()}");

            if (!Try(out var files, out var e, await server.GetFileAsync(reference, repo.Path)))
            {
                return R.Error($"Failed to get files", e);
            }

            var browser = new FileBrowseDlg();
            if (!Try(out var path, browser.Show(files, title)))
                return R.Ok;

            // Git blames a binary file as text, which arrives as mojibake. Only checked when the
            // file is in the working tree, since blaming an old revision of a since deleted file
            // is legitimate and IsText says 'not text' for a file that is not there.
            var fullPath = System.IO.Path.Join(repo.Path, path);
            if (File.Exists(fullPath) && !Files.IsText(fullPath))
            {
                return R.Error($"Cannot blame a binary file:\n{path}");
            }

            var blameReference = commit.IsUncommitted ? "" : commit.Id;
            if (!Try(out var blame, out e, await server.GetBlameAsync(path, blameReference, repo.Path)))
            {
                return R.Error($"Failed to blame {path}", e);
            }

            UI.Post(() => blameView.Show(blame, repo.Repo));
            return R.Ok;
        });

    // public void SquashHeadTo(string id) => Do(async () =>
    // {
    //     // if (!Try(out var e, await server.RebaseBranchAsync(repo.Repo, branchName)))
    //     //     return R.Error($"Failed to rebase branch {branchName}", e);

    //     RefreshAndFetch();
    //     return R.Ok;
    // });

    void Do(Func<Task<R>> action) => CommandRunner.Do(progress, action);

    async Task<bool> CheckBinaryOrLargeAddedFilesAsync()
    {
        var addFiles = repo.Repo.Status.AddedFiles.ToList();
        var addAndModified = addFiles
            .Concat(repo.Repo.Status.ModifiedFiles)
            .Concat(repo.Repo.Status.RenamedTargetFiles)
            .ToList();

        var binaryFiles = addAndModified.Where(f => !Files.IsText(Path.Join(repo.Path, f))).ToList();

        if (binaryFiles.Any())
        {
            var msg =
                $"There are {binaryFiles.Count} binary modified files:\n"
                + $"  {string.Join("\n  ", binaryFiles)}"
                + "\n\nDo you want to commit them as they are\nor first undo/revert them and then commit?";
            var rsp = UI.InfoMessage("Binary Files Detected !", msg, 1, ["Commit", "Undo", "Cancel"]);
            if (rsp == 2 || rsp == -1)
                return false; // Cancel

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
            .Select(f => $"{f} ({Files.FileSize(f).FileSize()})")
            .ToList();

        if (largeFiles.Any())
        {
            var msg =
                $"There are {largeFiles.Count} added large files:\n"
                + $"  {string.Join("\n  ", largeFiles)}"
                + "\n\nDo you want to continue?";
            if (0 != UI.InfoMessage("Large Files Detected !", msg, 1, ["Yes", "No"]))
            {
                return false;
            }
        }

        return true;
    }
}
