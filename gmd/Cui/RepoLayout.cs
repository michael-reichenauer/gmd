using gmd.ViewRepos;

namespace gmd.Cui;


interface IRepoLayout
{
    void SetText(IEnumerable<Commit> commits, ColorText text);
}

class RepoLayout : IRepoLayout
{
    public RepoLayout()
    {
    }

    public void SetText(IEnumerable<Commit> commits, ColorText text)
    {
        text.Reset();

        foreach (var c in commits)
        {
            text.Magenta(" ┃ ┃");
            text.Blue($" {c.Sid}");
            text.White($" {c.Subject.Max(50),-50}");
            text.Green($" {c.Author.Max(10),-10}");
            text.DarkGray($" {c.AuthorTime.ToString().Max(10),-10}");
            text.EoL();
        }
    }
}


