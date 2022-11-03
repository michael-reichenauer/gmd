namespace gmd.ViewRepos.Private.Augmented.Private;

interface IBranchNameService
{
    void ParseCommit(WorkCommit c);
    bool IsPullMerge(WorkCommit c);
    string GetBranchName(string id);
}


class BranchNameService : IBranchNameService
{
    public string GetBranchName(string id)
    {
        return "";
    }

    public bool IsPullMerge(WorkCommit c)
    {

        return false;
    }

    public void ParseCommit(WorkCommit c)
    {
    }
}