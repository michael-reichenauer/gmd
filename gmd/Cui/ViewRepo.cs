using gmd.ViewRepos;
using Terminal.Gui;

namespace gmd.Cui;


interface IRepo
{
    Repo Repo { get; }
    Graph Graph { get; }
    int TotalRows { get; }
    int CurrentIndex { get; }
    Commit CurrentRowCommit { get; }
    int ViewWidth { get; }
    Point CurrentPoint { get; }

    void Refresh();
    void UpdateRepo(Repo newRepo);

    void Commit();
    void PushCurrentBranch();
}

class ViewRepo : IRepo
{
    readonly IRepoView repoView;
    readonly IRepoCommands cmds;

    internal ViewRepo(
        IRepoView repoView,
        Repo repo,
        IGraphService graphService,
        IRepoCommands cmds)
    {
        this.repoView = repoView;
        Repo = repo;
        this.cmds = cmds;

        Graph = graphService.CreateGraph(repo);
    }

    public Repo Repo { get; init; }

    public Graph Graph { get; init; }
    public int TotalRows => Repo.Commits.Count;
    public int CurrentIndex => repoView.CurrentIndex;
    public Commit CurrentRowCommit => Repo.Commits[CurrentIndex];
    public int ViewWidth => repoView.ViewWidth;
    public Point CurrentPoint => repoView.CurrentPoint;

    public void Refresh() => repoView.Refresh();
    public void UpdateRepo(Repo newRepo) => repoView.UpdateRepo(newRepo);

    public void Commit() => cmds.Commit(this);
    public void PushCurrentBranch() => cmds.PushCurrentBranch(this);

}

// interface IRepo
// {
//     int ViewWidth { get; }
//     Repo Repo { get; }
//     int CurrentIndex { get; }
//     Point CurrentPoint { get; }

//     void Refresh();
//     void UpdateRepo(Repo newRepo);
// }
