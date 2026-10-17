using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// Whether commits can be rewritten, by Amend, Drop or Squash, and why not when they cannot. One rule
// for all of them, pushed or not: the commits are the current branch's, with nothing in the way of
// the rebase that rewrites them, i.e. no merge after them, which the rebase would flatten into plain
// commits, and no other branch or tag on them or after them, which would be left on the old ones.
// Commits on origin are rewritten too, and replaced there by a force push, which 'p' then offers
// (RewrittenHere): not while origin has commits the branch has not, which that push would drop. It
// is asked first (PushedWarning) when the branch is one others build on, or some of the commits on
// origin are someone else's. Uncommit, a reset that would leave the branch behind origin, is for
// commits not pushed only.
//
// Plain functions of the repo, so they are testable without a view. The augmented service checks the
// same against git before it rewrites anything (CommitRewriteService.CheckRewritableAsync).
static class CommitRewrite
{
    // Not pushed yet: ahead of its remote branch, or a commit of a local branch never pushed. The one
    // rule for Amend, Uncommit and Drop, which before it was only 'ahead' for Amend, so the last
    // commit of a branch never pushed could be uncommitted but not amended.
    public static bool IsNotPushed(Repo repo, Commit commit) =>
        !commit.IsUncommitted
        && (
            commit.IsAhead
            || (repo.BranchByName.TryGetValue(commit.BranchName, out var b) && !b.IsRemote && b.RemoteName == "")
        );

    // Why the last commit cannot be amended, the 'a' key, or "" when it can: 'git commit --amend'
    // rewrites it alone, so only the rule for commits on origin applies
    public static string WhyNotAmendLast(Repo repo)
    {
        var current = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (current == null || current.IsDetached || current.IsRemote)
            return "Not on a branch: switch to one first, whose last commit is amended";
        var last = repo.CurrentCommit();
        if (last.Id == Repo.EmptyRepoCommitId)
            return "There is no commit to amend yet";
        return IsNotPushed(repo, last) ? "" : WhyNotForcePushed(repo, current);
    }

    // Why a commit older than the last one cannot be amended, or "" when it can. Changes in the
    // working tree are no reason: the ones not added to it are put aside while it runs.
    public static string WhyNotAmend(Repo repo, Commit commit) =>
        commit.IsUncommitted ? "A commit is amended: move to one first" : WhyNot(repo, commit, "amended");

    // Why a commit cannot be dropped, or "" when it can. The files go back to before it, so changes
    // that git would have to overwrite are committed or stashed first.
    public static string WhyNotDrop(Repo repo, Commit commit)
    {
        if (commit.IsUncommitted)
            return "A commit is dropped: move to one first";
        if (WhyNot(repo, commit, "dropped") is var whyNot && whyNot != "")
            return whyNot;
        if (commit.ParentIds.Count == 0)
            return "The first commit cannot be dropped";
        if (!repo.Status.IsOk)
            return Why.Changes;
        return "";
    }

    // Why the commits from this one up to the last of the current branch cannot be uncommitted, i.e.
    // the branch reset to the commit before it with their changes kept, or "" when they can. Only
    // ones not pushed yet, since the reset would leave the branch behind origin, which a pull takes
    // back, and the first commit has nothing before it to reset to: the item was offered on it, and
    // on pushed commits, and threw on the first one.
    public static string WhyNotUncommit(Repo repo, Commit commit)
    {
        if (commit.IsUncommitted)
            return "Commits are uncommitted: move to one first";
        if (!repo.Status.IsOk)
            return Why.Changes;
        if (!IsNotPushed(repo, commit))
            return "Only a commit not yet pushed can be uncommitted";
        if (CommitsDownTo(repo, commit) is null)
            return "Only commits on the current branch can be uncommitted";
        if (commit.GitParentIds.Count == 0)
            return "The first commit cannot be uncommitted";
        return "";
    }

    // Why a commit cannot be moved past the next one up the current branch (isUp), or down it, or ""
    // when it can: the two are swapped, by a rebase from the older of them, on the rules Amend and
    // Drop have for that one, and both have to be the branch's
    public static string WhyNotMove(Repo repo, Commit commit, bool isUp)
    {
        if (commit.IsUncommitted)
            return "A commit is moved: move to one first";
        if (CommitsDownTo(repo, commit) is null)
            return "Only a commit on the current branch can be moved";
        if (Neighbour(repo, commit, isUp) is not { } other)
            return isUp
                ? "It is the last commit of the branch, with none after it to move it past"
                : "It is the first commit of the branch, with none before it to move it past";
        var older = isUp ? commit : other;
        if (older.GitParentIds.Count == 0)
            return "The first commit cannot be moved, nor be moved past";
        return WhyNot(repo, older, "moved");
    }

