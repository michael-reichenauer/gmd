using gmd.Common;
using gmd.Cui.Branches;
using gmd.Cui.Common;
using gmd.Cui.Recovery;
using gmd.Server;

namespace gmd.Cui.RepoView;

// Creating, renaming and deleting branches, i.e. the commands that ask the user for a name or for
// what to delete first, and then run the git command for them.
interface IBranchCreateCommands
{
    void CreateBranchFromBranch(string name);
    void CreateBranchFromCommit();
    void CreateBranchFromLostCommit(string commitId, string madeOnName);
    void RenameBranch(string name);
    void DeleteBranch(string name);
    void DeleteMergedBranches();
    void MoveCommitsToNewBranch(string commitId);
}

class BranchCreateCommands : IBranchCreateCommands
{
    readonly CommandContext context;
    readonly IProgress progress;
    readonly IStatusLine status;
    readonly IRepoView repoView;
    readonly IServer server;
    readonly IRepoConfigService repoConfig;
    readonly ICreateBranchDlg createBranchDlg;
    readonly IRenameBranchDlg renameBranchDlg;
    readonly IDeleteBranchDlg deleteBranchDlg;
    readonly IMergedBranchesDlg mergedBranchesDlg;
    readonly IMoveCommitsDlg moveCommitsDlg;

    public BranchCreateCommands(
        CommandContext context,
        IRepoConfigService repoConfig,
        ICreateBranchDlg createBranchDlg,
        IRenameBranchDlg renameBranchDlg,
        IDeleteBranchDlg deleteBranchDlg,
        IMergedBranchesDlg mergedBranchesDlg,
        IMoveCommitsDlg moveCommitsDlg
    )
    {
        this.context = context;
        this.progress = context.Progress;
        this.status = context.Status;
        this.repoView = context.View;
        this.server = context.Server;
        this.repoConfig = repoConfig;
        this.createBranchDlg = createBranchDlg;
        this.renameBranchDlg = renameBranchDlg;
        this.deleteBranchDlg = deleteBranchDlg;
        this.mergedBranchesDlg = mergedBranchesDlg;
        this.moveCommitsDlg = moveCommitsDlg;
    }

    public void CreateBranchFromBranch(string name) =>
        Do(async repo =>
        {
            var branchName = "";
            try
            {
                var branch = repo.Repo.BranchByName[name];
                if (branch.LocalName != "")
                    name = branch.LocalName;

                if (createBranchDlg.Show(name, "") is not CreateBranchResult rsp)
                    return Result.Ok;

                var created = await server.CreateBranchFromBranchAsync(repo.Repo, rsp.Name, name, rsp.IsCheckout);
                if (created is Error e)
                {
                    return new Error($"Failed to create branch {rsp.Name}", e);
                }
                branchName = rsp.Name;

                return rsp.IsPush ? await PushNewBranchAsync(branchName) : Result.Ok;
            }
            finally
            {
                RefreshIfMade(branchName);
            }
        });

    // The uncommitted row is no commit to start a branch at, which the menu and the hints say, and
    // 'b' asked for a name and then failed in git
    public const string NoCommitToStartAt = "A branch starts at a commit: move to one first";

    public void CreateBranchFromCommit() =>
        Do(async repo =>
        {
            var commit = repo.RowCommit;
            if (commit.IsUncommitted)
                return new Notice(NoCommitToStartAt);
            return await CreateBranchAtAsync(commit.BranchName, commit.Id, "", true);
        });

    // A branch at a commit no branch has any more, brought back from Recover Lost Commits: named as
    // the branch it was made on when that name is free, since that is most often what is being
    // brought back, and not published by default, since what was lost was never more than local
    public void CreateBranchFromLostCommit(string commitId, string madeOnName) =>
        Do(async repo =>
        {
            var isNameFree =
                madeOnName != ""
                && !repo.Repo.BranchByName.ContainsKey(madeOnName)
                && !repo.Repo.BranchByName.ContainsKey($"origin/{madeOnName}");
            var from = madeOnName != "" ? madeOnName : "lost commit";
            return await CreateBranchAtAsync(from, commitId, isNameFree ? madeOnName : "", false);
        });

    async Task<Result> CreateBranchAtAsync(string fromName, string commitId, string name, bool isPublish)
    {
        var repo = context.ViewRepo;
        var branchName = "";
        try
        {
            if (createBranchDlg.Show(fromName, commitId.Sid(), name, isPublish) is not CreateBranchResult rsp)
                return Result.Ok;

            var created = await server.CreateBranchFromCommitAsync(repo.Repo, rsp.Name, commitId, rsp.IsCheckout);
            if (created is Error e)
            {
                return new Error($"Failed to create branch {rsp.Name}", e);
            }
            branchName = rsp.Name;

            return rsp.IsPush ? await PushNewBranchAsync(branchName) : Result.Ok;
        }
        finally
        {
            RefreshIfMade(branchName);
        }
    }

