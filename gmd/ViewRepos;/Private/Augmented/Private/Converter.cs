

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
            workRepo.Commits.Select((WorkCommit c, int index) => ToCommit(c, index)).ToList(),
            workRepo.Branches.Select(ToBranch).ToList(),
            workRepo.Status
        );
    }

    Commit ToCommit(WorkCommit c, int index)
    {
        return new Commit(
            Id: c.Id,
            Sid: c.Sid,
            Subject: c.Subject,
            Message: c.Message,
            Author: c.Author,
            AuthorTime: c.AuthorTime,
            ParentIds: c.ParentIds,
            Index: index,

            BranchName: c.Branch!.Name,
            ChildIds: c.ChildIds,
            Tags: c.Tags,
            BranchTips: c.BranchTips,
            IsCurrent: c.IsCurrent,
            IsUncommitted: c.IsUncommitted,
            IsConflicted: c.IsConflicted,
            IsAhead: c.IsAhead,
            IsBehind: c.IsBehind,
            IsPartialLogCommit: c.IsPartialLogCommit,
            IsAmbiguous: c.IsAmbiguous,
            IsAmbiguousTip: c.IsAmbiguousTip);
    }

    Branch ToBranch(WorkBranch b)
    {
        return new Branch(
            Name: b.Name,
            DisplayName: b.DisplayName,
            TipId: b.TipID,
            BottomId: b.BottomID,
            IsCurrent: b.IsCurrent,
            IsRemote: b.IsRemote,
            RemoteName: b.RemoteName,
            LocalName: b.LocalName,

            ParentBranchName: b.ParentBranch?.Name ?? "",

            IsGitBranch: b.IsGitBranch,
            IsDetached: b.IsDetached,
            IsSetAsParent: b.IsSetAsParent,
            IsMainBranch: b.IsMainBranch,

            AheadCount: b.AheadCount,
            BehindCount: b.BehindCount,
            HasAheadCommits: b.HasLocalOnly,
            HasBehindCommits: b.HasRemoteOnly,
            AmbiguousTipId: b.AmbiguousTipId,
            AmbiguousBranchNames: b.AmbiguousBranchNames);
    }
}

