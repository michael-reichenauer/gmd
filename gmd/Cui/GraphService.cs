using gmd.ViewRepos;

using Color = Terminal.Gui.Attribute;


namespace gmd.Cui;

// https://code-maze.com/csharp-flags-attribute-for-enum/
[Flags]
enum Sign
{
    Blank = 0,            //

    // Branches
    Commit = 1,           // ┣
    BLine = 2,            // ┃
    Tip = 4,              // ┏
    Bottom = 8,           // ┗
    ActiveTip = 16,       // ┣

    // Connections
    Pass = 32,            // ─  (or ╂ for branch positions)
    MergeFromLeft = 64,   // ╭
    MergeFromRight = 128, // ╮
    BranchToLeft = 256,   // ╰
    BranchToRight = 512,  // ╯
    ConnectLine = 1024,   // │
}



interface IGraphService
{
    Graph CreateGraph(Repo repo);
}



class GraphService : IGraphService
{
    public Graph CreateGraph(Repo repo)
    {
        var branches = ToGraphBranches(repo);
        SetBranchesColor(branches);
        SetBranchesXLocation(branches);

        // The width is the max branch X + room for 'more' branch in/out signs
        int width = branches.Max(b => b.X) + 1;

        Graph graph = new Graph(width, repo.Commits.Count, branches);
        SetGraph(graph, repo, branches);

        return graph;
    }

    private void SetBranchesColor(IReadOnlyList<GraphBranch> branches)
    {
        branches.ForEach(b => b.Color = Colors.Magenta);
    }

    void SetGraph(Graph graph, Repo repo, IReadOnlyList<GraphBranch> branches)
    {
        foreach (var b in branches)
        {
            bool isAmbiguous = false; // Is set to true if commit is ambigous, changes branch color

            for (int y = b.TipIndex; y <= b.BottomIndex; y++)
            {
                var c = repo.Commits[y];
                if (c.BranchName == b.B.Name && c.IsAmbiguous)
                {
                    isAmbiguous = true;
                }

                if (c.BranchName != b.B.Name && c.Id == b.B.TipId)
                {   // this tip commit is a tip                     ─┺  (multiple tips on commit)
                    DrawOtherBranchTip(graph, repo, b, c);
                    continue;
                }

                DrawBranch(graph, repo, b, c, isAmbiguous); // Drawing either ┏  ┣ ┃ ┗

                if (c.BranchName != b.B.Name)
                {   // Not current branch
                    continue;
                }

                if (c.ParentIds.Count > 1)
                {  // Merge commit                     Drawing         ╭ or  ╮
                    DrawMerge(graph, repo, c, b);
                }
                // if c.More.Has(api.MoreBranchOut) { // ╯
                // 	t.drawMoreBranchOut(repo, c) // Drawing  ╮
                // }

                if (c.ParentIds.Count > 0 && repo.CommitById[c.ParentIds[0]].BranchName != c.BranchName)
                {   // Commit parent is on other branch (i.e. commit is first/bottom commit on this branch)
                    // Draw branched from parent branch  ╯ or ╰
                    DrawBranchFromParent(repo, c);
                }
            }
        }
    }

    // DrawOtherBranchTip draws  ─┺ in when multiple tips on same commit
    void DrawOtherBranchTip(Graph graph, Repo repo, GraphBranch b, Commit c)
    {
        int x = b.X;
        int y = c.Index;
        Color color = b.Color;
        var commitBranch = graph.BranchByName(c.BranchName);

        // this tip commit is not part of the branch (multiple branch tips on the same commit)
        graph.DrawHorizontalLine(commitBranch.X + 1, x + 1, y, color);  //   ─

        if (c.IsAmbiguous)
        {
            color = Colors.Ambiguous;
        }
        graph.SetGraphBranch(x, y, Sign.Bottom | Sign.Pass, color); //       ┺
    }

