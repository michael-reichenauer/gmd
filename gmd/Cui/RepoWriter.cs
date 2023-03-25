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
    static readonly int maxTipsLength = 41;
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
            WriteSubject(text, cw, c, crb, branchTips, i == currentIndex);
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

    void WriteSubject(Text text, Columns cw, Commit c, Branch currentRowBranch,
        IReadOnlyDictionary<string, Text> branchTips, bool isCurrent)
    {
        int width = cw.Subject;
        Text tagsText = Text.New;
        if (c.Tags.Any())
        {
            string tags = "";
            c.Tags.ForEach(t => tags += $"[{t.Name}]");
            tags = tags.Max(Math.Max(0, width - 10));
            tagsText.Green(tags);
            width = width - tags.Length;
        }

        int columnWidth = width;
        int maxTipWidth = Math.Min(maxTipsLength, columnWidth - 5);

        if (maxTipWidth < columnWidth - c.Subject.Length)
        {
            maxTipWidth = (columnWidth - c.Subject.Length);
        }

        if (branchTips.TryGetValue(c.Id, out var tips))
        {
            columnWidth -= Math.Min(tips.Length, maxTipWidth);
            columnWidth -= 1;
        }
        if (c.IsBranchSetByUser)
        {
            columnWidth -= 2;
        }

        string subject = Txt(c.Subject, columnWidth);

        if (c.IsUncommitted && isCurrent) { text.YellowSelected(subject); }
        else if (isCurrent) { text.WhiteSelected(subject); }
        else if (c.IsConflicted) { text.BrightRed(subject); }
        else if (c.IsUncommitted) { text.BrightYellow(subject); }
        else if (c.IsAhead) { text.BrightGreen(subject); }
        else if (c.IsBehind) { text.BrightBlue(subject); }
        else if (c.Id == Repo.PartialLogCommitID) { text.Dark(subject); }
        else if (c.BranchCommonName == currentRowBranch.CommonName)
        {
            text.White(subject);
        }
        else { text.Dark(subject); }
        text.Add(tagsText);

        if (tips != null) { WriteBranchTips(text, tips, maxTipWidth); }
        if (c.IsBranchSetByUser) { text.Dark(" Ф"); }
    }

    void WriteBranchTips(Text text, Text tips, int maxWidth)
    {
        if (tips.Length > maxWidth - 1)
        {
            maxWidth -= 2;
        }

        text.Black(" ");
        text.Add(tips.Subtext(0, maxWidth - 1));
        if (tips.Length > maxWidth - 1)
        {
            text.Dark("┅");
        }
    }

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

            string branchName = b.DisplayName;
            int splitIndex = branchName.LastIndexOf('/');
            if (splitIndex != -1)
            {
                branchName = "┅" + branchName.Substring(splitIndex);
            }

            if (branchName.Length > maxTipNameLength)
            {   // Branch name to long, shorten it
                branchName = branchName.Substring(branchName.Length - maxTipNameLength);
                branchName = "┅" + branchName;
            }

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
                                tipText.Color(color, $"(^").Dark("|").White("● ").Color(color, $"{branchName})");
                            }
                            else
                            {
                                tipText.Color(color, $"(^").Dark("|").Color(color, $"{branchName})");
                            }
                        }
                        else
                        {
                            tipText.Color(color, $"(^/{branchName})");
                        }
                    }
                    else
                    {
                        tipText.Color(color, $"(^/{branchName})");
                    }
                }
                if (!b.IsRemote)
                {
                    if (b.RemoteName != "")
                    {
                        var remote = repo.Branches.First(bb => bb.Name == b.RemoteName);
                        if (remote.TipId == b.TipId)
                        {
                            // Both local and remote tips on same commit, handled by the remote branch
                            continue;
                        }
                        else
                        {
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
                    {
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
}


