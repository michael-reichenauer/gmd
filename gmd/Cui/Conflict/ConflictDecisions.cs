using gmd.Server;

namespace gmd.Cui.Conflict;

// The decisions made in the files of the workspace and not saved, kept for as long as the resolver is
// open: going to another file keeps those of the file left, and going back to it finds them as they
// were. Going to another file asked about them instead, which the list could not do as its arrows
// show one file after the other, so they are asked about once, as the resolver closes. No view, so
// it is unit testable.
class ConflictDecisions
{
    // One per file, in the order the files were left
    readonly List<ConflictResolution> kept = [];

    public IReadOnlyList<ConflictResolution> Kept => kept;

    // The decisions of a file left, kept where there are any
    public void Keep(ConflictResolution resolution)
    {
        Forget(resolution.File.Path);
        if (resolution.IsChanged)
            kept.Add(resolution);
    }

    // A file done with, saved or decided whole, or the resolver closed with its decisions asked about
    public void Forget(string path) => kept.RemoveAll(r => r.File.Path == path);

    public void Clear() => kept.Clear();

    // The decisions for the file, as it is opened again: those kept for it, or none. isLost where some
    // were kept but no longer fit it, changed on disk since (ConflictResolution.For).
    public ConflictResolution Take(ConflictFile file, out bool isLost)
    {
        var resolution = kept.FirstOrDefault(r => r.File.Path == file.Path);
        Forget(file.Path);
        var same = resolution?.For(file);
        isLost = resolution != null && same == null;
        return same ?? new ConflictResolution(file);
    }

    // The files with decisions not saved, the open one's last, which closing asks about
    public IReadOnlyList<ConflictResolution> Unsaved(ConflictResolution open)
    {
        List<ConflictResolution> unsaved = [.. kept.Where(r => r.File.Path != open.File.Path)];
        if (open.IsChanged)
            unsaved.Add(open);
        return unsaved;
    }
}
