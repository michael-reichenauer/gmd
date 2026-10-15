namespace gmd.Git.Private;

// A copy of the repository's index for a command to stage into, so that what the user staged is not
// touched: git reads GIT_INDEX_FILE for which index to use, as 'git stash -u' does for the same
// reason. A copy rather than a new, empty index, so that git's record of which files are unchanged
// since it last looked carries over, and it need not read every file in the working tree. A repo
// that has never staged anything has no index to copy, and git creates one.
static class TempIndex
{
    // Runs 'use' with the environment that points git at the copy, which is deleted afterwards
    public static async Task<Result<T>> WithCopyAsync<T>(
        string wd,
        Func<IReadOnlyDictionary<string, string>, Task<Result<T>>> use
    )
    {
        var tempIndex = Path.Join(Path.GetTempPath(), $"gmd-index-{Guid.NewGuid():N}");
        try
        {
            var index = Path.Join(GitDir.GitDirPathOf(wd), "index");
            if (File.Exists(index) && Result.Catch(() => File.Copy(index, tempIndex)) is Error copyError)
                return new Error("Failed to copy the index", copyError);

            return await use(new Dictionary<string, string> { ["GIT_INDEX_FILE"] = tempIndex });
        }
        finally
        {
            // Git writes the copy through a lock file beside it, left behind only if git was killed
            string[] paths = [tempIndex, $"{tempIndex}.lock"];
            foreach (var path in paths)
            {
                if (Result.Catch(() => File.Delete(path)) is Error e)
                    Log.Warn($"Failed to delete {path}, {e}");
            }
        }
    }
}
