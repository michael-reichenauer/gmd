using gmd.ViewRepos;
using Terminal.Gui;

namespace gmd.Cui;

interface IRepoWriter
{
    void WriteRepo(Graph graph, Repo repo, int width, int firstCommit, int commitCount, int currentIndex);
}

class RepoWriter : IRepoWriter
{
    private readonly ColorText text;
    private IGraphWriter graphWriter;

    record Columns(int Subject, int Sid, int Author, int Time);

    public RepoWriter(View view, int startX)
    {
        this.text = new ColorText(view, startX);
        graphWriter = new GraphWriter(text);
    }

    public void WriteRepo(Graph graph, Repo repo, int width, int firstCommit, int commitCount, int currentIndex)
    {
        var crc = repo.Commits[currentIndex];
        var crb = repo.BranchByName[crc.BranchName];

        text.Reset();
        int graphWidth = 3;// graph.Width;
        int markersWidth = 3; // 1 margin to graph and then 1 current marker and 1 ahead/behind

        Columns cw = ColumnWidths(width - (graphWidth + markersWidth));

        for (int i = firstCommit; i < firstCommit + commitCount; i++)
        {
            var c = repo.Commits[i];
            var graphRow = graph.GetRow(i);
            WriteGraph(graphRow);
            WriteCurrentMarker(c);
            WriteAheadBehindMarker(c);
            WriteSubject(cw, c, crb);
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

    void WriteCurrentMarker(Commit c)
    {
        if (c.IsCurrent)
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

    void WriteSubject(Columns cw, Commit c, Branch currentRowBranch)
    {
        string subject = $"{c.Subject,-60} ({Text(c.BranchName, 20)})";
        if (c.IsConflicted)
        {
            text.BrightRed(Text(subject, cw.Subject));
            return;
        }
        if (c.IsUncommitted)
        {
            text.BrightYellow(Text(subject, cw.Subject));
            return;
        }
        if (c.IsAhead)
        {
            text.BrightGreen(Text(subject, cw.Subject));
            return;
        }
        if (c.IsBehind)
        {
            text.BrightBlue(Text(subject, cw.Subject));
            return;
        }

        if (c.BranchName == currentRowBranch.Name ||
            c.BranchName == currentRowBranch.LocalName ||
            c.BranchName == currentRowBranch.RemoteName)
        {
            text.White(Text(subject, cw.Subject));
            return;
        }

        text.DarkGray(Text(subject, cw.Subject));
    }

    void WriteSid(Columns cw, Commit c)
    {
        text.Green(Text(" " + c.Sid, cw.Sid));
    }

    void WriteAuthor(Columns cw, Commit c)
    {
        text.DarkGray(Text(" " + c.Author, cw.Author));
    }

    void WriteTime(Columns cw, Commit c)
    {
        text.Blue(Text(" " + c.AuthorTime.ToString("yy-MM-dd HH:mm"), cw.Time));
    }

    string Text(string text, int width)
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
}


