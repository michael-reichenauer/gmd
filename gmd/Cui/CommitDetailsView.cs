using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface ICommitDetailsView
{
    ContentView View { get; }

    void Set(Repo repo, Commit commit, Branch branch);
}

class CommitDetailsView : ICommitDetailsView
{
    ContentView? contentView;
    Server.Commit? commit;
    IReadOnlyList<Text> rows = new List<Text>();

    internal static readonly int ContentHeight = 11;
    private readonly IBranchColorService branchColorService;

    public CommitDetailsView(IBranchColorService branchColorService)
    {
        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = Pos.AnchorEnd(ContentHeight),
            Height = 0,
            Width = Dim.Fill(),
            IsTopBorder = true,
            IsShowCursor = false,
            IsScrollMode = true,
            IsFocus = false,
            // Height = Dim.Fill(),
        };

        this.branchColorService = branchColorService;
    }

    public ContentView View => contentView!;

    public int TotalRows => rows.Count;

    public void Set(Repo repo, Server.Commit commit, Branch branch) => UI.RunInBackground(async () =>
    {
        await Task.Yield();

        var tags = commit.Tags;

        this.commit = commit;
        var id = commit.Id;
        var color = branchColorService.GetColor(repo, branch);

        if (id == Repo.UncommittedId)
        {
            id = "Uncommitted";
        }

        var newRows = new List<Text>();
        var repoText = $"  ({repo.Path})";

        if (commit.Id == Repo.UncommittedId)
        {
            newRows.Add(Text.Dark("Id:         ").BrightYellow(id).Dark(repoText));
        }
        else
        {
            newRows.Add(Text.Dark("Id:         ").White(id).Dark(repoText));
        }

        var branchName = branch.IsGitBranch ? branch.Name : "~" + branch.Name;
        branchName = $"{branch.NiceNameUnique}  ({branchName})";

        if (commit.IsBranchSetByUser)
        {
            newRows.Add(Text.Dark("Branch:     ").Color(color, branchName).White("   Φ").Dark(" (ambiguity resolved by user)"));
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
                newRows.Add(Text.Dark("Branch:     ").White(branchName));
                newRows.Add(Text.Dark("Ambiguous:  ").White($"{ambBranches}"));
            }
            else
            {
                newRows.Add(Text.Dark("Branch:     ").Color(color, branchName));
            }
        }

        if (commit.Author != "")
        {
            newRows.Add(Text.Dark("Author:     ").White($"{commit.Author}").Dark(", time: ").White(commit.AuthorTime.IsoZone()));
        }

        newRows.Add(Text.Dark("Children:   ").White(string.Join(", ", commit.AllChildIds.Select(id =>
            id == Repo.UncommittedId ? "" : id.Sid()))));
        newRows.Add(Text.Dark("Parents:    ").White(string.Join(", ", commit.ParentIds.Select(id =>
            id.Sid()))));
        if (commit.IsAhead)
        {
            newRows.Add(Text.Dark("Remote:   ").Green("▲ pushable"));
        }
        if (commit.IsBehind)
        {
            newRows.Add(Text.Dark("Remote:   ").Blue("▼ pullable"));
        }
        if (commit.Tags.Any())
        {
            var tagText = "[" + string.Join("][", commit.Tags.Select(t => t.Name)) + "]";
            newRows.Add(Text.Dark("Tags:       ").Green(tagText));
        }
        var tips = repo.ViewBranches.Where(b => b.TipId == commit.Id);
        if (tips.Any())
        {
            var tipText = new TextBuilder();
            tips.ForEach(t => tipText.Color(branchColorService.GetColor(repo, t), $"({t.Name})"));
            newRows.Add(Text.Dark("Tips:       ").Add(tipText));
        }
        newRows.AddRange(commit.Message.Split('\n').Select(l => Text.White(l).ToText()));
        newRows.Add(Text.Black(""));

        rows = newRows;
        contentView!.SetNeedsDisplay();
        contentView!.MoveToTop();
    });


    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width) =>
        (rows.Skip(firstIndex).Take(count), rows.Count);
}

