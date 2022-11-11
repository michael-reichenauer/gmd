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
    private readonly IBranchColorService branchColorService;

    public GraphService(IBranchColorService branchColorService)
    {
        this.branchColorService = branchColorService;
    }

    public Graph CreateGraph(Repo repo)
    {
        var t = Timing.Start();
        var branches = ToGraphBranches(repo);
        SetBranchesColor(repo, branches);
        SetBranchesXLocation(branches);

        // The width is the max branch X +2 room for 'more' branch in/out signs
        int width = branches.Max(b => b.X) + 2;

        Graph graph = new Graph(width, repo.Commits.Count, branches);
        SetGraph(graph, repo, branches);

        Log.Debug($"{t}");
        return graph;
    }

    private void SetBranchesColor(Repo repo, IReadOnlyList<GraphBranch> branches)
    {
        branches.ForEach(b => b.Color = branchColorService.GetColor(repo, b.B));
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
                {   // Merge commit                     Drawing         ╭ or  ╮
                    DrawMerge(graph, repo, c, b);
                }

                if (c.ChildIds.Count > 1 &&
                    null != c.ChildIds.FirstOrDefault(id => !repo.CommitById.ContainsKey(id)))
                {
                    DrawMoreBranchOut(graph, c, b); // Drawing  ╯
                }

                if (c.ParentIds.Count > 0 && repo.CommitById[c.ParentIds[0]].BranchName != c.BranchName)
                {   // Commit parent is on other branch (i.e. commit is first/bottom commit on this branch)
                    // Draw branched from parent branch  ╯ or ╰
                    DrawBranchFromParent(graph, repo, c, b);
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

    void DrawMerge(Graph graph, Repo repo, Commit commit, GraphBranch commitBranch)
    {
        if (repo.CommitById.TryGetValue(commit.ParentIds[1], out var mergeParent))
        {
            var parentBranch = graph.BranchByName(mergeParent.BranchName);
            // Commit is a merge commit, has 2 parents
            if (parentBranch.Index < commitBranch.Index)
            {   // Other branch is on the left side, merged from parent parent branch ╭
                DrawMergeFromParentBranch(graph, repo, commit, commitBranch, mergeParent, parentBranch);
            }
            else
            {
                // Other branch is on the right side, merged from child branch,       ╮
                DrawMergeFromChildBranch(graph, repo, commit, commitBranch, mergeParent, parentBranch);
            }
        }
        else
        {
            // Drawing a dark  ╮
            int x = commitBranch.X;
            int y = commit.Index;
            Color color = Colors.DarkGray;
            graph.SetGraphConnect(x + 1, y, Sign.MergeFromRight, color);  //   ╮     
        }
    }

    void DrawMoreBranchOut(Graph graph, Commit commit, GraphBranch commitBranch)
    {
        // Drawing a dark   ╯
        int x = commitBranch.X;
        int y = commit.Index;
        Color color = Colors.DarkGray;
        graph.SetGraphConnect(x + 1, y, Sign.BranchToRight, color);  //   ╯    
    }

    private void DrawMergeFromParentBranch(Graph graph, Repo repo,
        Commit commit, GraphBranch commitBranch,
        Commit mergeParent, GraphBranch parentBranch)
    {
        int x = commitBranch.X;
        int y = commit.Index;
        int x2 = parentBranch.X;
        int y2 = mergeParent.Index;

        // Other branch is on the left side, merged from parent parent branch ╭
        Color color = commitBranch.Color;
        if (commit.IsAmbiguous)
        {
            color = Colors.Ambiguous;
        }

        graph.SetGraphBranch(x, y, Sign.MergeFromLeft, color); //     ╭
        graph.SetGraphConnect(x, y, Sign.MergeFromLeft, color);
        if (commitBranch != parentBranch)
        {
            graph.DrawVerticalLine(x, y + 1, y2, color); //             │
        }
        graph.SetGraphConnect(x, y2, Sign.BranchToRight, color); //   ╯
        graph.DrawHorizontalLine(x2 + 1, x, y2, color);            // ──
    }

    private void DrawMergeFromChildBranch(Graph graph, Repo repo,
    Commit commit, GraphBranch commitBranch,
    Commit mergeParent, GraphBranch parentBranch)
    {
        // Commit is a merge commit, has 2 parents
        int x = commitBranch.X;
        int y = commit.Index;
        int x2 = parentBranch.X;
        int y2 = mergeParent.Index;

        // Other branch is on the right side, merged from child branch,  ╮
        Color color = parentBranch.Color;

        if (mergeParent.IsAmbiguous)
        {
            color = Colors.Ambiguous;
        }
        graph.DrawHorizontalLine(x + 1, x2, y, color); //                 ─

        if (commitBranch != parentBranch)
        {
            graph.SetGraphConnect(x2, y, Sign.MergeFromRight, color); //   ╮
            graph.DrawVerticalLine(x2, y + 1, y2, color); //               │
            graph.SetGraphBranch(x2, y2, Sign.BranchToLeft, color); //     ╰
            graph.SetGraphConnect(x2, y2, Sign.BranchToLeft, color);
        }
        else
        {
            graph.SetGraphBranch(x2, y2, Sign.Commit, color); //           ┣
        }
    }

    void DrawBranchFromParent(Graph graph, Repo repo, Commit c, GraphBranch commitBranch)
    {
        // Commit parent is on other branch (commit is first/bottom commit on this branch)
        // Branched from parent branch
        int x = commitBranch.X;
        int y = c.Index;
        var parent = repo.CommitById[c.ParentIds[0]];
        var parentBranch = graph.BranchByName(parent.BranchName);
        int x2 = parentBranch.X;
        int y2 = parent.Index;
        Color color = commitBranch.Color;

        if (c.IsAmbiguous)
        {
            color = Colors.Ambiguous;
        }

        if (parentBranch.Index < commitBranch.Index)
        {   // Other branch is left side  ╭
            graph.SetGraphBranch(x, y, Sign.MergeFromLeft, color);
            graph.SetGraphConnect(x, y, Sign.MergeFromLeft, color);  //      ╭
            graph.DrawVerticalLine(x, y + 1, y2, color);              //     │
            graph.SetGraphConnect(x, y2, Sign.BranchToRight, color); //      ╯
            graph.DrawHorizontalLine(x2 + 1, x, y2, color);           //  ──
        }
        else
        {   // (is this still valid ????)
            // Other branch is right side, branched from some child branch ╮ 
            // graph.SetGraphConnect(x + 1, y, Sign.MergeFromRight, color); // ╮
            // graph.DrawVerticalLine(x + 1, y + 1, y2, color);             // │
            // graph.SetGraphBranch(x2, y2, Sign.BranchToLeft, color);      // ╰
            // graph.SetGraphConnect(x2, y2, Sign.BranchToLeft, color);
        }
    }


    IReadOnlyList<GraphBranch> ToGraphBranches(Repo repo)
    {
        IReadOnlyList<GraphBranch> branches = repo.Branches.Select((b, i) => new GraphBranch(b, i)).ToList();
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
