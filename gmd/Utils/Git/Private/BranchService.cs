using System.Text.RegularExpressions;

namespace gmd.Utils.Git.Private;

interface IBranchService
{
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync();
}


class BranchService : IBranchService
{
    static readonly string regexpText =
     @"(?im)^(\*)?\s+(\(HEAD detached at (\S+)\)|(\S+))\s+(\S+)(\s+)?(\[(\S+)(:\s)?(ahead\s(\d+))?(,\s)?(behind\s(\d+))?(gone)?\])?(\s+)?(.+)?";
    static string remotePrefix = "remotes/";
    static string originPrefix = "origin/";
    static readonly Regex BranchesRegEx = new Regex(regexpText,
     RegexOptions.Compiled | RegexOptions.CultureInvariant |
     RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private readonly ICmd cmd;

    public BranchService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<IReadOnlyList<Branch>>> GetBranchesAsync()
    {
        var args = "branch -vv --no-color --no-abbrev --all";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.ErrorMessage);
        }

        return ParseLines(cmdResult.Output);
    }

    private R<IReadOnlyList<Branch>> ParseLines(string output)
    {
        List<Branch> branches = new List<Branch>();

        var matches = BranchesRegEx.Matches(output);
        foreach (Match match in matches)
        {
            if (!IsPointerBranch(match))
            {
                Branch branch = ToBranch(match);
                branches.Add(branch);
            }
        }

        Log.Info($"Got {branches.Count} branches");
        return branches;
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
        string displayName = name.StartsWith(originPrefix) ?
            name.Substring(originPrefix.Length) :
            name;

        string tipId = match.Groups[5].Value;
        string remoteName = match.Groups[8].Value;
        int.TryParse(match.Groups[11].Value, out int aheadCount);
        int.TryParse(match.Groups[14].Value, out int behindCount);
        bool isRemoteMissing = match.Groups[15].Value == "gone";
        // string message = (match.Groups[17].Value ?? "").TrimEnd('\r');

        return new Branch(
            name, displayName, tipId, isCurrent, isRemote, remoteName, isDetached,
            aheadCount, behindCount, isRemoteMissing);
    }

    private static bool IsPointerBranch(Match match) => match.Groups[5].Value == "->";
}