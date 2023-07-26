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


// cspell:ignore erged
class BranchNameService : IBranchNameService
{
    readonly Dictionary<string, FromInto> parsedCommits = new Dictionary<string, FromInto>();
    readonly Dictionary<string, string> branchNames = new Dictionary<string, string>();

    readonly FromInto noNames = new FromInto("", "", false, false);

    static readonly string[] prefixes = { "refs/remotes/origin/", "remotes/origin/", "origin/" };

    // Parse subject like e.g. "Merge branch 'develop' into main"
    static readonly string regExText =
        @"[Mm]erged?" + //                                 'Merge' or 'merged' word
        @"(\s+remote-tracking)?" + //                      'remote-tracking' optional word when merging remote branches
        @"(\s+(pull request #[0-9]+ from|PR|from branch|branch|commit|from))?" + //     'branch'|'commit'|'from' word
        @"\s+'?(?<from>[0-9A-Za-z_/-]+)'?" + //           the <from> branch name
        @"(?<direction>\s+of\s+[^\s]+)?" + //             the optional 'of repo url'
        @"(\s+(into|to)\s+(?<into>[0-9A-Za-z_/-]+))?"; // the <into> branch name

    static readonly Regex branchesRegEx = new Regex(regExText,
        RegexOptions.Compiled | RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);
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
        {   // Already parsed this commit,use the cached result
            return fi;
        }

        fi = ParseSubject(c.Subject);

        // Some child commit might have already specified the branch name of this commit
        // E.g. a child, which merged from this commit might have a subject like
        // 'Merge from some-branch'
        var name = branchNames.TryGetValue(c.Id, out var n) ? n : "";

        if (fi.Into != "")
        {   // Subject does specify own commit, lets check if it is matches a possible child commit
            // subject
            if (name != fi.Into && name.EndsWith(fi.Into))
            {   // The child branch name is a prefix of the into value, so we can use the child branch name
                fi = fi with { Into = name };
            }
        }
        else
        {   // Commit subject did not have an into value specifying the branch name of this commit
            if (name != "")
            {   // Some child subject contained info about this commits branch name so we can use that
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
        {   // Normal commit, set the branch name for the second (other) parent
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

        if (IsMatchPullMerge(match))
        {
            // Subject is a pull merge same branch from remote repo (same remote source and target branch)
            return new FromInto(
                From: TrimBranchName(match.Groups[indexes.from].Value),
                Into: TrimBranchName(match.Groups[indexes.from].Value),
                true,
                false);
        }

        if (IsMatchPullRequest(match))
        {
            // Subject is a pull request
            var fr = TrimBranchName(match.Groups[indexes.from].Value);
            if (match.Groups[3].Value == "PR")
            {
                fr = "PR" + fr;

            }
            return new FromInto(
                From: fr,
                Into: TrimBranchName(match.Groups[indexes.into].Value),
                false,
                true);
        }

        return new FromInto(
            From: TrimBranchName(match.Groups[indexes.from].Value),
            Into: TrimBranchName(match.Groups[indexes.into].Value),
            false,
            false);
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
        if (match.Groups[indexes.from].Value != "" &&
            match.Groups[indexes.direction].Value != "" &&
            (match.Groups[indexes.into].Value == "" ||
                 match.Groups[indexes.into].Value == match.Groups[indexes.from].Value))
        {
            return true;
        }

        if (match.Groups[indexes.from].Value != "" && match.Groups[indexes.into].Value != "" &&
            TrimBranchName(match.Groups[indexes.from].Value) == TrimBranchName(match.Groups[indexes.into].Value))
        {
            return true;
        }

        return false;
    }

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