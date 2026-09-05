using System.Text.RegularExpressions;

namespace gmd.Server.Private.Augmented.Private;

interface IBranchNameService
{
    void ParseCommitSubject(WorkCommit c);
    bool IsPullMerge(WorkCommit c);
    bool TryGetBranchName(string commitId, out string branchName);
}

record FromInto(string From, string Into, bool IsPullMerge, bool IsPullRequest);

record Indexes(int from, int into, int direction);

// Parses merge commit subjects to recover the branch names git no longer has, and remembers what
// it found: ParseCommitSubject is called by one stage of the pipeline (CommitGraphService) and the
// names are looked up by the later stages (CommitBranchService, CommitBranchRules), so all of them
// must share the one instance holding the cache. Hence the single instance: resolved per consumer,
// the later stages would each get an empty cache and every deleted branch would lose its name.
// cspell:ignore erged
[SingleInstance]
class BranchNameService : IBranchNameService
{
    readonly Dictionary<string, FromInto> parsedCommits = [];
    readonly Dictionary<string, string> branchNames = [];

    readonly FromInto noNames = new FromInto("", "", false, false);

    static readonly string[] prefixes = ["refs/remotes/origin/", "remotes/origin/", "origin/"];

    // A branch name may contain dots, e.g. 'release/1.0', but a git ref can neither start nor end
    // with one, so the name is bounded by a non-dot character. That also keeps a sentence ending
    // like "Merged from dev." from taking the period as part of the name.
    const string namePattern = @"[0-9A-Za-z_/-](?:[0-9A-Za-z_/.-]*[0-9A-Za-z_/-])?";

    // Parse subject like e.g. "Merge branch 'develop' into main".
    // Only the parts that are used are captured, the rest are non-capturing groups, so that adding
    // a group cannot shift the meaning of another one.
    static readonly string regExText =
        @"[Mm]erged?"
        + //                                 'Merge' or 'merged' word
        @"(?:\s+remote-tracking)?"
        + //                    'remote-tracking' optional word when merging remote branches
        @"(?:\s+(?<keyword>pull request #[0-9]+ from|PR|from branch|branches|branch|commit|tag|from))?"
        + // 'branch'|'commit'|'tag'|'from' word ('branches' before 'branch' to match the longer one)
        $@"\s+'?(?<from>{namePattern})'?"
        + //          the <from> branch name
        $@"(?:(?:\s*,|\s+and)\s+'?{namePattern}'?)*"
        + // the other names of an octopus merge, e.g. "branches 'a', 'b' and 'c'"
        @"(?<direction>\s+of\s+[^\s]+)?"
        + //             the optional 'of repo url'
        $@"(?:\s+(?:into|to)\s+'?(?<into>{namePattern})'?)?"; // the <into> branch name, quoted by GitLab

    static readonly Regex branchesRegEx = new Regex(
        regExText,
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );
    static readonly Indexes indexes = NameRegExpIndexes();

    public void ParseCommitSubject(WorkCommit c)
    {
        ParseCommit(c);
    }

    public bool TryGetBranchName(string commitId, out string branchName)
    {
        return branchNames.TryGetValue(commitId, out branchName!) && branchName != "";
    }

    public bool IsPullMerge(WorkCommit c)
    {
        var fi = ParseCommit(c);
        return fi.IsPullMerge;
    }

    FromInto ParseCommit(WorkCommit c)
    {
        if (c.ParentIds.Count != 2)
        {
            return noNames;
        }

        if (parsedCommits.TryGetValue(c.Id, out var fi))
        { // Already parsed this commit,use the cached result
            return fi;
        }

        fi = ParseSubject(c.Subject);

        // Some child commit might have already specified the branch name of this commit
        // E.g. a child, which merged from this commit might have a subject like
        // 'Merge from some-branch'
        var name = branchNames.TryGetValue(c.Id, out var n) ? n : "";

        if (fi.Into != "")
        { // Subject does specify own commit, lets check if it is matches a possible child commit
            // subject
            if (name != fi.Into && name.EndsWith(fi.Into))
            { // The child branch name is a prefix of the into value, so we can use the child branch name
                fi = fi with { Into = name };
            }
        }
        else
        { // Commit subject did not have an into value specifying the branch name of this commit
            if (name != "")
            { // Some child subject contained info about this commits branch name so we can use that
                fi = fi with { Into = name };
            }
        }

        // Cache the result
        parsedCommits[c.Id] = fi;
        branchNames[c.Id] = fi.Into;

        if (IsPullMergeCommit(fi))
        {
            // The order of the parents will be switched for a pull merge and thus
            // Set the first branch name of the first parent
            branchNames[c.ParentIds[0]] = fi.From;
        }
        else
        { // Normal commit, set the branch name for the second (other) parent
            branchNames[c.ParentIds[1]] = fi.From;
        }

        return fi;
    }

