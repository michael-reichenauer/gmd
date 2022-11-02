using gmd.ViewRepos.Private.Augmented;

namespace gmd.ViewRepos.Private;


class ViewRepoService : IViewRepoService
{
    private readonly IAugmentedRepoService augmentedRepoService;

    public ViewRepoService(IAugmentedRepoService augmentedRepoService)
    {
        this.augmentedRepoService = augmentedRepoService;
    }

    public event EventHandler? RepoChange;


    public async Task<R<Repo>> GetRepoAsync(string path)
    {
        var augmentedRepo = await augmentedRepoService.GetRepoAsync(path);
        if (augmentedRepo.IsError)
        {
            return augmentedRepo.Error;
        }

        return await GetViewRepoAsync(augmentedRepo.Value);
    }

    protected virtual void OnRepoChange(EventArgs e)
    {
        EventHandler? handler = RepoChange;
        handler?.Invoke(this, e);
    }

    async Task<R<Repo>> GetViewRepoAsync(Augmented.Repo augmentedRepo)
    {
        return new Repo(
            augmentedRepo.Commits.Select(ToCommit).ToList(),
            augmentedRepo.Branches.Select(ToBranch).ToList());
    }

    Commit ToCommit(Augmented.Commit c)
    {
        return new Commit(
            Id: c.Id,
            Sid: c.Sid,
            Subject: c.Subject,
            Message: c.Message,
            Author: c.Author,
            AuthorTime: c.AuthorTime,

            BranchName: c.BranchName,
            ParentIds: c.ParentIds,
            ChildIds: c.ChildIds,
            Tags: c.Tags,
            BranchTips: c.BranchTips,
            IsCurrent: false,
            IsUncommitted: false,
            IsLocalOnly: false,
            IsRemoteOnly: false,
            IsPartialLogCommit: false,
            IsAmbiguous: false,
            IsAmbiguousTip: false,

            More: More.None);
    }

    Branch ToBranch(Augmented.Branch b)
    {
        return new Branch(
            Name: b.Name,
            DisplayName: b.DisplayName,
            TipID: b.TipID,
            IsCurrent: b.IsCurrent,
            IsRemote: b.IsRemote,
            RemoteName: b.RemoteName,

            IsGitBranch: b.IsGitBranch,
            IsDetached: b.IsDetached,
            IsAmbiguousBranch: b.IsAmbiguousBranch,
            IsSetAsParent: b.IsSetAsParent,
            IsMainBranch: b.IsMainBranch,

            AheadCount: b.AheadCount,
            BehindCount: b.BehindCount,
            HasLocalOnly: b.HasLocalOnly,
            HasRemoteOnly: b.HasRemoteOnly,

            AmbiguousTipId: b.AmbiguousTipId,
            AmbiguousBranchNames: b.AmbiguousBranchNames,

            X: 0,
            IsIn: false,
            IsOut: false);
    }
}

