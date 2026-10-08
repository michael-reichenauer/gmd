using System.Text.RegularExpressions;

namespace gmd.Server;

// What was typed into the search, split into the terms that must all match. A "quoted phrase" is
// one term, spaces and all. A term starting with 'file:' is a path instead, matched against the
// files each commit changed, and one starting with 'change:' a text, matched against the changes
// themselves, i.e. the commits that added or removed it ('git log -S'). Only git can tell those, and
// it takes a moment in a large repo, so they are kept apart from the words matched against what the
// log already holds. 'file:' or 'change:' alone, as it is while being typed, is no term yet.
internal record SearchTerms(IReadOnlyList<string> Words, IReadOnlyList<string> Files, IReadOnlyList<string> Changes)
{
    const string FilePrefix = "file:";
    const string ChangePrefix = "change:";

    // The terms git is asked about rather than the log
    public bool IsAskingGit => Files.Count + Changes.Count > 0;

    public static SearchTerms Parse(string filter)
    {
        // The quoted phrases, with their spaces made newlines so that the split below keeps each
        // one whole, and then made spaces again
        var quoted = Regex.Matches(filter, "\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
        var modified = filter;
        quoted.ForEach(q => modified = modified.Replace($"\"{q}\"", q.Replace(" ", "\n")));
        var terms = modified.Split(' ').Where(p => p != "").Select(p => p.Replace("\n", " ")).ToList();

        var files = Of(terms, FilePrefix);
        var changes = Of(terms, ChangePrefix);
        var words = terms.Where(t => !Is(t, FilePrefix) && !Is(t, ChangePrefix)).ToList();
        return new SearchTerms(words, files, changes);
    }

    static List<string> Of(IEnumerable<string> terms, string prefix) =>
        terms.Where(t => Is(t, prefix)).Select(t => t[prefix.Length..].Trim('"')).Where(t => t != "").ToList();

    static bool Is(string term, string prefix) => term.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
