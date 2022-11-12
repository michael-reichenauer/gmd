using System.Text.RegularExpressions;

namespace gmd.Git.Private;

interface IBranchService
{
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<R> CheckoutAsync(string name, string wd);
    Task<R> MergeBranch(string name, string wd);
}

class BranchService : IBranchService
{
    static string remotePrefix = "remotes/";
    static string originPrefix = "origin/";

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
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd)))
        {
            return e;
        }

        return ParseBranches(output);
    }

    public async Task<R> CheckoutAsync(string name, string wd)
    {
        name = RemoteService.TrimRemotePrefix(name);
        return await cmd.RunAsync("git", $"checkout {name}", wd);
    }

    public async Task<R> MergeBranch(string name, string wd)
    {
        name = RemoteService.TrimRemotePrefix(name);
        return await cmd.RunAsync("git", $"merge --no-ff --no-commit --stat {name}", wd);
        // if strings.Contains(err.Error(), "exit status 1") &&
        //     strings.Contains(output, "CONFLICT") {
        //     return ErrConflicts
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
            name = $"({match.Groups[3].Value})";
        }

        string commonName = name.StartsWith(originPrefix) ?
            name.Substring(originPrefix.Length) :
            name;

        string tipId = match.Groups[5].Value;
        string remoteName = match.Groups[8].Value;

        int.TryParse(match.Groups[11].Value, out int aheadCount);
        int.TryParse(match.Groups[14].Value, out int behindCount);
        bool isRemoteMissing = match.Groups[15].Value == "gone";

        return new Branch(
            name, commonName, tipId, isCurrent, isRemote, remoteName, isDetached,
            aheadCount, behindCount, isRemoteMissing);
    }


    // IsNormalBranch returns true if branch is normal and not a pointer branch
    bool IsNormalBranch(Match match) => match.Groups[5].Value != "->";
}