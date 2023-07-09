
using GitStatus = gmd.Git.Status;

namespace gmd.Server.Private.Augmented.Private;

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
            workRepo.Stashes.ToList(),
            workRepo.Status
        );
    }

    public Status ToStatus(GitStatus gitStatus)
    {
        var s = gitStatus;
        return new Status(s.Modified, s.Added, s.Deleted, s.Conflicted,
            s.IsMerging, s.MergeMessage, s.MergeHeadId, s.ModifiedFiles,
            s.AddedFiles, s.DeletedFiles, s.ConflictsFiles);
    }

    Commit ToCommit(WorkCommit c, int gitIndex)
    {
        return new Commit(
            Id: c.Id,
            Sid: c.Sid,
            Subject: c.Subject,
            Message: c.Message,
            Author: c.Author,
            AuthorTime: c.AuthorTime,
            ParentIds: c.ParentIds,
            GitIndex: gitIndex,

            BranchName: c.Branch!.Name,
            BranchCommonName: c.Branch.CommonName,
            BranchViewName: c.Branch.ViewName,
            ChildIds: c.ChildIds,
            Tags: c.Tags,
            BranchTips: c.BranchTips,
            IsCurrent: c.IsCurrent,
            IsDetached: c.IsDetached,
            IsUncommitted: false,
            IsConflicted: false,
            IsAhead: false,
            IsBehind: false,
            IsTruncatedLogCommit: c.IsTruncatedLogCommit,
            IsAmbiguous: c.IsAmbiguous,
            IsAmbiguousTip: c.IsAmbiguousTip,
            IsBranchSetByUser: c.IsBranchSetByUser);
    }

    Branch ToBranch(WorkBranch b)
    {
        return new Branch(
            Name: b.Name,
            CommonName: b.CommonName,
            CommonBaseName: b.CommonBaseName,
            HumanName: b.HumanName,
            ViewName: b.ViewName,
            TipId: b.TipID,
            BottomId: b.BottomID,
            IsCurrent: b.IsCurrent,
            IsLocalCurrent: b.IsLocalCurrent,
            IsRemote: b.IsRemote,
            RemoteName: b.RemoteName,
            LocalName: b.LocalName,

            ParentBranchName: b.ParentBranch?.Name ?? "",
            ParentBranchCommonName: b.ParentBranch?.CommonName ?? "",
            PullMergeParentBranchName: b.PullMergeParentBranch?.Name ?? "",

            IsGitBranch: b.IsGitBranch,
            IsDetached: b.IsDetached,
            IsSetAsParent: b.IsSetAsParent,
            IsMainBranch: b.IsMainBranch,

            HasAheadCommits: b.HasLocalOnly,
            HasBehindCommits: b.HasRemoteOnly,
            AmbiguousTipId: b.AmbiguousTipId,
            AmbiguousBranchNames: b.AmbiguousBranches.Select(bb => bb.Name).ToList(),
            PullMergeBranchNames: b.PullMergeChildBranches.Select(bb => bb.Name).ToList());
    }
}

