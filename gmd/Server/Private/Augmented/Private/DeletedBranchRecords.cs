using gmd.Common;

namespace gmd.Server.Private.Augmented.Private;

// The records of the branches gmd deleted (RepoConfig.DeletedBranches): how a delete is added to
// them, which of them can still be restored, and forgetting what is restored or cannot be. No git
// and no view, so it is testable as it is.
//
// A record is the two sides of one pair, a local branch and the remote branch it tracked, one of
// them deleted or both, and a side is in one record at most: a branch made again and deleted again
// is where it was the last time. Records are told apart by their sides rather than by name, since
// two deletes of a name can be of branches that have nothing to do with each other, say a local
// 'fix' that tracked nothing and an 'origin/fix' someone else pushed.
static class DeletedBranchRecords
{
    // The newest are kept: a delete older than these is the work of Recover Lost Commits, if anything
    public const int MaxCount = 20;

    // Adds a delete as the newest record. An older side of the same branch is replaced, and so is an
    // older side of the remote branch a deleted local branch tracked, which is there, then. A delete
    // of one side is joined with the older record of the other side of its pair, so that a branch
    // deleted here and later on origin, or the other way round, is restored as one: a remote branch
    // no local branch tracks any more with the local branch that tracked it, and a local branch
    // tracking nothing with the remote branch it tracked, when the record of that says it did.
    public static void Add(List<RecordedDelete> records, RecordedDelete deleted)
    {
        foreach (var older in records)
        {
            if (deleted.TipId != "" && older.Name == deleted.Name)
                ForgetLocal(older);
            if (deleted.RemoteName != "" && older.RemoteName == deleted.RemoteName)
                ForgetRemote(older);
        }
        records.RemoveAll(IsEmpty);

        var isLocal = deleted.TipId != "";
        var isRemote = deleted.RemoteTipId != "";
        if (isRemote && !isLocal && deleted.Name == "")
        {
            var local = records.FirstOrDefault(r =>
                r.TipId != "" && r.RemoteTipId == "" && r.RemoteName == deleted.RemoteName
            );
            if (local != null)
            {
                records.Remove(local);
                deleted.Name = local.Name;
                deleted.TipId = local.TipId;
                deleted.Subject = local.Subject;
            }
        }
        else if (isLocal && !isRemote && deleted.RemoteName == "")
        {
            var remote = records.FirstOrDefault(r => r.TipId == "" && r.RemoteTipId != "" && r.Name == deleted.Name);
            if (remote != null)
            {
                records.Remove(remote);
                deleted.RemoteName = remote.RemoteName;
                deleted.RemoteTipId = remote.RemoteTipId;
                deleted.RemoteSubject = remote.RemoteSubject;
            }
        }

        records.Insert(0, deleted);
        if (records.Count > MaxCount)
            records.RemoveRange(MaxCount, records.Count - MaxCount);
    }

    // The tips of the records, for asking git which of them are commits it still has
    public static IReadOnlyList<string> TipIds(IEnumerable<RecordedDelete> records) =>
        records.SelectMany(r => new[] { r.TipId, r.RemoteTipId }).Where(id => id != "").Distinct().ToList();

    // The record as it can be restored now, or null when neither side can: a side whose tip git no
    // longer has, since a gc pruned it, or whose name a branch has again, which a restore would
    // collide with. A local branch is looked for here, a remote one as the repo last fetched it, which
    // the push that restores it checks again on origin. A remote branch no local branch tracked is
    // named as its local branch would be.
    public static DeletedBranch? Restorable(RecordedDelete record, Repo repo, IReadOnlySet<string> existingIds)
    {
        var tipId =
            record.TipId != "" && existingIds.Contains(record.TipId) && !HasBranch(repo, record.Name, false)
                ? record.TipId
                : "";
        var remoteTipId =
            record.RemoteTipId != ""
            && existingIds.Contains(record.RemoteTipId)
            && !HasBranch(repo, record.RemoteName, true)
                ? record.RemoteTipId
                : "";
        if (tipId == "" && remoteTipId == "")
            return null;

        var name = record.Name != "" ? record.Name : record.RemoteName.TrimPrefix("origin/");
        var subject = tipId != "" ? record.Subject : record.RemoteSubject;
        return new DeletedBranch(name, tipId, record.RemoteName, remoteTipId, subject, record.Time);
    }

    // The sides of a record that cannot be restored, as Restorable read it, to forget, or null when
    // both can
    public static RecordedDelete? Unrestorable(RecordedDelete record, DeletedBranch? restorable)
    {
        var tipId = record.TipId != "" && restorable?.IsLocal != true ? record.TipId : "";
        var remoteTipId = record.RemoteTipId != "" && restorable?.IsRemote != true ? record.RemoteTipId : "";
        if (tipId == "" && remoteTipId == "")
            return null;

        return new RecordedDelete
        {
            Name = record.Name,
            TipId = tipId,
            RemoteName = record.RemoteName,
            RemoteTipId = remoteTipId,
        };
    }

    // The sides of a deleted branch that were restored, to forget
    public static RecordedDelete Restored(DeletedBranch deleted, bool isLocal, bool isRemote) =>
        new()
        {
            Name = deleted.Name,
            TipId = isLocal ? deleted.TipId : "",
            RemoteName = deleted.RemoteName,
            RemoteTipId = isRemote ? deleted.RemoteTipId : "",
        };

    // Forgets the sides given, the ones with a tip, and a record once it has neither side: one kept
    // would be offered again, and joined with a later delete. A side is found by its branch and tip,
    // since the records can have changed since the sides were read from them.
    public static void Forget(List<RecordedDelete> records, RecordedDelete sides)
    {
        foreach (var r in records)
        {
            if (sides.TipId != "" && r.Name == sides.Name && r.TipId == sides.TipId)
                ForgetLocal(r);
            if (sides.RemoteTipId != "" && r.RemoteName == sides.RemoteName && r.RemoteTipId == sides.RemoteTipId)
                ForgetRemote(r);
        }
        records.RemoveAll(IsEmpty);
    }

    // The name stays, since it says which local branch tracked the remote branch
    static void ForgetLocal(RecordedDelete r)
    {
        r.TipId = "";
        r.Subject = "";
    }

    // The remote name stays, since it says which remote branch the local branch tracked
    static void ForgetRemote(RecordedDelete r)
    {
        r.RemoteTipId = "";
        r.RemoteSubject = "";
    }

    static bool IsEmpty(RecordedDelete r) => r.TipId == "" && r.RemoteTipId == "";

    // A branch git has, rather than one the inference made up for a deleted branch, whose name is
    // different anyway ('feature:1a2b3c')
    static bool HasBranch(Repo repo, string name, bool isRemote) =>
        repo.BranchByName.TryGetValue(name, out var b) && b.IsGitBranch && b.IsRemote == isRemote;
}
