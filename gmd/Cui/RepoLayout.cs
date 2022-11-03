using gmd.ViewRepos;
using Terminal.Gui;

namespace gmd.Cui;


interface IRepoLayout
{
    void WriteRepo(Repo repo, int width, int firstCommit, int commitCount, int currentIndex);
}

class RepoLayout : IRepoLayout
{
    private readonly ColorText text;

    record Columns(int Subject, int Sid, int Author, int Time);

    public RepoLayout(View view, int startX)
    {
        this.text = new ColorText(view, startX);
    }

    public void WriteRepo(Repo repo, int width, int firstCommit, int commitCount, int currentIndex)
    {
        var crc = repo.Commits[currentIndex];
        var crb = repo.BranchByName[crc.BranchName];

        text.Reset();
        int graphWidth = 3;
        int markersWidth = 3; // 1 margin to graph and then 1 current marker and 1 ahead/behind

        Columns cw = ColumnWidths(width - (graphWidth + markersWidth));

        var commits = repo.Commits.Skip(firstCommit).Take(commitCount);
        foreach (var c in commits)
        {
            WriteGraph();
            WriteCurrentMarker(c);
            WriteAheadBehindMarker(c);
            WriteSubject(cw, c, crb);
            WriteSid(cw, c);
            WriteAuthor(cw, c);
            WriteTime(cw, c);
            text.EoL();
        }
    }


    void WriteGraph()
    {
        text.Magenta("┃ ┃");
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
        if (c.IsLocalOnly)
        {
            text.White("▲");
            return;
        }
        if (c.IsRemoteOnly)
        {
            text.White("▼");
            return;
        }

        text.White(" ");
    }

    void WriteSubject(Columns cw, Commit c, Branch currentRowBranch)
    {
        if (c.BranchName == currentRowBranch.Name ||
            c.BranchName == currentRowBranch.LocalName ||
            c.BranchName == currentRowBranch.RemoteName)
        {
            text.White(Text(c.Subject, cw.Subject));
            return;
        }

        text.DarkGray(Text(c.Subject, cw.Subject));
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


