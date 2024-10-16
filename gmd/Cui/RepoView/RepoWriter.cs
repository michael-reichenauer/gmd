using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IRepoWriter
{
    IEnumerable<Text> ToPage(IViewRepo repo, int firstRow, int rowCount, int currentIndex,
        string hooverBranchName, int hooverIndex, int width, Selection selection);
    bool IShowSid { get; set; }
}

class RepoWriter : IRepoWriter
{
    const int markersWidth = 3; //  1 current marker and 1 ahead/behind and one space

    readonly IBranchColorService branchColorService;
    readonly IGraphWriter graphWriter;

    record Columns(int Subject, int Sid, int Author, int Time, int GraphWidth);

    public RepoWriter(IBranchColorService branchColorService, IGraphWriter graphWriter)
    {
        this.branchColorService = branchColorService;
        this.graphWriter = graphWriter;
    }

    public bool IShowSid { get; set; } = true;


    public IEnumerable<Text> ToPage(IViewRepo repo, int firstRow, int count, int currentIndex,
        string hooverBranchName, int hooverIndex, int width, Selection selection)
    {
        if (!repo.Repo.ViewCommits.Any() || count == 0) return new List<Text>();
        if (!selection.IsEmpty) currentIndex = selection.InitialIndex;
        count = Math.Min(count, repo.Repo.ViewCommits.Count - firstRow);
        currentIndex = Math.Min(currentIndex, repo.Repo.ViewCommits.Count - 1);
        currentIndex = Math.Max(0, currentIndex);

        List<Text> rows = new List<Text>();
        var branchTips = GetBranchTips(repo);

        var crc = repo.Repo.ViewCommits[currentIndex];
        var crb = repo.Repo.BranchByName[crc.BranchName];
        var isUncommitted = !repo.Repo.Status.IsOk;
        var isBranchDetached = crb.IsDetached;
        Columns cw = ColumnWidths(repo, width);

        // Branch? prevBranch = null;
        for (int i = firstRow; i < firstRow + count; i++)
        {
            var c = repo.Repo.ViewCommits[i];

            var isSelected = !selection.IsEmpty && i >= selection.I1 && i <= selection.I2;

            // Build row
            var graphText = new TextBuilder();
            WriteGraph(graphText, repo.Graph, i, cw.GraphWidth, hooverBranchName, i == hooverIndex);
            WriteBlankOrStash(graphText, c);
            WriteCurrentMarker(graphText, c, isUncommitted, isBranchDetached, isSelected);
            WriteAheadBehindMarker(graphText, c);

            var text = new TextBuilder();
            WriteSubjectColumn(text, cw, c, crb, branchTips);
            WriteSid(text, cw, c);
            WriteAuthor(text, cw, c);
            WriteTime(text, cw, c);
            // if (i == hooverIndex && hooverBranchName == "") text.Highlight(); // hoover commit
            if (i == currentIndex && hooverBranchName == "") text.Highlight();      // current commit
            if (isSelected && c.BranchPrimaryName == crb.PrimaryName) text.Select();  // Selected commit

            rows.Add(graphText.Add(text));
        }

        return rows;
    }

    Columns ColumnWidths(IViewRepo repo, int width)
    {
        width++;
        int graphWidth = Math.Max(0, Math.Min(repo.Graph.Width, width - 20));

        // Normal columns when content width is wide enough
        int commitWidth = width - (graphWidth + markersWidth);
        int authorWidth = 15;
        int timeWidth = 15;
        int sidWidth = IShowSid ? 7 : 0;

        if (commitWidth < 70)
        {   // Disabled sid, author and time if very narrow view  
            sidWidth = 0;
            authorWidth = 0;
            timeWidth = 0;
        }
        else if (commitWidth < 100)
        {   // Reducing sid, author and and time if narrow view
            sidWidth = 0;
            authorWidth = 10;
            timeWidth = 9;
        }
        else if (commitWidth < 110)
        {   // Reducing  author and and time if narrow view
            authorWidth = 10;
            timeWidth = 9;
        }

        int subjectWidth = commitWidth - sidWidth - authorWidth - timeWidth;
        if (subjectWidth < 0)
        {
            subjectWidth = 0;
        }

        return new Columns(subjectWidth, sidWidth, authorWidth, timeWidth, graphWidth);
    }


