
using GitStatus = gmd.Git.Status;

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IConverter
{
    Repo ToRepo(WorkRepo augRepo);
    Status ToStatus(GitStatus gitStatus);
}

class Converter : IConverter
{
    public Repo ToRepo(WorkRepo workRepo)
    {
        return new Repo(
            workRepo.TimeStamp,
            workRepo.Path,
            workRepo.Commits.Select((WorkCommit c, int index) => ToCommit(c, index)).ToList(),
            workRepo.Branches.Select(ToBranch).ToList(),
            workRepo.Status
        );
    }

    public Status ToStatus(GitStatus gitStatus)
    {
        var s = gitStatus;
        return new Status(s.Modified, s.Added, s.Deleted, s.Conflicted,
          s.IsMerging, s.MergeMessage, s.AddedFiles, s.ConflictsFiles);
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
            CommonName: b.CommonName,
            DisplayName: b.DisplayName,
            TipId: b.TipID,
            BottomId: b.BottomID,
            IsCurrent: b.IsCurrent,
            IsLocalCurrent: b.IsLocalCurrent,
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

