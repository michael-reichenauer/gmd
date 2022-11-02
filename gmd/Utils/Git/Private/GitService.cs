namespace gmd.Utils.Git.Private;

internal class GitService : IGitService
{
    public GitService()
    {
    }

    public IGit GetGit(string path)
    {
        return new Git(path);
    }
}