    void WriteGraph(TextBuilder text, Graph graph, int index, int maxGraphWidth,
        string highlightBranchName, bool isHoverIndex)
    {
        text.Add(graphWriter.ToText(graph, index, maxGraphWidth, highlightBranchName, isHoverIndex));
    }

    static void WriteBlankOrStash(TextBuilder text, Commit c)
    {
        if (c.HasStash)
        {
            text.White("ß");
            return;
        }

        text.Black(" ");
    }

    static void WriteCurrentMarker(TextBuilder text, Commit c, bool isUncommitted, bool isBranchDetached, bool isSelected)
    {
        if (isSelected)
        {
            text.White("|");
            return;
        }
        if (c.IsDetached && c.IsCurrent && !isUncommitted || c.Id == Repo.UncommittedId && isBranchDetached)
        {   // Detached head, so the is shown at the current commit
            text.White("*");
            return;
        }
        if (c.IsCurrent)
        {   // No uncommitted changes, so the is shown at the current commit
            text.White("●");
            return;
        }

        text.Black(" ");
    }

    static void WriteAheadBehindMarker(TextBuilder text, Commit c)
    {
        if (c.IsAhead) text.BrightGreen("▲");
        else if (c.IsBehind) text.BrightBlue("▼");
        else if (c.IsUncommitted) text.Yellow("©");
        else text.Black(" ");
    }

    static void WriteSubjectColumn(TextBuilder text, Columns cw, Commit c, Branch currentRowBranch,
        IReadOnlyDictionary<string, TextBuilder> branchTips)
    {
        Text subjectText = GetSubjectText(c, currentRowBranch);
        Text tagsText = GetTagsText(c);
        var tipsText = GetTipsText(c, branchTips);

        int columnWidth = cw.Subject;

        int subjectWidth = columnWidth;
        int tipsWidth = 0;
        int tagsWidth = 0;

        // Calculate subject, tips and tags widths
        if (columnWidth > 5 + 3 + 3)
        {   // There is room for subject, tags and tips, check if shorten is needed 
            if (subjectText.Length + tagsText.Length + tipsText.Length <= columnWidth)
            {   // No need to shorten any of them, all fit
                tagsWidth = tagsText.Length;
                tipsWidth = tipsText.Length;
                subjectWidth = columnWidth - tagsWidth - tipsWidth;
            }
            else if (Math.Min(subjectText.Length, 25) + tagsText.Length + tipsText.Length <= columnWidth)
            {   // Just shorten the subject a bit (will show enough)
                tagsWidth = tagsText.Length;
                tipsWidth = tipsText.Length;
                subjectWidth = columnWidth - tagsWidth - tipsWidth;
            }
            else
            {   // Need to shorten tags and tips a bit too, but give tags and tips at least each 3 chars and max 20
                subjectWidth = Math.Min(columnWidth - 3 - 3,
                    columnWidth - Math.Min(tagsWidth + tipsWidth, 20));
                int rest = (columnWidth - subjectWidth) / 2;
                tagsWidth = Math.Min(tagsText.Length, columnWidth - subjectWidth - rest);
                tipsWidth = columnWidth - subjectWidth - tagsWidth;
            }
        }

        WriteSubText(text, subjectText, subjectWidth);
        WriteSubText(text, tipsText, tipsWidth);
        WriteSubText(text, tagsText, tagsWidth);
    }

    static void WriteSubText(TextBuilder text, Text subText, int maxWidth)
    {
        if (maxWidth == 0)
        {
            return;
        }
        if (subText.Length <= maxWidth)
        {
            text.Add(subText.Subtext(0, maxWidth, true));
            return;
        }

        // Shorten and add a '┅' char
        text.Add(subText.Subtext(0, maxWidth - 1, true)).Dark("┅");
    }

    static Text GetSubjectText(Commit c, Branch currentRowBranch)
    {
        var text = new TextBuilder();

        if (c.Id == Repo.EmptyRepoCommitId) { text.Dark(c.Subject); }
        else if (c.IsConflicted) { text.BrightRed(c.Subject); }
        else if (c.IsUncommitted) { text.BrightYellow(c.Subject); }
        else if (c.IsAhead) { text.BrightGreen(c.Subject); }
        else if (c.IsBehind) { text.BrightBlue(c.Subject); }
        else if (c.Id == Repo.TruncatedLogCommitId) { text.Dark(c.Subject); }
        else if (c.BranchPrimaryName == currentRowBranch.PrimaryName) { text.White(c.Subject); }
        else { text.Dark(c.Subject); }

        return text;
    }

