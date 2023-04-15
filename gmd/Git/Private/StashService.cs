namespace gmd.Git.Private;

interface IStashService
{
    Task<R> Stash(string commitId, string message, string wd);
    Task<R<IReadOnlyList<Stash>>> GetStashes(string wd);
}

class StashService : IStashService
{
    private readonly ICmd cmd;

    public StashService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> Stash(string commitId, string message, string wd)
    {
        var msg = $"\"{message}: {commitId}\"";
        return await cmd.RunAsync("git", $"stash save -u {msg}", wd);
    }


    public async Task<R<IReadOnlyList<Stash>>> GetStashes(string wd)
    {
        if (!Try(out var output, out var e, await cmd.RunAsync("git", "stach list", wd, true)))
        {
            if (e.ErrorMessage.StartsWith("\n"))
            {   // Empty tag list (no tags yet)
                return new List<Stash>();
            }
            Log.Warn($"Failed to list staches, {e}");
            return e;
        }

        return ParseStashes(output);
    }

    R<IReadOnlyList<Stash>> ParseStashes(string output)
    {
        List<Stash> staches = new List<Stash>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            var parts = line.Split(':');
            if (parts.Length < 3) continue;

            var id = parts[0].Trim();
            var branch = parts[1];
            if (branch.StartsWith(" On "))
            {
                branch = branch.Substring(4);
            }
            else if (branch.StartsWith(" WIP on "))
            {
                branch = branch.Substring(8);
            }
            else
            {
                continue;
            }

            var message = parts[2].Trim();
            var commitId = "";
            if (parts.Length > 3)
            {
                commitId = parts[3].Trim();
            }

            var commitID = line.Substring(0, 40);
            var name = line.Substring(51);

            // Seems that some client add a suffix for some reason
            name = name.TrimSuffix("^{}");

            staches.Add(new Stash(id, branch, commitId, message));
        }

        return staches;
    }
}
