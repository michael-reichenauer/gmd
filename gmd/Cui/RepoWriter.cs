using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IRepoWriter
{
    void WriteRepoPage(IRepo repo, int firstRow, int rowCount);
}

class RepoWriter : IRepoWriter
{
    static readonly int maxTipNameLength = 16;
    static readonly int maxTipsLength = 41;

    private readonly ColorText text;
    private readonly IBranchColorService branchColorService;
    private IGraphWriter graphWriter;

    record Columns(int Subject, int Sid, int Author, int Time);

    public RepoWriter(View view, int startX, IBranchColorService branchColorService)
    {
        this.branchColorService = branchColorService;
        this.text = new ColorText(view, startX);
        graphWriter = new GraphWriter(text);
    }

    public void WriteRepoPage(IRepo repo, int firstRow, int rowCount)
    {
        var branchTips = GetBranchTips(repo);

        var crc = repo.Repo.Commits[repo.CurrentIndex];
        var crb = repo.Repo.BranchByName[crc.BranchName];
        var isUncommitted = repo.HasUncommittedChanges; //repo.Repo.Status.IsOk;

        text.Reset();
        int graphWidth = repo.Graph.Width;
        int markersWidth = 3; //  1 current marker and 1 ahead/behind

        Columns cw = ColumnWidths(repo.ContentWidth - (graphWidth + markersWidth));

        for (int i = firstRow; i < firstRow + rowCount; i++)
        {
            var c = repo.Repo.Commits[i];
            var graphRow = repo.Graph.GetRow(i);
            WriteGraph(graphRow);
            WriteCurrentMarker(c, isUncommitted);
            WriteAheadBehindMarker(c);
            WriteSubject(cw, c, crb, branchTips);
            WriteSid(cw, c);
            WriteAuthor(cw, c);
            WriteTime(cw, c);
            text.EoL();
        }
    }


    void WriteGraph(GraphRow graphRow)
    {
        graphWriter.Write(graphRow);
    }

    void WriteCurrentMarker(Commit c, bool isUncommitted)
    {
        if (c.IsCurrent && !isUncommitted)
        {
            text.White(" ●");
            return;
        }
        if (c.Id == Repo.UncommittedId)
        {
            text.White(" ●");
            return;
        }

        text.White("  ");
    }

    void WriteAheadBehindMarker(Commit c)
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

        text.White(" ");
    }

    void WriteSubject(Columns cw, Commit c, Branch currentRowBranch,
        IReadOnlyDictionary<string, Text> branchTips)
    {
        int columnWidth = cw.Subject;
        int maxTipWidth = maxTipsLength;

        if (maxTipWidth < columnWidth - c.Subject.Length)
        {
            maxTipWidth = (columnWidth - c.Subject.Length);
        }

        if (branchTips.TryGetValue(c.Id, out var tips))
        {
            columnWidth -= (Math.Min(tips.Length, maxTipsLength));
            columnWidth -= 1;
            // if (tips.Length >= maxTipWidth)
            // {
            //     columnWidth -= 1;
            // }
        }

        string subject = Txt(c.Subject, columnWidth);

        if (c.IsConflicted) { text.BrightRed(subject); }
        else if (c.IsUncommitted) { text.BrightYellow(subject); }
        else if (c.IsAhead) { text.BrightGreen(subject); }
        else if (c.IsBehind) { text.BrightBlue(subject); }
        else if (c.BranchName == currentRowBranch.Name ||
            c.BranchName == currentRowBranch.LocalName ||
            c.BranchName == currentRowBranch.RemoteName)
        {
            text.White(Txt(subject, columnWidth));
        }
        else { text.DarkGray(Txt(subject, columnWidth)); }

        if (tips != null) { WriteBranchTips(tips, maxTipWidth); }
    }

    void WriteBranchTips(Text tips, int maxWidth)
    {
        if (tips.Length > maxWidth - 1)
        {
            maxWidth -= 2;
        }
        Text.New.Black(" ").Draw();
        tips.Draw(0, maxWidth - 1);
        if (tips.Length > maxWidth - 1)
        {
            Text.New.Dark("┅").Draw();
        }
    }

    void WriteSid(Columns cw, Commit c)
    {
        if (c.IsUncommitted)
        {
            text.Black(Txt("       ", cw.Sid));
            return;
        }
        text.DarkGray(Txt(" " + c.Sid, cw.Sid));
    }

    void WriteAuthor(Columns cw, Commit c)
    {
        text.DarkGray(Txt(" " + c.Author, cw.Author));
    }

    void WriteTime(Columns cw, Commit c)
    {
        text.DarkGray(Txt(" " + c.AuthorTime.ToString("yy-MM-dd HH:mm"), cw.Time));
    }

    string Txt(string text, int width)
    {
        if (text.Length <= width)
        {
            return text + new string(' ', width - text.Length);
        }

        return text.Substring(0, width);
    }

    Columns ColumnWidths(int commitWidth)
    {
        int authorWidth = 15;
        int timeWidth = 15;
        int sidWidth = 7;

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

        return new Columns(subjectWidth, sidWidth, authorWidth, timeWidth);
    }



    IReadOnlyDictionary<string, Text> GetBranchTips(IRepo repo)
    {
        var branchTips = new Dictionary<string, Text>();

        foreach (var b in repo.Repo.Branches)
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
                branchName = splitIndex == -1 ? "┅" + branchName : branchName;
            }

            var color = branchColorService.GetColor(repo.Repo, b);

            if (b.IsGitBranch)
            {
                if (b.IsRemote)
                {
                    branchName = "^/" + branchName;
                }
                if (b.IsCurrent)
                {
                    tipText.Add($"(", color).White("● ").Add($"{branchName})", color);
                }
                else
                {
                    tipText.Add($"({branchName})", color);
                }
            }
            else
            {
                tipText.Add("(~", color).Dark(branchName).Add(")", color);
            }

            branchTips[b.TipId] = tipText;
        }

        return branchTips;
    }
}


