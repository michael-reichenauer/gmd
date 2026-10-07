using gmd.Common;

namespace gmd.Server.Private.Augmented.Private;

// The records of the branches gmd deleted (RepoConfig.DeletedBranches): how a delete is added to
// them, and which of them can still be restored. No git and no view, so it is testable as it is.
static class DeletedBranchRecords
{
    // The newest are kept: a delete older than these is the work of Recover Lost Commits, if anything
    public const int MaxCount = 20;

    // Adds a delete as the newest record, in the place of an older one of the same name, whose sides
    // the new one did not delete are kept: a branch deleted here and later on origin is restored as
    // one. A delete of both sides is added once per side, as each is deleted, and joined this way.
    public static void Add(List<RecordedDelete> records, RecordedDelete deleted)
    {
        var older = records.FirstOrDefault(r => r.Name == deleted.Name);
        if (older != null)
        {
            records.Remove(older);
            if (deleted.TipId == "" && older.TipId != "")
            {
                deleted.TipId = older.TipId;
                deleted.Subject = older.Subject;
            }
            if (deleted.RemoteTipId == "")
                deleted.RemoteTipId = older.RemoteTipId;
            if (deleted.RemoteName == "")
                deleted.RemoteName = older.RemoteName;
        }

        records.Insert(0, deleted);
        if (records.Count > MaxCount)
            records.RemoveRange(MaxCount, records.Count - MaxCount);
    }

    // The tips of the records, for asking git which of them are commits it still has
    public static IReadOnlyList<string> TipIds(IEnumerable<RecordedDelete> records) =>
        records.SelectMany(r => new[] { r.TipId, r.RemoteTipId }).Where(id => id != "").Distinct().ToList();

    // The record as it can be restored now, or null when neither side can: a side whose tip git no
    // longer has, since a gc pruned it, or whose name a branch has again, by a restore or otherwise,
    // which a restore would collide with. A local branch is looked for here, a remote one as the repo
    // last fetched it, which the push that restores it checks again on origin.
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

        return new DeletedBranch(record.Name, tipId, record.RemoteName, remoteTipId, record.Subject, record.Time);
    }

    // Whether two records are the same delete, as read from the config at different times
    public static bool IsSame(RecordedDelete a, RecordedDelete b) =>
        a.Name == b.Name && a.TipId == b.TipId && a.RemoteTipId == b.RemoteTipId;

    // A branch git has, rather than one the inference made up for a deleted branch, whose name is
    // different anyway ('feature:1a2b3c')
    static bool HasBranch(Repo repo, string name, bool isRemote) =>
        repo.BranchByName.TryGetValue(name, out var b) && b.IsGitBranch && b.IsRemote == isRemote;
}
