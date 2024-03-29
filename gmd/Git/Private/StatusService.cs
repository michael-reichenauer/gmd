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
        List<string> deletedFiles = new List<string>();
        int modified = 0;
        List<string> modifiedFiles = new List<string>();
        int renamed = 0;
        List<string> renamedSourceFiles = new List<string>();
        List<string> renamedTargetFiles = new List<string>();


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
                conflictsFiles.Add(line.Substring(3).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("UU "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("AA "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("UD "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("DU "))
            {
                conflicted++;
                conflictsFiles.Add(line.Substring(3).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("?? ") || line.StartsWith(" A "))
            {
                added++;
                addedFiles.Add(line.Substring(3).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("D"))
            {
                deleted++;
                deletedFiles.Add(line.Substring(2).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("R"))
            {
                renamed++;
                var parts = line.Substring(2).Split(" -> ");
                renamedSourceFiles.Add(parts[0].Trim().Replace("\"", ""));
                renamedTargetFiles.Add(parts[1].Trim().Replace("\"", ""));
            }
            else
            {
                modified++;
                modifiedFiles.Add(line.Substring(2).Trim().Replace("\"", ""));
            }
        }
        (string mergeMessage, string mergeHeadId, bool isMerging) = GetMergeStatus(wd);

        return new Status(modified, added, deleted, conflicted, renamed, isMerging, mergeMessage, mergeHeadId,
            modifiedFiles.ToArray(), addedFiles.ToArray(), deletedFiles.ToArray(), conflictsFiles.ToArray(),
            renamedSourceFiles.ToArray(), renamedTargetFiles.ToArray());
    }

    (string, string, bool) GetMergeStatus(string wd)
    {
        string mergeHeadPath = Path.Join(wd, ".git", "MERGE_HEAD");
        string mergeMsgPath = Path.Join(wd, ".git", "MERGE_MSG");

        if (!File.Exists(mergeMsgPath))
        {
            return ("", "", false);
        }

        // Read the merge message
        if (!Try(out var mergeMessage, out var e, () => File.ReadAllText(mergeMsgPath))) return ("", "", false);

        var lines = mergeMessage.Split('\n');
        mergeMessage = lines[0].Trim();

        // Read the merge head id (from commit)
        if (Try(out var mergeHeadId, out e, () => File.ReadAllText(mergeHeadPath)))
        {
            mergeHeadId = mergeHeadId.Trim();
        }

        return (mergeMessage, mergeHeadId ?? "", true);
    }

    internal static bool IsMergeInProgress(string wd) =>
        File.Exists(Path.Join(wd, ".git", "MERGE_MSG"));
}