    // The commit after this one up the current branch (isUp), or before it down it, which a move
    // takes it past, or null when the branch has none there: the last commit has none after it, and
    // the first one of a branch made from another none before it, its parent being the other's
    public static Commit? Neighbour(Repo repo, Commit commit, bool isUp)
    {
        if (CommitsDownTo(repo, commit) is not { } commits)
            return null;
        if (isUp)
            return commits.Count >= 2 ? commits[^2] : null;
        if (commit.GitParentIds.Count == 0 || !repo.CommitById.TryGetValue(commit.GitParentIds[0], out var parent))
            return null;
        return CommitsDownTo(repo, parent) is null ? null : parent;
    }

    // Why the commits from this one up to the last of the current branch cannot be moved to a new
    // branch, e.g. made on main by mistake, or "" when they can: the ones Uncommit takes, not pushed
    // yet and with one before them for the branch to go back to, but with any changes left as they
    // are, since the new branch is checked out where the commits are, which moves no file
    public static string WhyNotMoveToNewBranch(Repo repo, Commit commit)
    {
        if (commit.IsUncommitted)
            return "Commits are moved: move to one first";
        if (repo.Status.IsMerging)
            return Why.InProgress;
        if (!IsNotPushed(repo, commit))
            return "Only commits not yet pushed can be moved to a new branch";
        if (CommitsDownTo(repo, commit) is null)
            return "Only commits on the current branch can be moved to a new branch";
        if (commit.GitParentIds.Count == 0)
            return "The first commit cannot be moved: the branch would have nothing left to be on";
        return "";
    }

    // Why the selected commits, the newest and the oldest of them, cannot be squashed into one, or ""
    // when they can. The menu offered it for any rows, and the command refused rows of two branches
    // or of another branch than the current in an error box.
    public static string WhyNotSquash(Repo repo, Commit newest, Commit oldest)
    {
        if (newest.IsUncommitted || oldest.IsUncommitted)
            return "The uncommitted changes are no commit to squash: select commits only";
        if (!repo.Status.IsOk)
            return Why.Changes;
        // A branch and its copy on origin are one, whose commits not pushed yet and pushed both are
        if (newest.BranchPrimaryName != oldest.BranchPrimaryName)
            return "The selected commits are on different branches: select commits of one branch";
        if (
            !repo.BranchByName.TryGetValue(newest.BranchName, out var branch)
            || (!branch.IsCurrent && !branch.IsLocalCurrent)
        )
            return "Only commits on the current branch can be squashed";
        if (oldest.ParentIds.Count == 0)
            return "The first commit cannot be squashed";
        // The oldest is pushed whenever any is, a branch being pushed from its first commit on
        if (!IsNotPushed(repo, oldest) && repo.AllBranches.FirstOrDefault(b => b.IsCurrent) is { } current)
            return WhyNotForcePushed(repo, current);
        return "";
    }

    // The commits a squash makes one, from the newest selected down its first parents to the oldest,
    // newest first. Once WhyNotSquash allows them, so the two are one branch's.
    public static IReadOnlyList<Commit> Squashed(Repo repo, Commit newest, Commit oldest)
    {
        List<Commit> commits = [];
        var c = newest;
        while (c.Id != oldest.ParentIds[0])
        {
            commits.Add(c);
            if (c.Id == oldest.Id || !c.ParentIds.Any() || !repo.CommitById.TryGetValue(c.ParentIds[0], out var parent))
                break;
            c = parent;
        }
        return commits;
    }

    // What to ask before commits on origin are rewritten, or "" when there is nothing to ask: when the
    // branch is one others build on (Branch.IsShared), or some of them are someone else's, by author,
    // since the force push replaces them for everyone, and whoever has them has to take the new ones.
    // The commits are all the rewrite replaces: an older commit's amend or drop replays the newer ones.
    public static string PushedWarning(Repo repo, IReadOnlyList<Commit> commits)
    {
        var current = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        var pushed = commits.Where(c => !c.IsUncommitted && !IsNotPushed(repo, c)).ToList();
        if (current == null || pushed.Count == 0)
            return "";
        var others =
            repo.CurrentAuthor == ""
                ? []
                : pushed.Select(c => c.Author).Where(a => a != repo.CurrentAuthor).Distinct().ToList();
        if (!current.IsShared && others.Count == 0)
            return "";

        var onOrigin =
            pushed.Count == commits.Count && pushed.Count == 1 ? "The commit it rewrites is on origin."
            : pushed.Count == commits.Count ? $"The {pushed.Count} commits it rewrites are on origin."
            : $"{pushed.Count} of the commits it rewrites {(pushed.Count == 1 ? "is" : "are")} on origin.";
        var shared = current.IsShared
            ? $"\n'{current.NiceNameUnique}' is {(current.IsMainBranch ? "the main branch" : "a shared branch")}, which others build on."
            : "";
        var byOthers =
            others.Count == 0
                ? ""
                : $"\n{OthersCount(pushed, others)} by {string.Join(" and ", others.Take(3))}{(others.Count > 3 ? " and others" : "")}.";
        var them = pushed.Count == 1 ? "it" : "them";
        return onOrigin
            + shared
            + byOthers
            + $"\n\nA force push of the rewrite replaces {them} on origin, for everyone,\n"
            + $"and whoever has {them} has to take the new version.\n\n"
            + "Rewrite all the same?";
    }

