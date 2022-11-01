using gmd.Utils.Git;

namespace gmd.Cui;


interface IRepoLayout
{
    void SetText(IReadOnlyList<Commit> commits, ColorText text);
}

class RepoLayout : IRepoLayout
{
    public RepoLayout()
    {
    }

    public void SetText(IReadOnlyList<Commit> commits, ColorText text)
    {
        for (int i = 0; i < 100; i++)
        {
            foreach (var c in commits)
            {
                text.Append(" ┃ ┃", Colors.Magenta);
                text.Append($" {c.Sid}", Colors.Blue);
                text.Append($" {c.Subject.Max(50),-50}", Colors.White);
                text.Append($" {c.Author.Max(10),-10}", Colors.Green);
                text.Append($" {c.AuthorTime.ToString().Max(10),-10}", Colors.DarkGray);
                text.EoL();
            }
        }
    }
}


