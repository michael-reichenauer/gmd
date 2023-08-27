using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;


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
    Resolve = 2048        // Φ
}



interface IGraphCreater
{
    Graph Create(Server.Repo repo);
}


class GraphCreater : IGraphCreater
{
    static readonly Color MoreColor = Color.Dark;
    readonly IBranchColorService branchColorService;

    public GraphCreater(IBranchColorService branchColorService)
    {
        this.branchColorService = branchColorService;
    }

    public Graph Create(Repo repo)
    {
        var branches = ToGraphBranches(repo);
        SetBranchesColor(repo, branches);
        SetBranchesXLocation(branches, repo.Filter != "");

        // The width is the max branch X room for 'more' branch in/out signs
        int maxBranchX = branches.Any() ? branches.Max(b => b.X) : 0;

        Sorter.Sort(branches, (b1, b2) => b1.X < b2.X ? -1 : b1.X > b2.X ? 1 : 0);
        Graph graph = new Graph(maxBranchX, repo.ViewCommits.Count, branches);
        SetGraph(graph, repo, branches);
        return graph;
    }

    public static bool IsOverlapping(GraphBranch b1, GraphBranch b2, int margin)
    {
        if (b2.B.Name == b1.B.Name ||       // Same branch
            b2.X != b1.X)                   // Not on the same column
        {
            return false;
        }

        int high1 = b1.HighIndex;
        int low1 = b1.LowIndex;
        int high2 = b2.HighIndex - margin;
        int low2 = b2.LowIndex + margin;

        return (high2 >= high1 && high2 <= low1) ||
            (low2 >= high1 && low2 <= low1) ||
            (high2 <= high1 && low2 >= low1);
    }


    void SetBranchesColor(Repo repo, IReadOnlyList<GraphBranch> branches)
    {
        branches.ForEach(b => b.Color = branchColorService.GetColor(repo, b.B));
    }

