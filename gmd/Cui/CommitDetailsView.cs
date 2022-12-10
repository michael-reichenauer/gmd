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

    internal static readonly int ContentHeight = 8;
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
            IsNoCursor = true,
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

        this.commit = commit;
        var id = commit.Id;
        var color = branchColorService.GetColor(repo, branch);

        if (id == Repo.UncommittedId)
        {
            id = "Uncommitted";
        }

        var newRows = new List<Text>();

        if (commit.Id == Repo.UncommittedId)
        {
            newRows.Add(Text.New.Dark("Id:         ").BrightYellow(id));
        }
        else
        {
            newRows.Add(Text.New.Dark("Id:         ").White(id).Dark($"  (#{commit.GitIndex})"));
        }

        var branchName = branch.IsGitBranch ? branch.Name : "~" + branch.Name;
        if (branch.IsRemote)
        {
            branchName = "^origin/" + branchName;
        }

        if (commit.IsBranchSetByUser)
        {
            newRows.Add(Text.New.Dark("Branch:     ").Color(color, branchName + "   Ф").Dark(" (ambiguity resolved by user)"));
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
                newRows.Add(Text.New.Dark("Branch:     ").White(branchName + $" (ambiguous: {ambBranches})"));
            }
            else
            {
                newRows.Add(Text.New.Dark("Branch:     ").Color(color, branchName));
            }
        }

        newRows.Add(Text.New.Dark("Children:   ").White(string.Join(", ", commit.ChildIds.Select(id =>
            id == Repo.UncommittedId ? "" : id.Substring(0, 6)))));
        newRows.Add(Text.New.Dark("Parents:    ").White(string.Join(", ", commit.ParentIds.Select(id =>
            id.Substring(0, 6)))));
        if (commit.IsAhead)
        {
            newRows.Add(Text.New.Dark("Remote:   ").Green("▲ pushable"));
        }
        if (commit.IsBehind)
        {
            newRows.Add(Text.New.Dark("Remote:   ").Blue("▼ pullable"));
        }
        newRows.AddRange(commit.Message.Split('\n').Select(l => Text.New.White(l)));

        rows = newRows;
        contentView!.TriggerUpdateContent(rows.Count);
        contentView!.MoveToTop();
    });


    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width) =>
        rows.Skip(firstIndex).Take(count);
}

