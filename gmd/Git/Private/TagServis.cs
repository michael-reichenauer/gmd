namespace gmd.Git.Private;

interface ITagService
{
    Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd);
}


class TagService : ITagService
{
    private readonly ICmd cmd;

    internal TagService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd)
    {
        var args = "show-ref --dereference --tags";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        return ParseTags(output);
    }

    private R<IReadOnlyList<Tag>> ParseTags(string output)
    {
        List<Tag> tags = new List<Tag>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            if (line.Length < 51)
            {
                continue;
            }

            var commitID = line.Substring(0, 40);
            var name = line.Substring(51);

            // Seems that some client add a suffix for some reason
            name = name.TrimSuffix("^{}");

            tags.Add(new Tag(name, commitID));
        }

        return tags;
    }
}
