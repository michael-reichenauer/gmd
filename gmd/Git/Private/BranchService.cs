using System.Text.RegularExpressions;

namespace gmd.Git.Private;

interface IBranchService
{
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<R> CheckoutAsync(string name, string wd);
    Task<R> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<R> RenameBranchAsync(string oldName, string newName, string wd);
    Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<R> MergeBranchAsync(string name, string wd);
    Task<R> RebaseBranchAsync(string name, string wd);
    Task<R> RebaseOntoAsync(string newBase, string oldBase, string wd);
    Task<R> CherryPickAsync(string sha, string wd);
}

class BranchService : IBranchService
{
    static string remotePrefix = "remotes/";

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

    private readonly ICmd cmd;

    public BranchService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd)
    {
        var args = "branch -vv --no-color --no-abbrev --all";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd)))
            return e;

        return ParseBranches(output);
    }

    public async Task<R> CheckoutAsync(string name, string wd)
    {
        name = RemoteService.TrimRemotePrefix(name);
        return await cmd.RunAsync("git", $"checkout {name}", wd);
    }

    public async Task<R> CreateBranchAsync(string name, bool isCheckout, string wd)
    {
        string args = isCheckout ? "checkout -b" : "branch";
        args += $" {name}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd)
    {
        string args = isCheckout ? $"checkout -b" : $"branch";
        args += $" {name} {sha}";
        return await cmd.RunAsync("git", args, wd);
    }

    // Renames a local branch. Git moves the branch's reflog and renames its '[branch "<name>"]'
    // config section as well, and updates HEAD if the branch is the current branch, so no checkout
    // is needed. Note that '-m' (and not '-M') is used, since git should refuse rather than
    // overwrite if the new name is already taken.
    public async Task<R> RenameBranchAsync(string oldName, string newName, string wd)
    {
        oldName = RemoteService.TrimRemotePrefix(oldName);
        return await cmd.RunAsync("git", $"branch -m {oldName} {newName}", wd);
    }

    public async Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd)
    {
        string args = $"branch --delete {name}";
        args = isForced ? args + " -D" : args;
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> MergeBranchAsync(string name, string wd)
    {
        //  name = RemoteService.TrimRemotePrefix(name);
        var rsp = await cmd.RunAsync("git", $"merge --no-ff --no-commit --stat {name}", wd);
        if (rsp.IsResultError && rsp.Output.Contains("CONFLICT"))
        {
            return R.Error("Merge Conflicts!\nPlease resolve conflicts before committing", rsp);
        }
        return rsp;
    }

    public async Task<R> RebaseBranchAsync(string name, string wd)
    {
        //  name = RemoteService.TrimRemotePrefix(name);
        var rsp = await cmd.RunAsync("git", $"rebase --stat {name}", wd);
        if (rsp.IsResultError && rsp.Output.Contains("CONFLICT"))
        {
            return R.Error("Merge Conflicts!\nPlease resolve conflicts before committing", rsp);
        }
        return rsp;
    }

    public async Task<R> RebaseOntoAsync(string newBase, string oldBase, string wd)
    {
        //  name = RemoteService.TrimRemotePrefix(name);
        var rsp = await cmd.RunAsync("git", $"rebase --onto {newBase} {oldBase}", wd);
        if (rsp.IsResultError && rsp.Output.Contains("CONFLICT"))
        {
            return R.Error("Merge Conflicts!\nPlease resolve conflicts before committing", rsp);
        }
        return rsp;
    }

    public async Task<R> CherryPickAsync(string sha, string wd)
    {
        var rsp = await cmd.RunAsync("git", $"cherry-pick --no-commit {sha}", wd);
        if (rsp.IsResultError && rsp.Output.Contains("CONFLICT"))
        {
            return R.Error("Merge Conflicts!\nPlease resolve conflicts before committing", rsp);
        }
        return rsp;
    }

    R<IReadOnlyList<Branch>> ParseBranches(string output)
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
            if (name.StartsWith(remotePrefix))
            {
                isRemote = true;
                name = name[remotePrefix.Length..];
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
