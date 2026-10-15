using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Commits;

// The rows of the commit details pane, which Enter opens below the log and the blame: the commit's
// id, branch, author and relatives, and its message. With no view, so they are tested without a
// terminal; CommitDetailsView draws them.
static class CommitDetailsRows
{
    public static IReadOnlyList<Text> Of(Repo repo, Commit commit, Branch branch, IBranchColorService colors)
    {
        var id = commit.Id;
        var color = colors.GetColor(repo, branch);

        if (id == Repo.UncommittedId)
        {
            id = "Uncommitted";
        }

        List<Text> rows = [];
        var repoText = $"  ({repo.Path})";

        if (commit.Id == Repo.UncommittedId)
        {
            rows.Add(Text.Dark("Id:         ").BrightYellow(id).Dark(repoText));
        }
        else
        {
            rows.Add(Text.Dark("Id:         ").White(id).Dark(repoText));
        }

        var branchName = branch.IsGitBranch ? branch.Name : "~" + branch.Name;
        branchName = $"{branch.NiceNameUnique}  ({branchName})";

        if (commit.IsBranchSetByUser)
        {
            rows.Add(
                Text.Dark("Branch:     ").Color(color, branchName).White("   Φ").Dark(" (ambiguity resolved by user)")
            );
        }
        else
        {
            if (commit.IsAmbiguous)
            {
                var ambBranches = string.Join(", ", branch.AmbiguousBranchNames.Take(3));
                if (branch.AmbiguousBranchNames.Count > 3)
                {
                    ambBranches += ",┅";
                }
                rows.Add(Text.Dark("Branch:     ").White(branchName));
                rows.Add(Text.Dark("Ambiguous:  ").White($"{ambBranches}"));
            }
            else
            {
                rows.Add(Text.Dark("Branch:     ").Color(color, branchName));
            }
        }

        if (commit.Author != "")
        {
            rows.Add(
                Text.Dark("Author:     ").White($"{commit.Author}").Dark(", time: ").White(commit.AuthorTime.IsoZone())
            );
        }

        rows.Add(
            Text.Dark("Children:   ")
                .White(string.Join(", ", commit.AllChildIds.Where(c => c != Repo.UncommittedId).Select(c => c.Sid())))
        );
        rows.Add(Text.Dark("Parents:    ").White(string.Join(", ", commit.GitParentIds.Select(p => p.Sid()))));
        if (commit.IsAhead)
        {
            rows.Add(Text.Dark("Remote:     ").Green("▲ pushable"));
        }
        if (commit.IsBehind)
        {
            rows.Add(Text.Dark("Remote:     ").Blue("▼ pullable"));
        }
        if (commit.Tags.Any())
        {
            var tagText = "[" + string.Join("][", commit.Tags.Select(t => t.Name)) + "]";
            rows.Add(Text.Dark("Tags:       ").Green(tagText));
        }
        var tips = repo.ViewBranches.Where(b => b.TipId == commit.Id);
        if (tips.Any())
        {
            var tipText = new TextBuilder();
            tips.ForEach(t => tipText.Color(colors.GetColor(repo, t), $"({t.Name})"));
            rows.Add(Text.Dark("Tips:       ").Add(tipText));
        }
        rows.AddRange(commit.Message.Split('\n').Select(l => Text.White(l).ToText()));

        return rows;
    }
}
