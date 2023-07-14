using System.Text.RegularExpressions;

namespace gmd.Git.Private;

interface IBranchService
{
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<R> CheckoutAsync(string name, string wd);
    Task<R> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<R> MergeBranchAsync(string name, string wd);
    Task<R> CherryPickAsync(string sha, string wd);
}

class BranchService : IBranchService
{
    static string remotePrefix = "remotes/";

    static readonly string regexpText =
     @"(?im)^(\*)?\s+(\(HEAD detached at (\S+)\)|(\S+))\s+(\S+)(\s+)?(\[(\S+)(:\s)?(ahead\s(\d+))?(,\s)?(behind\s(\d+))?(gone)?\])?(\s+)?(.+)?";
    static readonly Regex BranchesRegEx = new Regex(regexpText,
     RegexOptions.Compiled | RegexOptions.CultureInvariant |
     RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private readonly ICmd cmd;

    public BranchService(ICmd cmd)
    {
        this.cmd = cmd;
    }


    public async Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd)
    {
        var args = "branch -vv --no-color --no-abbrev --all";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

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

        return matches.Where(IsNormalBranch)
            .Select(ToBranch).ToList();
    }

    Branch ToBranch(Match match)
    {
        bool isCurrent = match.Groups[1].Value == "*";
        bool isDetached = !string.IsNullOrEmpty(match.Groups[3].Value);

        bool isRemote = false;
        string name = isDetached ? $"({match.Groups[3].Value})" : match.Groups[4].Value;
        if (name.StartsWith(remotePrefix))
        {
            isRemote = true;
            name = name.Substring(remotePrefix.Length);
        }

        if (isDetached)
        {
            // name = $"DETACHED-{match.Groups[3].Value}";
            name = "DETACHED";
        }

        string tipId = match.Groups[5].Value;
        string remoteName = match.Groups[8].Value;

        int.TryParse(match.Groups[11].Value, out int aheadCount);
        int.TryParse(match.Groups[14].Value, out int behindCount);

        return new Branch(name, tipId, isCurrent, isRemote, remoteName, isDetached, aheadCount, behindCount);
    }


    // IsNormalBranch returns true if branch is normal and not a pointer branch
    bool IsNormalBranch(Match match) => match.Groups[5].Value != "->";
}