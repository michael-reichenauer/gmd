namespace gmd.Utils.Git.Private;

internal class GitRepo : IGitRepo
{
    public Commit[] Log(int maxCount = 30000)
    {
        return new Commit[] { };
    }

}