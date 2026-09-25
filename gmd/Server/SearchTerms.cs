using System.Text.RegularExpressions;

namespace gmd.Server;

// What was typed into the search, split into the terms that must all match. A "quoted phrase" is
// one term, spaces and all. A term starting with 'file:' is a path instead, matched against the
// files each commit changed, which only git can tell and which takes it a moment in a large repo,
// so it is kept apart from the words matched against what the log already holds. 'file:' alone,
// as it is while being typed, is no term yet.
internal record SearchTerms(IReadOnlyList<string> Words, IReadOnlyList<string> Files)
{
    const string FilePrefix = "file:";

    public static SearchTerms Parse(string filter)
    {
        // The quoted phrases, with their spaces made newlines so that the split below keeps each
        // one whole, and then made spaces again
        var quoted = Regex.Matches(filter, "\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
        var modified = filter;
        quoted.ForEach(q => modified = modified.Replace($"\"{q}\"", q.Replace(" ", "\n")));
        var terms = modified.Split(' ').Where(p => p != "").Select(p => p.Replace("\n", " ")).ToList();

        var files = terms.Where(IsFile).Select(t => t[FilePrefix.Length..].Trim('"')).Where(t => t != "").ToList();
        return new SearchTerms(terms.Where(t => !IsFile(t)).ToList(), files);
    }

    public static bool HasFiles(string filter) => Parse(filter).Files.Count > 0;

    static bool IsFile(string term) => term.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase);
}
