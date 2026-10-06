using System.Text;

namespace gmd.Server.Private;

// The change log, CHANGELOG.md, made from the commits of the main branch. A release is a commit
// tagged with its version ('v0.94.1431.496'), and its notes are the messages of that commit and of
// the untagged commits before it, back to the previous release. The notes of a merge are the lines
// after its subject, i.e. the list written in the merge commit message.
static class ChangeLog
{
    // The subject of the commit CI makes for each release from main, which holds the bumped version
    // and this change log (see .github/workflows/build-and-release.yml). It has no notes of its own.
    public const string ReleaseCommitPrefix = "Release v";

    // The commits after the latest release are headed 'newRelease', the version CI is about to
    // release them as, or "Current" when they are not being released (null).
    public static string Create(Repo repo, string? newRelease, DateTime now)
    {
        var nextTag = newRelease ?? "Current";
        var nextTagDate = now;
        var totalText = new StringBuilder();
        var text = "";
        var count = 0;
        foreach (Commit c in repo.ViewCommits)
        {
            if (c.IsUncommitted)
                continue;

            var tag = c.Tags.FirstOrDefault(t => t.Name.StartsWith('v') && Version.TryParse(t.Name[1..], out var _));
            if (tag != null)
            { // New version
                if (text.Trim() != "")
                {
                    totalText.Append($"\n## [{nextTag}] - {nextTagDate.IsoDate()}\n{text}\n");
                    if (nextTag == newRelease)
                        count++; // The new release, which has no tag yet
                }

                nextTag = tag.Name;
                nextTagDate = c.AuthorTime;
                text = "";
                count++;
            }

            // Git messages have no final newline, so the notes of two commits are put on lines of their own
            var notes = Notes(c);
            if (notes != "")
                text = text == "" ? notes : $"{text}\n{notes}";
        }

        return $"\n{count} releases:\n{totalText}";
    }

    static string Notes(Commit c)
    {
        if (c.Subject.StartsWith(ReleaseCommitPrefix))
            return "";

        var message = c.Message;
        var parts = c.Message.Split('\n');
        if (c.ParentIds.Count > 1 && parts.Length > 2 && parts[1].Trim() == "")
        {
            message = string.Join('\n', parts.Skip(2));
        }
        else if (parts.Length == 1)
        {
            message = $"- {parts[0]}";
        }

        // Adjust some message lines
        return message
            .Split('\n')
            .Select(l =>
            {
                if (l.StartsWith("- Fix "))
                    l = $"- Fixed {l[6..]}";
                if (l.StartsWith("- Add "))
                    l = $"- Added {l[6..]}";
                if (l.StartsWith("- Update "))
                    l = $"- Updated {l[9..]}";
                return l;
            })
            .Join("\n");
    }
}
