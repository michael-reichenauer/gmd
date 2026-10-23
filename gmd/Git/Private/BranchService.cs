using System.Text.RegularExpressions;

namespace gmd.Git.Private;

interface IBranchService
{
    Task<Result<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<Result<IReadOnlyList<string>>> GetMergedBranchNamesAsync(string id, string wd);
    Task<Result> CheckoutAsync(string name, string wd);
    Task<Result> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<Result> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<Result> RenameBranchAsync(string oldName, string newName, string wd);
    Task<Result> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<Result> SetUpstreamAsync(string name, string remoteName, string wd);
    Task<Result> MoveBranchAsync(string name, string toId, string fromId, string message, string wd);
    Task<Result> MergeBranchAsync(string name, string wd);
    Task<Result> RebaseBranchAsync(string name, string wd);
    Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd);
    Task<Result> RebaseOntoRemoteAsync(string remoteName, string forkPointId, string wd);
    Task<Result> CherryPickAsync(string sha, string wd);
    Task<Result> CherryPickCommitsAsync(IReadOnlyList<string> idsOldestFirst, string wd);
}

class BranchService : IBranchService
{
    const string RemotePrefix = "remotes/";

    // Parses one line of 'git branch -vv --no-color --no-abbrev --all', e.g.
    //   '* main   8ec7cee… [origin/main: ahead 1, behind 2] Subject'
    // The first column is '*' for the current branch, '+' for a branch checked out in another
    // worktree, else a space. The '+' has to be matched: a line the regex does not match is not a
    // branch without a marker, it is no branch at all, and that silently dropped every branch a
    // linked worktree had checked out. Such a line also has the worktree's path in parenthesis
    // after the commit id, before the upstream, which has to be stepped over for the upstream to
    // be read at all:
    //   '+ dev    b6bb1a5… (/home/me/repo-dev) [origin/dev: ahead 1] Subject'
    // A detached HEAD has a parenthesized pseudo name instead of a branch name. Git writes it as
    // '(HEAD detached at|from <ref>)', or as '(no branch…)' while rebasing or bisecting. The forms
    // are matched explicitly rather than as any '(…)', since a git ref name may contain parenthesis.
    // Groups are named, so adding one does not shift the others (they used to be read by index).
    static readonly string regexpText =
        @"(?im)^(?:(?<current>\*)|(?<worktree>\+))?\s+(?:\((?<detached>HEAD\sdetached\s(?:at|from)\s\S+|no\sbranch[^)]*)\)|(?<name>\S+))"
        + @"\s+(?<tip>\S+)(?:\s+)?"
        + @"(?:\((?<worktreepath>.+?)\)\s+)?"
        + @"(?:\[(?<remote>\S+)(?::\s)?(?:ahead\s(?<ahead>\d+))?(?:,\s)?(?:behind\s(?<behind>\d+))?(?<gone>gone)?\])?"
        + @"(?:\s+)?(?<subject>.+)?";
    static readonly Regex BranchesRegEx = new Regex(
        regexpText,
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    readonly ICmd cmd;

    public BranchService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result<IReadOnlyList<Branch>>> GetBranchesAsync(string wd)
    {
        var args = "branch -vv --no-color --no-abbrev --all";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        return ParseBranches(output);
    }

    public async Task<Result> CheckoutAsync(string name, string wd)
    {
        name = RemoteService.TrimRemotePrefix(name);
        return await cmd.RunAsync("git", $"checkout {name}", wd);
    }

    public async Task<Result> CreateBranchAsync(string name, bool isCheckout, string wd)
    {
        string args = isCheckout ? "checkout -b" : "branch";
        args += $" {name}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<Result> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd)
    {
        string args = isCheckout ? $"checkout -b" : $"branch";
        args += $" {name} {sha}";
        return await cmd.RunAsync("git", args, wd);
    }

    // Renames a local branch. Git moves the branch's reflog and renames its '[branch "<name>"]'
    // config section as well, and updates HEAD if the branch is the current branch, so no checkout
    // is needed. Note that '-m' (and not '-M') is used, since git should refuse rather than
    // overwrite if the new name is already taken.
    public async Task<Result> RenameBranchAsync(string oldName, string newName, string wd)
    {
        oldName = RemoteService.TrimRemotePrefix(oldName);
        return await cmd.RunAsync("git", $"branch -m {oldName} {newName}", wd);
    }

    // The branches merged into a commit, i.e. whose tips it has: 'git branch --all --merged', the local
    // ones by name and the remote ones as e.g. 'origin/feature', the format pinned to the full ref name,
    // which config could otherwise color or mark with the '*' of the current branch or the '+' of one
    // checked out in another worktree, and which tells a remote's HEAD, a pointer, from its branches
    public async Task<Result<IReadOnlyList<string>>> GetMergedBranchNamesAsync(string id, string wd)
    {
        var result = await cmd.RunAsync("git", $"branch --all --merged {id} --format=%(refname)", wd);
        if (result is not string output)
            return result.Error;
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(refName =>
                refName.StartsWith("refs/heads/") ? refName["refs/heads/".Length..]
                : refName.StartsWith("refs/remotes/") && !refName.EndsWith("/HEAD") ? refName["refs/remotes/".Length..]
                : ""
            )
            .Where(name => name != "")
            .ToList();
    }

    public async Task<Result> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        await cmd.RunAsync("git", $"branch {(isForced ? "-D" : "-d")} {name}", wd);

    // Makes a local branch track a remote branch, e.g. 'origin/feature', which git forgets when the
    // branch is deleted: the '[branch "<name>"]' config section goes with it. Written as git writes it,
    // rather than with 'branch --set-upstream-to', which refuses a remote branch there is no
    // remote-tracking ref of: one deleted too and not pushed back yet, or one a narrowed fetch refspec
    // leaves out. The branch tracks it once it is there, as a branch whose remote branch is gone does.
    public async Task<Result> SetUpstreamAsync(string name, string remoteName, string wd)
    {
        var separator = remoteName.IndexOf('/');
        if (separator < 1)
            return new Error($"Not a remote branch: '{remoteName}'");
        var (remote, branch) = (remoteName[..separator], remoteName[(separator + 1)..]);

        if (await cmd.RunAsync("git", $"config branch.{name}.remote {remote}", wd) is Error e)
            return e;
        return await cmd.RunAsync("git", $"config branch.{name}.merge refs/heads/{branch}", wd);
    }

    // Moves a branch that is not checked out, with the message its reflog gets, and only if it is
    // still at fromId: git compares and moves in one step, so a branch something else moved in the
    // meantime is left where it is rather than losing what moved it
    public async Task<Result> MoveBranchAsync(string name, string toId, string fromId, string message, string wd) =>
        await cmd.RunAsync("git", $"update-ref -m {Cmd.Quote(message)} refs/heads/{name} {toId} {fromId}", wd);

    public async Task<Result> MergeBranchAsync(string name, string wd)
    {
        var rsp = await cmd.RunAsync("git", $"merge --no-ff --no-commit --stat {name}", wd);
        return ConflictError.ToConflict(rsp, "The merge stopped on conflicts");
    }

    public async Task<Result> RebaseBranchAsync(string name, string wd)
    {
        var rsp = await cmd.RunAsync("git", $"rebase --stat {name}", wd);
        return ConflictError.ToConflict(rsp, "The rebase stopped on conflicts");
    }

    // Moves the current branch's own commits, the ones after where it was built on its remote branch,
    // onto the remote branch, which a force push rewrote: what 'git pull --rebase' does with the fork
    // point it finds, done whatever pull.rebase says. A merge among them is kept as a merge.
    public async Task<Result> RebaseOntoRemoteAsync(string remoteName, string forkPointId, string wd)
    {
        var rsp = await cmd.RunAsync(
            "git",
            $"rebase --rebase-merges --onto refs/remotes/{remoteName} {forkPointId}",
            wd
        );
        return ConflictError.ToConflict(rsp, "The pull stopped on conflicts");
    }

    public async Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd)
    {
        var rsp = await cmd.RunAsync("git", $"rebase --onto {newBase} {oldBase}", wd);
        return ConflictError.ToConflict(rsp, "The rebase stopped on conflicts");
    }

    public async Task<Result> CherryPickAsync(string sha, string wd)
    {
        var rsp = await cmd.RunAsync("git", $"cherry-pick --no-commit {sha}", wd);
        return ConflictError.ToConflict(rsp, "The cherry pick stopped on conflicts");
    }

    // Git's own cherry pick of several commits, which commits each as it was made rather than staging
    // it for a message, as CherryPickAsync does, and which git can continue past a conflict, its
    // sequence of the ones left being its own (.git/sequencer)
    public async Task<Result> CherryPickCommitsAsync(IReadOnlyList<string> idsOldestFirst, string wd)
    {
        var rsp = await cmd.RunAsync("git", $"cherry-pick {string.Join(' ', idsOldestFirst)}", wd);
        return ConflictError.ToConflict(rsp, "The cherry pick stopped on conflicts");
    }

    Result<IReadOnlyList<Branch>> ParseBranches(string output)
    {
        var matches = BranchesRegEx.Matches(output);

        return matches.Where(IsNormalBranch).Select(ToBranch).ToList();
    }

    Branch ToBranch(Match match)
    {
        bool isCurrent = match.Groups["current"].Success;
        bool isCheckedOutElsewhere = match.Groups["worktree"].Success;
        bool isDetached = match.Groups["detached"].Success;

        // The ref a detached HEAD points at is not kept, all detached states share one name
        bool isRemote = false;
        string name = "DETACHED";
        if (!isDetached)
        {
            name = match.Groups["name"].Value;
            if (name.StartsWith(RemotePrefix))
            {
                isRemote = true;
                name = name[RemotePrefix.Length..];
            }
        }

        string tipId = match.Groups["tip"].Value;

        // Note: a 'gone' upstream still reports its remote name here. Augmenter.AddAugBranches
        // clears it, by requiring a matching remote branch rather than trusting this name.
        string remoteName = match.Groups["remote"].Value;

        int.TryParse(match.Groups["ahead"].Value, out int aheadCount);
        int.TryParse(match.Groups["behind"].Value, out int behindCount);

        return new Branch(
            name,
            tipId,
            isCurrent,
            isRemote,
            remoteName,
            isDetached,
            aheadCount,
            behindCount,
            isCheckedOutElsewhere
        );
    }

    // IsNormalBranch returns true if branch is normal and not a pointer branch, i.e. the
    // 'remotes/origin/HEAD -> origin/main' line, which has '->' where a commit id would be
    bool IsNormalBranch(Match match) => match.Groups["tip"].Value != "->";
}