    static string OthersCount(IReadOnlyList<Commit> pushed, IReadOnlyList<string> others)
    {
        var count = pushed.Count(c => others.Contains(c.Author));
        return pushed.Count == 1 ? "It is"
            : count == pushed.Count ? (count == 2 ? "Both are" : "All are")
            : $"{count} of them {(count == 1 ? "is" : "are")}";
    }

    // Whether the dialog of a rewrite offers to push the branch with force when done: when some of the
    // commits it replaces are on origin, ticked unless the rewrite was asked about first
    public static ForcePush ForcePushOf(Repo repo, IReadOnlyList<Commit> commits) =>
        !commits.Any(c => !c.IsUncommitted && !IsNotPushed(repo, c)) ? ForcePush.NotOffered
        : PushedWarning(repo, commits) == "" ? ForcePush.Ticked
        : ForcePush.Offered;

    // The commits a rewrite of this commit replaces: it and the newer ones of the current branch,
    // which the rebase replays, newest first, or none when it is not on the current branch
    public static IReadOnlyList<Commit> RewrittenBy(Repo repo, Commit commit) => CommitsDownTo(repo, commit) ?? [];

    // Commits on origin are replaced there by a force push after the rewrite, leased on origin's tip as
    // shown, which would drop any commit origin has and the branch has not: those are pulled first.
    // Unless the branch was rewritten here already, when they are its old version, which it replaces.
    static string WhyNotForcePushed(Repo repo, Branch current) =>
        current.BehindCount > 0 && RewrittenHere.Of(repo, current) == null
            ? $"'{current.RemoteName}' has commits '{current.NiceNameUnique}' has not, which the force push "
                + "would drop: pull them first"
            : "";

    static string WhyNot(Repo repo, Commit commit, string done)
    {
        if (repo.Status.IsMerging)
            return Why.InProgress;
        if (CommitsDownTo(repo, commit) is not { } commits)
            return $"Only a commit on the current branch can be {done}";

        if (commit.ParentIds.Count > 1)
            return $"A merge cannot be {done}: the rebase would flatten it";
        if (commits.Any(c => c.ParentIds.Count > 1))
            return "There is a merge after this commit, which the rebase would flatten";

        // Before the branches on them, of which origin's new commits would be one
        var current = repo.AllBranches.First(b => b.IsCurrent);
        if (
            commits.Any(c => !IsNotPushed(repo, c))
            && WhyNotForcePushed(repo, current) is var pullFirst
            && pullFirst != ""
        )
            return pullFirst;

        // Origin's copy of the branch is no other branch: the force push after the rewrite replaces it
        var ids = commits.Select(c => c.Id).ToHashSet();
        if (
            repo.AllBranches.FirstOrDefault(b =>
                b.Name != current.Name && b.Name != current.RemoteName && ids.Contains(b.TipId)
            ) is
            { } other
        )
            return OnIt($"'{other.NiceNameUnique}'");
        // Nor is the old version of a branch rewritten here, which the force push replaces too
        var oldVersion = RewrittenHere.Of(repo, current)?.OldIds.ToHashSet() ?? [];
        if (
            commits
                .SelectMany(c => c.AllChildIds)
                .FirstOrDefault(id => id != Repo.UncommittedId && !ids.Contains(id) && !oldVersion.Contains(id)) is
            { } childId
        )
            return OnIt(
                repo.CommitById.TryGetValue(childId, out var child) ? $"'{child.BranchNiceUniqueName}'" : "A branch"
            );
        if (commits.SelectMany(c => c.Tags).FirstOrDefault() is { } tag)
            return OnIt($"The tag '{tag.Name}'");
        return "";
    }

    static string OnIt(string what) => $"{what} is on this commit or one after it, and would keep the old ones";

    // The commits of the current branch from its last one down to the commit, newest first, or null
    // when the commit is not one of them: the branch's own, here or on origin, which share the primary
    // name of the pair, or not pushed yet, as a commit another branch is built on can be taken to be
    // that branch's, which the check of the branches on them then names. Down git's first parents,
    // which is the line the rebase replays, whereas gmd swaps the parents of a pull merge to draw it.
    static IReadOnlyList<Commit>? CommitsDownTo(Repo repo, Commit commit)
    {
        var current = repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (current == null || current.IsRemote || current.IsDetached)
            return null;

        List<Commit> commits = [];
        var c = repo.CurrentCommit();
        while (!c.IsUncommitted && (c.BranchPrimaryName == current.PrimaryName || IsNotPushed(repo, c)))
        {
            commits.Add(c);
            if (c.Id == commit.Id)
                return commits;
            if (c.ParentIds.Count == 0 || !repo.CommitById.TryGetValue(c.GitParentIds[0], out var parent))
                return null;
            c = parent;
        }
        return null;
    }
}
