using gmd.Server;

namespace gmd.Cui.Common;

interface IRepoCommands
{
    void ShowBranch(string name, bool includeAmbiguous);
    void HideBranch(string name);
    void SwitchTo(string branchName);
}

class RepoCommands : IRepoCommands
{
    readonly IServer server;
    readonly IProgress progress;

    readonly IRepo repo;
    readonly Repo serverRepo;
    readonly IRepoView repoView;

    string path => serverRepo.Path;

    internal RepoCommands(
        IRepo repo,
        Server.Repo serverRepo,
        IRepoView repoView,
        IServer server,
        IProgress progress)
    {
        this.repo = repo;
        this.serverRepo = serverRepo;
        this.repoView = repoView;
        this.server = server;
        this.progress = progress;
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

    public void SwitchTo(string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.SwitchToAsync(branchName, path)))
        {
            return R.Error($"Failed to switch to {branchName}", e);
        }
        Refresh();
        return R.Ok;
    });


    void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);

    void UpdateRepoTo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);

    void Do(Func<Task<R>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await action()))
                {
                    UI.ErrorMessage($"{e.AllErrorMessages()}");
                }
            }
        });
    }
}

