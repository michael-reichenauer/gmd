namespace gmd.Utils.Git.Private;

internal class GitService : IGitService
{
    public GitService()
    {
    }

    public IGitRepo GetRepo(string path)
    {
        return new GitRepo(path);
    }
}