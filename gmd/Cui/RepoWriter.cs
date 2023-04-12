using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

interface IRepoWriter
{
    IEnumerable<Text> ToPage(IRepo repo, int firstRow, int rowCount, int currentIndex, int width);
    bool IShowSid { get; set; }
}

class RepoWriter : IRepoWriter
{
    static readonly int maxTipNameLength = 16;
    // static readonly int maxTipsLength = 41;
    const int markersWidth = 3; //  1 current marker and 1 ahead/behind and one space

    readonly IBranchColorService branchColorService;
    readonly IGraphWriter graphWriter;

    record Columns(int Subject, int Sid, int Author, int Time, int GraphWidth);

    public RepoWriter(IBranchColorService branchColorService, IGraphWriter graphWriter)
    {
        this.branchColorService = branchColorService;
        this.graphWriter = graphWriter;
    }

    public bool IShowSid { get; set; } = false;


    public IEnumerable<Text> ToPage(IRepo repo, int firstRow, int count, int currentIndex, int width)
    {
        List<Text> rows = new List<Text>();
        var branchTips = GetBranchTips(repo);

        var crc = repo.RowCommit;
        var crb = repo.Branch(crc.BranchName);
        var isUncommitted = !repo.Status.IsOk;

        Columns cw = ColumnWidths(repo, width);

        for (int i = firstRow; i < firstRow + count; i++)
        {
            Text text = Text.New;
            var c = repo.Commits[i];
            var graphRow = repo.Graph.GetRow(i);

            WriteGraph(text, graphRow, cw.GraphWidth);
            WriteCurrentMarker(text, c, isUncommitted);
            WriteAheadBehindMarker(text, c);
            WriteSubjectColumn(text, cw, c, crb, branchTips, i == currentIndex);
            WriteSid(text, cw, c);
            WriteAuthor(text, cw, c);
            WriteTime(text, cw, c);

            rows.Add(text);
        }

        return rows;
    }

    Columns ColumnWidths(IRepo repo, int width)
    {
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

        int subjectWidth = commitWidth - sidWidth - authorWidth - timeWidth;
        if (subjectWidth < 0)
        {
            subjectWidth = 0;
        }

        return new Columns(subjectWidth, sidWidth, authorWidth, timeWidth, graphWidth);
    }


    void WriteGraph(Text text, GraphRow graphRow, int maxGraphWidth)
    {
        text.Add(graphWriter.ToText(graphRow, maxGraphWidth));
    }


    void WriteCurrentMarker(Text text, Commit c, bool isUncommitted)
    {
        if (c.IsCurrent && !isUncommitted)
        {   // No uncommitted changes, so the is shown at the current commit
            text.White(" ●");
            return;
        }
        if (c.Id == Repo.UncommittedId)
        {   // There are uncommitted changes, so the current marker is at the uncommitted commit
            text.White(" ●");
            return;
        }

        text.Black("  ");
    }


    void WriteAheadBehindMarker(Text text, Commit c)
    {
        if (c.IsAhead)
        {
            text.BrightGreen("▲");
            return;
        }
        if (c.IsBehind)
        {
            text.BrightBlue("▼");
            return;
        }

        text.Black(" ");
    }

    void WriteSubjectColumn(Text text, Columns cw, Commit c, Branch currentRowBranch,
        IReadOnlyDictionary<string, Text> branchTips, bool isCurrent)
    {
        Text subjectText = GetSubjectText(c, isCurrent, currentRowBranch);
        Text tagsText = GetTagsText(c);
        Text tipsText = GetTipsText(c, branchTips);

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
            {   // Just shorten the subject a bit (will show enaugh)
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

        // Add a 'Branch set by user mark to subject if needed
        if (c.IsBranchSetByUser) { subjectWidth -= 2; }
        WriteSubText(text, subjectText, subjectWidth);
        if (c.IsBranchSetByUser) { text.White(" ╠"); }

        WriteSubText(text, tipsText, tipsWidth);
        WriteSubText(text, tagsText, tagsWidth);
    }

    void WriteSubText(Text text, Text subText, int maxWidth)
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

    Text GetSubjectText(Commit c, bool isCurrent, Branch currentRowBranch)
    {
        Text text = Text.New;
        if (c.IsUncommitted && isCurrent) { text.YellowSelected(c.Subject); }
        else if (isCurrent) { text.WhiteSelected(c.Subject); }
        else if (c.IsConflicted) { text.BrightRed(c.Subject); }
        else if (c.IsUncommitted) { text.BrightYellow(c.Subject); }
        else if (c.IsAhead) { text.BrightGreen(c.Subject); }
        else if (c.IsBehind) { text.BrightBlue(c.Subject); }
        else if (c.Id == Repo.PartialLogCommitID) { text.Dark(c.Subject); }
        else if (c.BranchCommonName == currentRowBranch.CommonName) { text.White(c.Subject); }
        else { text.Dark(c.Subject); }

        return text;
    }

    Text GetTagsText(Commit c) => c.Tags.Any()
        ? Text.New.Green($" [{string.Join("][", c.Tags.Select(t => t.Name))}]") : Text.New;

    Text GetTipsText(Commit c, IReadOnlyDictionary<string, Text> branchTips) =>
        branchTips.ContainsKey(c.Id) ? Text.New.Black(" ").Add(branchTips[c.Id]) : Text.New;


    void WriteSid(Text text, Columns cw, Commit c)
    {
        if (c.IsUncommitted)
        {
            text.Black(Txt("       ", cw.Sid));
            return;
        }
        text.Dark(Txt(" " + c.Sid, cw.Sid));
    }

    void WriteAuthor(Text text, Columns cw, Commit c)
    {
        text.Dark(Txt(" " + c.Author, cw.Author));
    }

    void WriteTime(Text text, Columns cw, Commit c)
    {
        text.Dark(Txt(" " + c.AuthorTime.ToString("yy-MM-dd HH:mm"), cw.Time));
    }

    string Txt(string text, int width)
    {
        if (text.Length <= width)
        {
            return text + new string(' ', width - text.Length);
        }

        return text.Substring(0, width);
    }


    IReadOnlyDictionary<string, Text> GetBranchTips(IRepo repo)
    {
        var branchTips = new Dictionary<string, Text>();

        foreach (var b in repo.Branches)
        {
            if (!branchTips.TryGetValue(b.TipId, out var tipText))
            {   //  Commit has no tip yet, crating tip text
                tipText = Text.New;
            }

            if (b.AmbiguousTipId != "")
            {   // The branch has an ambigous tip id as well, add that
                if (!branchTips.TryGetValue(b.AmbiguousTipId, out var ambiguousTipText))
                {
                    ambiguousTipText = Text.New;
                }

                ambiguousTipText.White("(~").Dark("ambiguous").White(")");
                branchTips[b.AmbiguousTipId] = ambiguousTipText;
            }

            string branchName = ToShortBranchName(b);
            var color = branchColorService.GetColor(repo.Repo, b);

            if (b.IsGitBranch)
            {
                if (b.IsRemote)
                {
                    if (b.LocalName != "")
                    {
                        var local = repo.Branches.First(bb => bb.Name == b.LocalName);
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
                        {   // Remote and local on different commits, (local banch will add itself)
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
                        var remote = repo.Branches.First(bb => bb.Name == b.RemoteName);
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
            else if (b.PullMergeBranchName != "")
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

    string ToShortBranchName(Branch branch)
    {
        var name = branch.DisplayName;
        if (name.Length > maxTipNameLength)
        {   // Branch name to long, shorten it
            name = "┅" + name.Substring(name.Length - maxTipNameLength);
        }
        return name;
    }
}


