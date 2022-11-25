using gmd.Server;

namespace gmd.Cui.Common;

interface IRepoCommands
{
    void ShowBranch(string name, bool includeAmbiguous);
    void HideBranch(string name);
}

class RepoCommands : IRepoCommands
{
    readonly IServer server;

    readonly IRepo repo;
    readonly Repo serverRepo;
    readonly IRepoView repoView;

    internal RepoCommands(IRepo repo, Server.Repo serverRepo, IRepoView repoView, IServer server)
    {
        this.repo = repo;
        this.serverRepo = serverRepo;
        this.repoView = repoView;
        this.server = server;
    }

    public void ShowBranch(string name, bool includeAmbiguous)
    {
        Server.Repo newRepo = server.ShowBranch(serverRepo, name, includeAmbiguous);
        UpdateRepoTo(newRepo, name);

    }

    public void HideBranch(string name)
    {
        Server.Repo newRepo = server.HideBranch(serverRepo, name);
        UpdateRepoTo(newRepo);
    }

    void UpdateRepoTo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);
}