    public FromInto ParseSubject(string subject)
    {
        subject = subject.Trim();
        var matches = branchesRegEx.Matches(subject);

        if (matches.Count == 0)
        {
            return noNames;
        }
        var match = matches[0];

        if (IsMatchMergeCommit(match))
        { // 'git merge <sha>' writes "Merge commit '<sha>' into x". A commit id is not a branch, so
            // it says nothing about the branch the merged commit was on, only about the target
            return new FromInto(From: "", Into: TrimBranchName(match.Groups[indexes.into].Value), false, false);
        }

        if (IsMatchPullMerge(match))
        {
            // Subject is a pull merge same branch from remote repo (same remote source and target branch)
            return new FromInto(
                From: TrimBranchName(match.Groups[indexes.from].Value),
                Into: TrimBranchName(match.Groups[indexes.from].Value),
                true,
                false
            );
        }

        if (IsMatchPullRequest(match))
        {
            // Subject is a pull request
            var fr = TrimBranchName(match.Groups[indexes.from].Value);
            if (match.Groups["keyword"].Value == "PR")
            { // Azure DevOps does not name the branch, so the pull request number is used instead
                fr = "PR" + fr;
            }
            return new FromInto(From: fr, Into: TrimBranchName(match.Groups[indexes.into].Value), false, true);
        }

        return new FromInto(
            From: TrimBranchName(match.Groups[indexes.from].Value),
            Into: TrimBranchName(match.Groups[indexes.into].Value),
            false,
            false
        );
    }

    bool IsPullMergeCommit(FromInto fi)
    {
        return fi.From != "" && fi.From == fi.Into;
    }

    string TrimBranchName(string name)
    {
        foreach (var prefix in prefixes)
        {
            if (name.StartsWith(prefix))
            {
                return name.Substring(prefix.Length);
            }
        }

        return name;
    }

    bool IsMatchPullMerge(Match match)
    {
        if (
            match.Groups[indexes.from].Value != ""
            && match.Groups[indexes.direction].Value != ""
            && (
                match.Groups[indexes.into].Value == ""
                || match.Groups[indexes.into].Value == match.Groups[indexes.from].Value
            )
        )
        {
            return true;
        }

        if (
            match.Groups[indexes.from].Value != ""
            && match.Groups[indexes.into].Value != ""
            && TrimBranchName(match.Groups[indexes.from].Value) == TrimBranchName(match.Groups[indexes.into].Value)
        )
        {
            return true;
        }

        return false;
    }

    // "Merge commit '<sha>'" names a commit rather than a branch, which is what git writes when a
    // commit is merged by id
    static bool IsMatchMergeCommit(Match match) =>
        match.Groups["keyword"].Value.Equals("commit", StringComparison.OrdinalIgnoreCase);

    bool IsMatchPullRequest(Match match)
    {
        if (match.Groups[0].Value.StartsWith("Merge pull request"))
        {
            return true;
        }
        if (match.Groups[0].Value.StartsWith("Merged PR "))
        {
            return true;
        }

        return false;
    }

    // nameRegExpIndexes returns the named group indexes to be used in parse
    static Indexes NameRegExpIndexes()
    {
        int fromIndex = 0;
        int intoIndex = 0;
        int directionIndex = 0;

        var n1 = branchesRegEx.GetGroupNames();

        for (int i = 0; i < n1.Length; i++)
        {
            string v = n1[i];
            if (v == "from")
            {
                fromIndex = i;
            }
            if (v == "into")
            {
                intoIndex = i;
            }
            if (v == "direction")
            {
                directionIndex = i;
            }
        }
        return new Indexes(fromIndex, intoIndex, directionIndex);
    }
}
