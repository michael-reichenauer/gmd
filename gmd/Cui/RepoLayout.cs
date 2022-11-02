using gmd.ViewRepos;

namespace gmd.Cui;


interface IRepoLayout
{
    void SetText(Repo repo, int first, int count, int width, ColorText text);
}

class RepoLayout : IRepoLayout
{
    record Columns(int Subject, int Sid, int Author, int Time);

    public RepoLayout()
    {
    }

    public void SetText(Repo repo, int first, int count, int width, ColorText text)
    {
        text.Reset();
        int graphWidth = 6;

        Columns cw = ColumnWidths(width - graphWidth);

        var commits = repo.Commits.Skip(first).Take(count);
        foreach (var c in commits)
        {
            DrawGraph(text);
            DrawSubject(text, cw, c);
            DrawSid(text, cw, c);
            DrawAuthor(text, cw, c);
            DrawTime(text, cw, c);
            text.EoL();
        }
    }

    private static void DrawGraph(ColorText text)
    {
        text.Magenta(" ┃ ┃");
    }

    private void DrawSubject(ColorText text, Columns cw, Commit c)
    {
        text.White(Width(" " + c.Subject, cw.Subject));
    }

    private void DrawSid(ColorText text, Columns cw, Commit c)
    {
        text.Green(Width(" " + c.Sid, cw.Sid));
    }

    private void DrawAuthor(ColorText text, Columns cw, Commit c)
    {
        text.DarkGray(Width(" " + c.Author, cw.Author));
    }

    private void DrawTime(ColorText text, Columns cw, Commit c)
    {
        text.Blue(Width(" " + c.AuthorTime.ToString("yy-MM-dd HH:mm"), cw.Time));
    }

    string Width(string text, int width)
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
        // int spaceWidth = 1;

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
            // spaceWidth = 3;
        }

        int subjectWidth = commitWidth - sidWidth - authorWidth - timeWidth;
        if (subjectWidth < 0)
        {
            subjectWidth = 0;
        }

        return new Columns(subjectWidth, sidWidth, authorWidth, timeWidth);
    }
}


