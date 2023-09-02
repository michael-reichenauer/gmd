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
        var allCommits = workRepo.Commits.Select(ToCommit).ToList();
        var allBranches = workRepo.Branches.Values.Select(ToBranch).ToList();
        var viewCommits = new List<Commit>();
        var viewBranches = new List<Branch>();

        return new Repo(
            workRepo.Path,
            workRepo.TimeStamp,
            workRepo.TimeStamp,
            viewCommits,
            viewBranches,
            allCommits,
            allBranches,
            workRepo.Stashes.ToList(),
            workRepo.Status,
            ""
        );
    }

    public Status ToStatus(GitStatus gitStatus)
    {
        var s = gitStatus;
        return new Status(s.Modified, s.Added, s.Deleted, s.Conflicted, s.Renamed,
            s.IsMerging, s.MergeMessage, s.MergeHeadId, s.ModifiedFiles,
            s.AddedFiles, s.DeletedFiles, s.ConflictsFiles, s.RenamedSourceFiles, s.RenamedTargetFiles);
    }

    static Commit ToCommit(WorkCommit c, int gitIndex)
    {
        return new Commit(
            Id: c.Id,
            Sid: c.Sid,
            Subject: c.Subject,
            Message: c.Message,
            Author: c.Author,
            AuthorTime: c.AuthorTime,
            ParentIds: c.ParentIds,
            IsInView: false,
            ViewIndex: -1,
            GitIndex: gitIndex,

            BranchName: c.Branch!.Name,
            BranchPrimaryName: c.Branch.PrimaryName,
            BranchNiceUniqueName: c.Branch.NiceNameUnique,
            AllChildIds: c.AllChildIds,
            FirstChildIds: c.FirstChildIds,
            MergeChildIds: c.MergeChildIds,
            Tags: c.Tags,
            BranchTips: c.BranchTips,
            IsCurrent: c.IsCurrent,
            IsDetached: c.IsDetached,
            IsUncommitted: c.IsUncommitted,
            IsConflicted: false,
            IsAhead: false,
            IsBehind: false,
            IsTruncatedLogCommit: c.IsTruncatedLogCommit,
            IsAmbiguous: c.IsAmbiguous,
            IsAmbiguousTip: c.IsAmbiguousTip,
            IsBranchSetByUser: c.IsBranchSetByUser,
            HasStash: c.HasStash,
            More: More.None);
    }

    Branch ToBranch(WorkBranch b)
    {
        return new Branch(
            Name: b.Name,
            PrimaryName: b.PrimaryName,
            PrimaryBaseName: b.PrimaryBaseName,
            NiceName: b.NiceName,
            NiceNameUnique: b.NiceNameUnique,
            TipId: b.TipID,
            BottomId: b.BottomID,
            IsCurrent: b.IsCurrent,
            IsLocalCurrent: b.IsLocalCurrent,
            IsRemote: b.IsRemote,
            RemoteName: b.RemoteName,
            LocalName: b.LocalName,

            ParentBranchName: b.ParentBranch?.Name ?? "",
            PullMergeParentBranchName: b.PullMergeParentBranch?.Name ?? "",

            IsInView: false,
            IsGitBranch: b.IsGitBranch,
            IsDetached: b.IsDetached,
            IsPrimary: b.IsPrimary,
            IsMainBranch: b.IsMainBranch,
            IsCircularAncestors: b.IsCircularAncestors,

            HasLocalOnly: b.HasLocalOnly,
            HasRemoteOnly: b.HasRemoteOnly,
            AmbiguousTipId: b.AmbiguousTip?.Id ?? "",
            RelatedBranchNames: b.RelatedBranches.Select(bb => bb.Name).ToList(),
            AmbiguousBranchNames: b.AmbiguousBranches.Select(bb => bb.Name).ToList(),
            PullMergeBranchNames: b.PullMergeChildBranches.Select(bb => bb.Name).ToList(),
            AncestorNames: b.Ancestors.Select(bb => bb.Name).ToList(),
            X: 0,
            IsIn: false,
            IsOut: false);
    }
}