    // Once the branch is made, whatever happened after, e.g. a push that failed: a dialog cancelled
    // or a git refusal changed nothing, and refreshed, which also fetched, for nothing
    void RefreshIfMade(string branchName)
    {
        if (branchName != "")
            Refresh(branchName);
    }

    // Renames both the local and the remote branch, since renaming only the local branch would
    // hardly be visible: the local branch would still track the old remote branch, and gmd shows
    // the remote branch of a pair, i.e. the branch would still be shown with its old name.
    public void RenameBranch(string name) =>
        Do(async repo =>
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
                RefreshIfMade(newName);
            }
        });

    // Deletes the branch here, on origin or both, as the dialog says, and says how to bring it back,
    // since gmd records what it deleted (see DeletedBranchService.DeleteBranchAsync). Both sides are
    // checked for unmerged commits before either is deleted.
    public void DeleteBranch(string name) =>
        Do(async repo =>
        {
            var (localBranch, remoteBranch) = LocalAndRemoteBranch(name);

            var isLocal = localBranch != null;
            var isRemote = remoteBranch != null;
            if (deleteBranchDlg.Show(name, isLocal, isRemote) is not DeleteBranchResult rsp)
                return Result.Ok;

            var local = rsp.IsLocal ? localBranch : null;
            var remote = rsp.IsRemote ? remoteBranch : null;
            if (local == null && remote == null)
                return Result.Ok;
            foreach (var branch in new[] { remote, local }.OfType<Server.Branch>())
            {
                if (!rsp.IsForce && repo.Repo.IsUnmerged(branch))
                {
                    return new Error($"Branch {branch.Name}\nnot fully merged, use force option to delete.");
                }
            }

            // Squash merged or rebase merged on origin is merged, though git refuses it unforced
            var isForce = rsp.IsForce || (local != null && repo.Repo.MergedOnOrigin.ContainsKey(local.Name));

            Result deleted;
            var deletedName = local?.Name ?? remote!.Name;
            using (status.Progress($"Deleting '{deletedName}'"))
            {
                deleted = await server.DeleteBranchAsync(repo.Repo, local?.Name ?? "", remote?.Name ?? "", isForce);
            }

            Refresh((local ?? remote)!.PrimaryBaseName);
            if (deleted is Error e)
                return e;
            status.Info(DeletedBranchRows.Deleted(deletedName, local != null && remote != null));
            return Result.Ok;
        });

    // The branches whose work is in main or an integration branch already, the local ones merged as
    // git counts it or squash or rebase merged on origin and those origin has merged as git counts it,
    // listed to delete one at a time, or all at once after asking. Shown again after each delete with
    // the ones left, until closed. Each is deleted forced, since gmd told it merged and git may not (a
    // squash merge, or a merge into main when another branch is checked out), and recorded for Restore
    // Deleted Branch. One on origin is deleted there only after a question of its own, with Cancel the
    // default, or by Delete All's Also on Origin, since that deletes it for everyone.
    public void DeleteMergedBranches() =>
        Do(async repo =>
        {
            Result<IReadOnlyList<MergedBranch>> mergedResult;
            using (status.Progress("Looking for merged branches"))
            {
                mergedResult = await server.GetMergedBranchesAsync(repo.Repo);
            }
            if (mergedResult is not IReadOnlyList<MergedBranch> found)
                return new Error("Failed to look for merged branches", mergedResult.Error);
            if (found.Count == 0)
                return new Notice(MergedBranchRows.NoneToDelete);

            var branches = found.ToList();
            List<MergedBranch> deleted = [];
            try
            {
                var selected = 0;
                while (
                    branches.Count > 0
                    && mergedBranchesDlg.Show(repo.Repo, branches, selected) is MergedBranchChoice choice
                )
                {
                    selected = Math.Max(0, branches.IndexOf(choice.Branch));
                    if (choice.Action == MergedBranchAction.Show)
                    {
                        ShowMergedBranch(choice.Branch);
                        continue;
                    }

                    List<MergedBranch> toDelete;
                    if (choice.Action == MergedBranchAction.DeleteAll)
                    {
                        var deletable = branches.Where(MergedBranchRows.CanDelete).ToList();
                        var answer = Confirm.DeleteMergedBranches(
                            MergedBranchRows.DeleteAllQuestion(branches),
                            deletable.Any(b => !b.IsOnOrigin),
                            deletable.Any(b => b.IsOnOrigin)
                        );
                        if (answer == MergedDeleteChoice.Cancel)
                            continue;
                        toDelete = deletable
                            .Where(b => answer == MergedDeleteChoice.AlsoOnOrigin || !b.IsOnOrigin)
                            .ToList();
                    }
                    else
                    {
                        if (
                            choice.Branch.IsOnOrigin
                            && !Confirm.DeleteMergedOnOrigin(MergedBranchRows.DeleteOnOriginQuestion(choice.Branch))
                        )
                            continue;
                        toDelete = [choice.Branch];
                    }

                    foreach (var b in toDelete)
                    {
                        using (status.Progress($"Deleting '{b.Name}'"))
                        {
                            if (await server.DeleteBranchAsync(repo.Repo, b.LocalName, b.RemoteName, true) is Error e)
                                return new Error($"Failed to delete '{b.Name}'", e);
                        }
                        deleted.Add(b);
                        branches.Remove(b);
                    }
                    selected = Math.Min(selected, Math.Max(0, branches.Count - 1));
                }
            }
            finally
            {
                if (deleted.Count > 0)
                    Refresh();
            }

            if (deleted is [var one])
                status.Info(
                    DeletedBranchRows.Deleted(one.IsLocal ? one.Name : one.RemoteName, one.IsLocal && one.IsOnOrigin)
                );
            else if (deleted.Count > 1)
                status.Info(MergedBranchRows.Deleted(deleted.Count));
            return Result.Ok;
        });

    // Shows a merged branch in the log behind the dialog, its last commit near the top, above the
    // dialog, which is shown again over it: origin's branch of a pair, which the pair is drawn as
    void ShowMergedBranch(MergedBranch merged)
    {
        var name = merged.IsOnOrigin ? merged.RemoteName : merged.LocalName;
        if (!context.ViewRepo.Repo.BranchByName.ContainsKey(name))
            return;
        context.BranchCmds.ShowBranch(name, merged.TipId, MergedBranchTipRow);
    }

    // The row of the log view a merged branch's last commit is shown on, with the newer commits of
    // main, e.g. its merge, in the rows above, and the branch below, above a dialog of most sizes
    const int MergedBranchTipRow = 3;

    // A branch name can name either the local or the remote branch of a pair, and a command usually
    // needs both of them, so resolve the name to the pair it is part of (either one can be missing)
    (Server.Branch? local, Server.Branch? remote) LocalAndRemoteBranch(string name)
    {
        var repo = context.ViewRepo;
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
        var repo = context.ViewRepo;
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
                // Renamed, two orders can be of one pair, or of a branch against itself
                s.BranchOrders = BranchOrder.OnePerPair(
                    s.BranchOrders.Select(b => new BranchOrder
                    {
                        Branch = Renamed(b.Branch),
                        Other = Renamed(b.Other),
                        Order = b.Order,
                    })
                );

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

    // The commits of the current branch from this one up, not pushed yet, moved to a new branch, e.g.
    // ones made on main by mistake: the new branch is checked out where the commits are, so the files
    // and any changes stay as they are, and the branch goes back to the commit before them, which Undo
    // in its branch menu takes back. Published as Create Branch publishes, when the box is ticked.
    public void MoveCommitsToNewBranch(string commitId) =>
        Do(async repo =>
        {
            var commit = repo.Repo.CommitById[commitId];
            if (CommitRewrite.WhyNotMoveToNewBranch(repo.Repo, commit) is var whyNot && whyNot != "")
                return new Notice(whyNot);
            var current = repo.Repo.CurrentBranch();
            var count = CommitRewrite.RewrittenBy(repo.Repo, commit).Count;
            var backTo = commit.GitParentIds[0].Sid();
            var isPublish = current.RemoteName != "";
            if (moveCommitsDlg.Show(current.NiceNameUnique, count, backTo, isPublish) is not MoveCommitsResult rsp)
                return Result.Ok;

            using (status.Progress($"Moving {count} commits to '{rsp.Name}'"))
            {
                if (await server.MoveCommitsToNewBranchAsync(repo.Repo, rsp.Name, commitId) is Error e)
                {
                    Refresh(rsp.Name);
                    return new Error($"Failed to move the commits to '{rsp.Name}'", e);
                }
                if (rsp.IsPush && await PushNewBranchAsync(rsp.Name) is Error pushError)
                {
                    Refresh(rsp.Name);
                    return pushError;
                }
            }

            Refresh(rsp.Name);
            var commits = count == 1 ? "1 commit" : $"{count} commits";
            status.Info(
                $"Moved {commits} of '{current.NiceNameUnique}' to '{rsp.Name}': "
                    + $"Undo in the branch menu of '{current.NiceNameUnique}' moves it forward again"
            );
            return Result.Ok;
        });

    // Pushes a branch just created. A repo with no 'origin' is a local only repo with nowhere to
    // push to, which is no failure to create the branch, so that push error is ignored.
    async Task<Result> PushNewBranchAsync(string branchName)
    {
        var repo = context.ViewRepo;
        if (await server.PushBranchAsync(branchName, repo.Path) is not Error e)
            return Result.Ok;
        if (e.Message.Contains("'origin' does not appear to be a git repository"))
            return Result.Ok;
        return new Error($"Failed to push branch {branchName} to remote server", e);
    }

    void Do(Func<IViewRepo, Task<Result>> action) => context.Do(action);
}
