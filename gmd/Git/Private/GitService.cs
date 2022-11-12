namespace gmd.Git.Private;

internal class GitService : IGitService
{
    public GitService()
    {
    }

    public IGit Git(string path)
    {
        return new Git(path);
    }
}