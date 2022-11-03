

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IConverter
{
    Repo ToRepo(WorkRepo augRepo);
}

class Converter : IConverter
{
    public Repo ToRepo(WorkRepo workRepo)
    {
        return new Repo(
            workRepo.Commits.Select(ToCommit).ToList(),
            workRepo.Branches.Select(ToBranch).ToList()
        );
    }


    Commit ToCommit(WorkCommit c)
    {
        return new Commit(
            Id: c.Id,
            Sid: c.Sid,
            Subject: c.Subject,
            Message: c.Message,
            Author: c.Author,
            AuthorTime: c.AuthorTime,
            ParentIds: c.ParentIds,

            BranchName: c.Branch!.Name,
            ChildIds: c.ChildIds,
            Tags: c.Tags,
            BranchTips: c.BranchTips,
            IsCurrent: c.IsCurrent,
            IsUncommitted: c.IsUncommitted,
            IsLocalOnly: c.IsLocalOnly,
            IsRemoteOnly: c.IsRemoteOnly,
            IsPartialLogCommit: c.IsPartialLogCommit,
            IsAmbiguous: c.IsAmbiguous,
            IsAmbiguousTip: c.IsAmbiguousTip);
    }

    Branch ToBranch(WorkBranch b)
    {
        return new Branch(
            Name: b.Name,
            DisplayName: b.DisplayName,
            TipID: b.TipID,
            IsCurrent: b.IsCurrent,
            IsRemote: b.IsRemote,
            RemoteName: b.RemoteName,
            LocalName: b.LocalName,

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
            AmbiguousBranchNames: b.AmbiguousBranchNames);
    }
}