    static Text GetTagsText(Commit c) => c.Tags.Any()
        ? Text.Green($"[{string.Join("][", c.Tags.Select(t => t.Name))}]") : Text.Empty;

    static Text GetTipsText(Commit c, IReadOnlyDictionary<string, TextBuilder> branchTips) =>
        branchTips.ContainsKey(c.Id) ? Text.Add(branchTips[c.Id]) : Text.Empty;

    static void WriteSid(TextBuilder text, Columns cw, Commit c)
    {
        if (c.IsUncommitted)
        {
            text.Black(Txt("       ", cw.Sid));
            return;
        }
        text.Cyan(Txt(" " + c.Sid, cw.Sid));
    }

    static void WriteAuthor(TextBuilder text, Columns cw, Commit c)
    {
        var txt = Txt(" " + c.Author, cw.Author);
        text.Dark(txt);
    }

    static void WriteTime(TextBuilder text, Columns cw, Commit c)
    {
        var txt = Txt(" " + c.AuthorTime.ToString("yy-MM-dd HH:mm"), cw.Time);
        text.Dark(txt);
    }

    static string Txt(string text, int width)
    {
        if (text.Length <= width)
        {
            return text + new string(' ', width - text.Length);
        }

        return text[..width];
    }


    IReadOnlyDictionary<string, TextBuilder> GetBranchTips(IViewRepo repo)
    {
        var branchTips = new Dictionary<string, TextBuilder>();

        foreach (var b in repo.Repo.ViewBranches)
        {
            if (!branchTips.TryGetValue(b.TipId, out var tipText))
            {   //  Commit has no tip yet, crating tip text
                tipText = new TextBuilder();
            }

            if (b.AmbiguousTipId != "")
            {   // The branch has an ambiguous tip id as well, add that
                if (!branchTips.TryGetValue(b.AmbiguousTipId, out var ambiguousTipText))
                {
                    ambiguousTipText = new TextBuilder();
                }

                ambiguousTipText.White("(~").Dark("ambiguous").White(")");
                branchTips[b.AmbiguousTipId] = ambiguousTipText;
            }

            string branchName = b.ShortNiceUniqueName();
            var color = branchColorService.GetColor(repo.Repo, b);

            if (b.IsGitBranch)
            {
                if (b.IsRemote)
                {
                    if (b.LocalName != "")
                    {
                        var local = repo.Repo.ViewBranches.First(bb => bb.Name == b.LocalName);
                        if (local.TipId == b.TipId)
                        {
                            // Both local and remote tips on same commit, combine them
                            if (local.IsCurrent)
                            {
                                tipText.Color(color, $"(^)(").White("● ").Color(color, $"{branchName})");
                            }
                            else
                            {
                                tipText.Color(color, $"(^)({branchName})");
                            }
                        }
                        else
                        {   // Remote and local on different commits, (local branch will add itself)
                            tipText.Color(color, $"(^/{branchName})");
                        }
                    }
                    else
                    {   // Only remote, no local branch
                        tipText.Color(color, $"(^/{branchName})");
                    }
                }
                if (!b.IsRemote)
                {
                    if (b.RemoteName != "")
                    {
                        var remote = repo.Repo.ViewBranches.First(bb => bb.Name == b.RemoteName);
                        if (remote.TipId == b.TipId)
                        {   // Both local and remote tips on same commit, handled by the remote branch
                            continue;
                        }
                        else
                        {   // Local branch on different commit as remote, remote will add itself
                            if (b.IsCurrent)
                            {
                                tipText.Color(color, $"(").White("● ").Color(color, $"{branchName})");
                            }
                            else
                            {
                                tipText.Color(color, $"({branchName})");
                            }
                        }
                    }
                    else
                    {   // Only local branch (no remote branch)
                        if (b.IsCurrent)
                        {
                            tipText.Color(color, $"(").White("● ").Color(color, $"{branchName})");
                        }
                        else
                        {
                            tipText.Color(color, $"({branchName})");
                        }
                    }
                }
            }
            else if (b.PullMergeParentBranchName != "")
            {
                tipText.Color(color, "(").Dark(branchName).Color(color, ")");
            }
            else
            {
                tipText.Color(color, "(~").Dark(branchName).Color(color, ")");
            }

            branchTips[b.TipId] = tipText;
        }

        return branchTips;
    }
}