    void DrawBranch(Graph graph, Repo repo, GraphBranch b, Commit c, bool isAmbiguous)
    {
        int x = b.X;
        int y = c.Index;
        Color color = !isAmbiguous ? b.Color : Colors.Ambiguous;

        if (c.BranchName != b.B.Name && c.Id != b.B.TipId)
        {   // Other branch commit, normal branch line (no commit on that branch)
            graph.SetGraphBranch(x, y, Sign.BLine, color); //      ┃  (other branch, not this commit)
            return;
        }

        if (c.BranchName != b.B.Name)
        {   // Not current branch (empty/blank sign)
            return;
        }

        if (c.Id == b.B.TipId)
        {
            graph.SetGraphBranch(x, y, Sign.Tip, color); //       ┏   (branch tip)
        }
        if (c.Id == b.B.TipId && b.B.IsGitBranch)
        {
            graph.SetGraphBranch(x, y, Sign.ActiveTip, color); // ┣   (indicate possible more commits in the future)
        }
        if (c.Id == b.B.BottomId)
        {
            graph.SetGraphBranch(x, y, Sign.Bottom, color); //    ┗   (bottom commit (e.g. initial commit on main)
        }
        if (c.Id != b.B.TipId && c.Id != b.B.BottomId)
        {
            graph.SetGraphBranch(x, y, Sign.Commit, color); //    ┣   (normal commit, in the middle)
        }
    }

    void DrawMerge(Graph graph, Repo repo, Commit c, GraphBranch b)
    {
        if (repo.CommitById.TryGetValue(c.ParentIds[1], out var mergeParent))
        {

        }
        else
        {
            // Drawing a ╮
            int x = b.X;
            int y = c.Index;
            Color color = Colors.DarkGray;

            graph.SetGraphConnect(x + 1, y, Sign.MergeFromRight, color);  //   ╮     
        }
    }


    void DrawBranchFromParent(Repo repo, Commit c)
    {

    }


    IReadOnlyList<GraphBranch> ToGraphBranches(Repo repo)
    {
        IReadOnlyList<GraphBranch> branches = repo.Branches.Select(b => new GraphBranch(b)).ToList();
        Func<string, GraphBranch> branchByName = name => branches.First(b => b.B.Name == name);

        foreach (var b in branches)
        {
            b.TipIndex = repo.CommitById[b.B.TipId].Index;
            b.BottomIndex = repo.CommitById[b.B.BottomId].Index;
            if (b.B.ParentBranchName != "")
            {
                b.ParentBranch = branchByName(b.B.ParentBranchName);
            }
        }

        return branches;
    }

    void SetBranchesXLocation(IReadOnlyList<GraphBranch> branches)
    {
        // Iterating in the order of the view repo branches
        for (int i = 1; i < branches.Count; i++)
        {
            var b = branches[i];
            b.X = 0;

            // Ensure parent branches are to the left of child branches
            if (b.ParentBranch != null)
            {
                if (b.ParentBranch.B.LocalName != "" && b.ParentBranch.B.LocalName != b.B.Name)
                {
                    // Some other child branch
                    b.X = b.ParentBranch.X + 2;
                }
                else
                {
                    // The local branch of a remote branch
                    b.X = b.ParentBranch.X + 1;
                }
            }

            // Ensure that siblings do not overlap (with a little margin)
            while (true)
            {
                if (null == branches.FirstOrDefault(v =>
                    v.B.Name != b.B.Name && v.X == b.X &&
                    IsOverlapping(v.TipIndex, v.BottomIndex, b.TipIndex - 1, b.BottomIndex + 1)))
                {
                    break;
                }

                b.X++;
            }
        }
    }


    bool IsOverlapping(int top1, int bottom1, int top2, int bottom2)
    {
        return (top2 >= top1 && top2 <= bottom1) ||
            (bottom2 >= top1 && bottom2 <= bottom1) ||
            (top2 <= top1 && bottom2 >= bottom1);
    }
}