    static void SetGraph(Graph graph, Repo repo, IReadOnlyList<GraphBranch> branches)
    {
        foreach (var b in branches)
        {
            bool isAmbiguous = false; // Is set to true if commit is ambiguous, changes branch color

            for (int y = b.TipIndex; y <= b.BottomIndex; y++)
            {
                var c = repo.ViewCommits[y];

                if (c.IsAmbiguous && c.BranchName == b.B.Name)
                {
                    isAmbiguous = true;
                }

                if (c.BranchName != b.B.Name && c.Id == b.B.TipId)
                {   // this tip commit is a tip                     ─┺  (multiple tips on commit)
                    DrawOtherBranchTip(graph, b, c);
                    continue;
                }

                DrawBranch(graph, b, c, isAmbiguous); // Drawing either ┏  ┣ ┃ ┗

                if (c.BranchName != b.B.Name)
                {   // Not current branch
                    continue;
                }

                if (c.ParentIds.Count > 1)
                {   // Merge commit                     Drawing         ╭ or  ╮
                    DrawMerge(graph, repo, c, b);
                }

                if (repo.Filter == "" && null != c.AllChildIds.FirstOrDefault(id => !repo.CommitById.ContainsKey(id)))
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
    static void DrawOtherBranchTip(Graph graph, GraphBranch b, Commit c)
    {
        var commitBranch = graph.BranchByName(c.BranchName);
        Color color = b.Color;

        int x1 = commitBranch.X;
        int x2 = b.X;
        int y2 = c.ViewIndex;

        // this tip commit is not part of the branch (multiple branch tips on the same commit)
        graph.DrawHorizontalLine(x1 + 1, x2 + 1, y2, color);  //   ─

        if (c.IsAmbiguous) color = Color.White;

        graph.SetGraphBranch(x2, y2, Sign.Bottom | Sign.Pass, color, b); //       ┺
    }

    static void DrawBranch(Graph graph, GraphBranch b, Server.Commit c, bool isAmbiguous)
    {
        int x = b.X;
        int y = c.ViewIndex;
        Color color = c.IsAmbiguous ? Color.White : b.Color;

        if (c.BranchName != b.B.Name && c.Id != b.B.TipId)
        {   // Other branch commit, normal branch line (no commit on that branch)
            Color otherColor = !isAmbiguous ? b.Color : Color.White;
            graph.SetGraphBranch(x, y, Sign.BLine, otherColor, b); //      ┃  (other branch, not this commit)
            return;
        }

        if (c.BranchName != b.B.Name)
        {   // Not current branch (empty/blank sign)
            return;
        }

        if (c.IsBranchSetByUser)
        {
            graph.SetGraphBranch(x, y, Sign.Resolve, Color.White, b); //       Φ   (Resolved/set by user)
            return;
        }
        if (c.Id == b.B.TipId)
        {
            graph.SetGraphBranch(x, y, Sign.Tip, color, b); //       ┏   (branch tip)
        }
        if (c.Id == b.B.TipId && b.B.IsGitBranch)
        {
            graph.SetGraphBranch(x, y, Sign.ActiveTip, color, b); // ┣   (indicate possible more commits in the future)
        }
        if (c.Id == b.B.BottomId)
        {
            graph.SetGraphBranch(x, y, Sign.Bottom, color, b); //    ┗   (bottom commit (e.g. initial commit on main)
        }
        if (c.Id != b.B.TipId && c.Id != b.B.BottomId)
        {
            graph.SetGraphBranch(x, y, Sign.Commit, color, b); //    ┣   (normal commit, in the middle)
        }
    }

    static void DrawMerge(Graph graph, Repo repo, Commit commit, GraphBranch commitBranch)
    {
        var mergeParent = repo.CommitById[commit.ParentIds[1]];
        if (mergeParent.IsInView)
        {
            var parentBranch = graph.BranchByName(mergeParent.BranchName);
            // Commit is a merge commit, has 2 parents
            if (parentBranch.X < commitBranch.X)
            {   // Other branch is on the left side, merged from parent branch ╭
                DrawMergeFromParentBranch(graph, commit, commitBranch, mergeParent, parentBranch);
            }
            else if (parentBranch.X == commitBranch.X)
            {   // Other branch is on the same column, merged from sibling branch │
                DrawMergeFromSiblingBranch(graph, commit, commitBranch, mergeParent, parentBranch);
            }
            else
            {
                // Other branch is on the right side, merged from child branch,       ╮
                DrawMergeFromChildBranch(graph, commit, commitBranch, mergeParent, parentBranch);
            }
        }
        else if (repo.Filter == "")
        {   // Drawing a more  ╮
            DrawMoreMergeIn(graph, commit, commitBranch);
        }
    }

    private static void DrawMoreMergeIn(Graph graph, Commit commit, GraphBranch commitBranch)
    {
        // Drawing a more marker  ╮
        int x = commitBranch.X;
        int y = commit.ViewIndex;
        graph.SetMoreGraphConnect(x + 1, y, Sign.MergeFromRight, MoreColor);  //   ╮     
    }

    static void DrawMoreBranchOut(Graph graph, Commit commit, GraphBranch commitBranch)
    {
        // Drawing a more marker  ╯
        int x = commitBranch.X;
        int y = commit.ViewIndex;
        graph.SetMoreGraphConnect(x + 1, y, Sign.BranchToRight, MoreColor);  //   ╯    
    }

    private static void DrawMergeFromParentBranch(Graph graph,
        Server.Commit commit, GraphBranch commitBranch,
        Server.Commit mergeParent, GraphBranch parentBranch)
    {
        int x = commitBranch.X;
        int y = commit.ViewIndex;
        int x2 = parentBranch.X;
        int y2 = mergeParent.ViewIndex;

        // Other branch is on the left side, merged from parent parent branch ╭
        Color color = commitBranch.Color;
        if (commit.IsAmbiguous)
        {
            color = Color.White;
        }

        graph.SetGraphBranch(x, y, Sign.MergeFromLeft, color, commitBranch); //     ╭
        graph.SetGraphConnect(x, y, Sign.MergeFromLeft, color);
        if (commitBranch != parentBranch)
        {
            graph.DrawVerticalLine(x, y + 1, y2, color); //                         │
        }
        graph.SetGraphConnect(x, y2, Sign.BranchToRight, color); //                 ╯
        graph.DrawHorizontalLine(x2 + 1, x, y2, color);            //            ──
    }

    private static void DrawMergeFromSiblingBranch(
        Graph graph,
        Commit commit, GraphBranch commitBranch,
        Commit mergeParent, GraphBranch parentBranch)
    {
        // Commit is a merge commit, has 2 parents
        int x = commitBranch.X;
        int y = commit.ViewIndex;
        int x2 = parentBranch.X;
        int y2 = mergeParent.ViewIndex;

        // Other branch is on same column merged from sibling branch,  │
        Color color = parentBranch.Color;

        if (mergeParent.IsAmbiguous)
        {
            color = Color.White;
        }

        if (commitBranch != parentBranch)
        {
            graph.SetGraphConnect(x + 1, y, Sign.MergeFromRight, color); //  ╮  
            graph.DrawVerticalLine(x + 1, y + 1, y2, color); //              │
            graph.SetGraphConnect(x2 + 1, y2, Sign.BranchToRight, color); // ╯
        }
        else
        {
            graph.SetGraphBranch(x2, y2, Sign.Commit, color, parentBranch); //  ┣
        }
    }

    private static void DrawMergeFromChildBranch(
        Graph graph,
        Commit commit, GraphBranch commitBranch,
        Commit mergeParent, GraphBranch parentBranch)
    {
        // Commit is a merge commit, has 2 parents
        int x = commitBranch.X;
        int y = commit.ViewIndex;
        int x2 = parentBranch.X;
        int y2 = mergeParent.ViewIndex;

        // Other branch is on the right side, merged from child branch,  ╮
        Color color = parentBranch.Color;

        if (mergeParent.IsAmbiguous)
        {
            color = Color.White;
        }
        graph.DrawHorizontalLine(x + 1, x2, y, color); //                 ─

        if (commitBranch != parentBranch)
        {
            graph.SetGraphConnect(x2, y, Sign.MergeFromRight, color); //                 ╮
            graph.DrawVerticalLine(x2, y + 1, y2, color); //                             │
            graph.SetGraphBranch(x2, y2, Sign.BranchToLeft, color, parentBranch); //     ╰
            graph.SetGraphConnect(x2, y2, Sign.BranchToLeft, color);
        }
        else
        {
            graph.SetGraphBranch(x2, y2, Sign.Commit, color, parentBranch); //           ┣
        }
    }

    static void DrawBranchFromParent(Graph graph, Repo repo, Commit c, GraphBranch commitBranch)
    {
        // Commit parent is on other branch (commit is first/bottom commit on this branch)
        // Branched from parent branch
        int x = commitBranch.X;
        int y = c.ViewIndex;
        var parent = repo.CommitById[c.ParentIds[0]];
        if (!parent.IsInView) return;  // parent filtered out, skip 

        var parentBranch = graph.BranchByName(parent.BranchName);
        int x2 = parentBranch.X;
        int y2 = parent.ViewIndex;
        Color color = commitBranch.Color;

        if (c.IsAmbiguous)
        {
            color = Color.White;
        }

        if (parentBranch.X < commitBranch.X)
        {   // Other branch is left side  ╭
            graph.SetGraphBranch(x, y, Sign.MergeFromLeft, color, commitBranch); // ╭
            graph.SetGraphConnect(x, y, Sign.MergeFromLeft, color);  //      ╭
            graph.DrawVerticalLine(x, y + 1, y2, color);              //     │
            graph.SetGraphConnect(x, y2, Sign.BranchToRight, color); //      ╯
            graph.DrawHorizontalLine(x2 + 1, x, y2, color);           //  ──
        }
        else
        {   // Other branch is right side, branched from some child branch ╮ 
            graph.SetGraphBranch(x, y, Sign.MergeFromRight, color, commitBranch); // ╮
            graph.SetGraphConnect(x + 1, y, Sign.MergeFromRight, color); // ╮
            graph.DrawVerticalLine(x + 1, y + 1, y2, color);             // │
            graph.SetGraphConnect(x + 1, y2, Sign.BranchToLeft, color);  // ╰
            graph.DrawHorizontalLine(x + 1, x2 + 1, y2, color);          //  ──
        }
    }


    // Returns a list of branches, with Y location set for tip and bottom commits
    static List<GraphBranch> ToGraphBranches(Repo repo)
    {
        if (repo.Filter != "") return ToFilteredGraphBranches(repo);

        List<GraphBranch> branches = repo.ViewBranches.Select((b, i) => new GraphBranch(b, i)).ToList();
        Dictionary<string, GraphBranch> branchMap = new Dictionary<string, GraphBranch>();
        foreach (var b in branches)
        {
            branchMap[b.B.Name] = b;
            b.TipIndex = repo.CommitById[b.B.TipId].ViewIndex;
            b.BottomIndex = repo.CommitById[b.B.BottomId].ViewIndex;
            b.HighIndex = b.TipIndex;
            b.LowIndex = b.BottomIndex;

            if (b.B.ParentBranchName != "")
            {   // Set parent branch
                b.ParentBranch = branches.First(bb => bb.B.Name == b.B.ParentBranchName);
            }
        }

        foreach (var c in repo.ViewCommits)
        {
            // var branchName = c.BranchName;
            // var branch = branchMap[branchName];
            if (c.ParentIds.Count > 1)
            {   // commit is a merge commit, lets check if its merge parent needs to adjust
                var cp = repo.CommitById[c.ParentIds[1]];
                if (cp.IsInView)
                {
                    var branch = branchMap[cp.BranchName];
                    if (branch.HighIndex > c.ViewIndex) branch.HighIndex = c.ViewIndex;
                    if (branch.LowIndex < c.ViewIndex) branch.LowIndex = c.ViewIndex;
                }
            }

            if (c.ParentIds.Count == 1)
            {
                var cp = repo.CommitById[c.ParentIds[0]];
                if (cp.IsInView && cp.BranchName != c.BranchName)
                {   // Commit is a bottom id
                    var branch = branchMap[c.BranchName];
                    if (branch.LowIndex < cp.ViewIndex) branch.LowIndex = cp.ViewIndex;
                }
            }
        }

        return branches;
    }

    // Returns a list of branches, with Y location set for first and last commits of a branch.
    // But not all branches are included, only the ones that do have commits, e.g. 
    // ancestors and related branches might not have any commits and thus skipped.
    // Also, since not all commits are included, the tip and bottom commits might not exists,
    // so first and last existing commits are used instead.
    static List<GraphBranch> ToFilteredGraphBranches(Repo repo)
    {
        List<GraphBranch> branches = new List<GraphBranch>();
        Dictionary<string, FirstLast> firstLast = new Dictionary<string, FirstLast>();

        // Find first and last commits for each branch are located by iterating commits
        // and updating the first and last index for each branch for each commit
        for (int i = 0; i < repo.ViewCommits.Count; i++)
        {
            var c = repo.ViewCommits[i];

            // Update first and last index for all branch tips on the commit (except the current branch)
            // And the branch for the current commit
            c.BranchTips.Where(t => t != c.BranchName).Append(c.BranchName).ForEach(t =>
            {
                if (!firstLast.TryGetValue(t, out var tb))
                {   // First time branch is detected, update both first and last index
                    firstLast[t] = new FirstLast { FirstIndex = i, LastIndex = i };
                }
                else
                {   // Branch already detected, just update last index
                    tb.LastIndex = i;
                }
            });
        }

        // Create a GraphBranch for each branch, using the first and last index in previous step
        // Skipping branches, which did not have own commits, e.g. ancestors or related branches
        for (var i = 0; i < repo.ViewBranches.Count; i++)
        {
            var b = repo.ViewBranches[i];

            if (!firstLast.TryGetValue(b.Name, out var tb))
            {   // Skipping branches which do not have own commits, e.g. parent or related branches
                continue;
            }

            var gb = new GraphBranch(repo.ViewBranches[i], i)
            {
                TipIndex = tb.FirstIndex,
                HighIndex = tb.FirstIndex,
                BottomIndex = tb.LastIndex,
                LowIndex = tb.LastIndex,
            };
            if (gb.B.ParentBranchName != "")
            {
                gb.ParentBranch = branches.FirstOrDefault(b => b.B.Name == gb.B.ParentBranchName);
            }

            branches.Add(gb);
        }

        return branches;
    }


    // Sets the X location for each branch, ensuring that branches do not overlap on the same X location
    // So e.g. Children must be to the right of their parent. But  siblings can share same X location,
    // if they do not overlap.
    static void SetBranchesXLocation(IReadOnlyList<GraphBranch> branches, bool isFilterRepo)
    {
        // Iterating in the order of the view repo branches, Skipping main/master branch
        for (int i = 1; i < branches.Count; i++)
        {
            var b = branches[i];
            b.X = 0;

            // Ensure parent branches are to the left of child branches
            if (b.ParentBranch != null)
            {
                if (b.B.Name == b.ParentBranch.B.LocalName)
                {   // The local branch of a remote branch
                    b.X = b.ParentBranch.X + 1;
                }
                else if (b.B.PullMergeParentBranchName == b.ParentBranch.B.Name)
                {   // The pull merger sub part of a branch
                    b.X = b.ParentBranch.X + 1;
                }
                else
                {   // Some other child branch
                    if (b.ParentBranch.B.LocalName != "")
                    {   // The parent has a local branch, so this branch should be to the right local
                        b.X = b.ParentBranch.X + 2;
                    }
                    else
                    {   // The parent has no local branch, so this branch should be to the right of the parent
                        b.X = b.ParentBranch.X + 1;
                    }
                }
            }

            // Ensure that siblings do not overlap (with a little margin if filter repo)
            var margin = isFilterRepo ? 1 : 0;
            while (true)
            {
                if (null == branches.FirstOrDefault(v => IsOverlapping(v, b, margin)) &&
                    null == branches.FirstOrDefault(v => IsOverlapping(b, v, margin)))
                {   // Found a free spot for the branch
                    break;
                }

                b.X++;
            }
        }
        // var mainBranches = branches.Where(b => b.B.IsPrimary || b.B.RemoteName != "").OrderBy(b => b.X).ToList();
        // Log.Info($"Ordered {mainBranches.Count} main branches:\n  {mainBranches.Select(b => $"{b.X,2} {b.B.Name}").Join("\n  ")}");
    }

    class FirstLast
    {
        public int FirstIndex { get; set; }
        public int LastIndex { get; set; }
    }
}
