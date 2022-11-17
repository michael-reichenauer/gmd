namespace gmd.Git.Private;

interface IStatusService
{
    Task<R<Status>> GetStatusAsync(string wd);
}

class StatusService : IStatusService
{
    private readonly ICmd cmd;

    public StatusService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<Status>> GetStatusAsync(string wd)
    {
        var args = "status -s --porcelain --ahead-behind --untracked-files=all";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        return Parse(output, wd);
    }

    private R<Status> Parse(string statusText, string wd)
    {
        var lines = statusText.Split('\n');

        int conflicted = 0;
        List<string> conflictsFiles = new List<string>();
        int added = 0;
        List<string> addedFiles = new List<string>();
        int deleted = 0;
        int modified = 0;

        foreach (var lineText in lines)
        {
            string line = lineText.Trim();
            if (line == "")
            {
                continue;
            }

            if (line.StartsWith("DD ") ||
                line.StartsWith("AU ") ||
                line.StartsWith("UA "))
            {   // How to reproduce this ???
                conflicted++;
                conflictsFiles.Add(line.Substring(3));
            }
            else if (line.StartsWith("UU "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3));
            }
            else if (line.StartsWith("AA "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3));
            }
            else if (line.StartsWith("UD "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3));
            }
            else if (line.StartsWith("DU "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3));
            }
            else if (line.StartsWith("?? ") || line.StartsWith(" A "))
            {
                added++;
                addedFiles.Add(line.Substring(3));
            }
            else if (line.StartsWith(" D ") || line.StartsWith("D"))
            {
                deleted++;
            }
            else
            {
                modified++;
            }
        }
        (string mergeMessage, bool isMerging) = GetMergeStatus(wd);

        return new Status(modified, added, deleted, conflicted, isMerging, mergeMessage,
            addedFiles.ToArray(), conflictsFiles.ToArray());
    }

    (string, bool) GetMergeStatus(string wd)
    {
        string mergeMessage = "";
        //mergeIpPath := path.Join(h.cmd.RepoPath(), ".git", "MERGE_HEAD")
        string mergeMsgPath = Path.Join(wd, ".git", "MERGE_MSG");

        try
        {
            if (!File.Exists(mergeMsgPath))
            {
                return ("", false);
            }

            mergeMessage = File.ReadAllText(mergeMsgPath);
        }
        catch (Exception)
        {
            return ("", false);
        }

        var lines = mergeMessage.Split('\n');

        mergeMessage = lines[0].Trim();

        return (mergeMessage, true);
    }

    internal static bool IsMergeInProgress(string wd) =>
        File.Exists(Path.Join(wd, ".git", "MERGE_MSG"));
}