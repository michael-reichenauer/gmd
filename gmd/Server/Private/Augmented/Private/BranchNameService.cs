using System.Text.RegularExpressions;

namespace gmd.Server.Private.Augmented.Private;

interface IBranchNameService
{
    void StartRead(WorkRepo repo);
    void ParseCommitSubject(WorkCommit c);
    bool IsPullMerge(WorkCommit c);
    bool TryGetBranchName(string commitId, out string branchName);
    string MergedFrom(WorkCommit c);
    bool IsPullRequest(WorkCommit c);
    bool IsMergeOfInto(WorkCommit c, string from);
    bool IsWeakName(string commitId);
    bool IsOwnName(string commitId);
    void ParentsSwapped(WorkCommit c);
}

// What a merge subject says: the branch merged in and the one merged into. A weak name merged in is
// a tag's, or a word the subject neither quotes nor puts after a keyword, see IsWeakName.
record FromInto(string From, string Into, bool IsPullMerge, bool IsPullRequest, bool IsWeak = false);

record Indexes(int from, int into, int direction);

// Parses merge commit subjects to recover the branch names git no longer has, and remembers what
// it found: ParseCommitSubject is called by one stage of the pipeline (CommitGraphService) and the
// names are looked up by the later stages (CommitBranchService, CommitBranchRules), so all of them
// must share the one instance holding the cache. Hence the single instance: resolved per consumer,
// the later stages would each get an empty cache and every deleted branch would lose its name.
// What is remembered is one read's, cleared before the next (see BranchStructureService), since a
// stage changes it for the read's own graph (ParentsSwapped).
// cspell:ignore erged
[SingleInstance]
class BranchNameService : IBranchNameService
{
    readonly Dictionary<string, FromInto> parsedCommits = [];
    readonly Dictionary<string, string> branchNames = [];
    readonly HashSet<string> weakNamed = [];
    readonly HashSet<string> ownNamed = [];

    readonly FromInto noNames = new FromInto("", "", false, false);

    // Whether the repo read has branches of both trunk names, see IsMatchPullMergeOfTrunk
    bool hasBothTrunks = false;

    // The repo read, and where the first-parent line of each of its commits ends, see IsOneLine
    WorkRepo? repo;
    Dictionary<string, string>? lineEnds;

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
        $@"\s+'?(?:[0-9A-Za-z_.-]+:)?(?<from>{namePattern})'?"
        + //          the <from> branch name, without GitHub's 'owner:' of another repository's branch
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

    // Starts the read of a repo: forgets the names of the read before, and notes whether the repo has
    // branches of both trunk names, which git's merge subjects cannot tell apart
    public void StartRead(WorkRepo repo)
    {
        parsedCommits.Clear();
        branchNames.Clear();
        weakNamed.Clear();
        ownNamed.Clear();
        hasBothTrunks =
            repo.Branches.Values.Any(b => b.NiceName == "main")
            && repo.Branches.Values.Any(b => b.NiceName == "master");
        this.repo = repo;
        lineEnds = null;
    }

    public void ParseCommitSubject(WorkCommit c)
    {
        ParseCommit(c);
    }

    public bool TryGetBranchName(string commitId, out string branchName)
    {
        return branchNames.TryGetValue(commitId, out branchName!) && branchName != "";
    }

    // The name of the branch a merge commit merged in, by its subject, or "" if it names none, or
    // if it is a pull merge, which merges a branch into itself
    public string MergedFrom(WorkCommit c)
    {
        var fi = ParseCommit(c);
        return IsPullMergeCommit(fi) ? "" : fi.From;
    }

    // Whether a merge commit's subject says the branch 'from' was merged into another named branch,
    // e.g. "Merge branch 'main' into feature" for 'main'
    public bool IsMergeOfInto(WorkCommit c, string from)
    {
        var fi = ParseCommit(c);
        return fi.From == from && fi.Into != "" && fi.Into != from;
    }

    // The parents of a merge were swapped after its subject was parsed (a foxtrot merge, see
    // CommitGraphService), so the names are pointed the other way, as if the subject had said it:
    // the merge is on the branch that was merged in, and its other parent on the branch merged into
    public void ParentsSwapped(WorkCommit c)
    {
        var fi = ParseCommit(c);
        var swapped = fi with { From = fi.Into, Into = fi.From };
        parsedCommits[c.Id] = swapped;
        branchNames[c.Id] = swapped.Into;
        branchNames[c.ParentIds[1]] = swapped.From;
        weakNamed.Remove(c.Id);
        weakNamed.Remove(c.ParentIds[1]);
        ownNamed.Add(c.Id);
        ownNamed.Remove(c.ParentIds[1]);
    }

    // Whether the name of a commit is only a weak one, which names the line merged in when nothing else
    // does, but is no evidence of a branch: a tag's, e.g. git-flow's release tag merged into develop,
    // or a word of a subject that is no branch's as often as it is, e.g. "Merged conflicts with dev"
    // or "Merge latest dev into x". Git, GitHub and GitLab all quote a branch or name it after a keyword.
    public bool IsWeakName(string commitId) => weakNamed.Contains(commitId);

    // Whether the name of a commit is from its own subject, "Merge branch 'a' into b", rather than
    // from a merge child's, of which it was the tip
    public bool IsOwnName(string commitId) => ownNamed.Contains(commitId);

    // Whether a merge commit's subject is a pull request's, e.g. 'Merge pull request #1 from x/y'
    public bool IsPullRequest(WorkCommit c) => ParseCommit(c).IsPullRequest;

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

        fi = ParseSubject(c.Subject, () => IsOneLine(c));

        // Some child commit might have already specified the branch name of this commit
        // E.g. a child, which merged from this commit might have a subject like
        // 'Merge from some-branch'
        var name = branchNames.TryGetValue(c.Id, out var n) ? n : "";
        var isOwnName = fi.Into != "";

        if (fi.Into != "")
        { // Subject does specify own commit, lets check if it is matches a possible child commit
            // subject
            if (name != fi.Into && name.EndsWith("/" + fi.Into))
            { // The child branch name is the into value with an owner in front, e.g. 'owner/dev' from a
                // pull request, the same branch, so we can use the child branch name
                fi = fi with
                {
                    Into = name,
                };
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
        if (isOwnName)
        {
            weakNamed.Remove(c.Id);
            ownNamed.Add(c.Id);
        }

        if (IsPullMergeCommit(fi))
        {
            // The order of the parents will be switched for a pull merge and thus
            // Set the first branch name of the first parent
            branchNames[c.ParentIds[0]] = fi.From;
            weakNamed.Remove(c.ParentIds[0]);
        }
        else if (fi.IsWeak)
        { // A weak name merged in names the merged commit only when no other has named it
            if (branchNames.GetValueOrDefault(c.ParentIds[1], "") == "" || weakNamed.Contains(c.ParentIds[1]))
            {
                branchNames[c.ParentIds[1]] = fi.From;
                weakNamed.Add(c.ParentIds[1]);
            }
        }
        else
        { // Normal commit, set the branch name for the second (other) parent
            branchNames[c.ParentIds[1]] = fi.From;
            weakNamed.Remove(c.ParentIds[1]);
        }

        return fi;
    }

    public FromInto ParseSubject(string subject) => ParseSubject(subject, () => true);

    // Parses a subject, and when it reads as a pull merge, asks whether the two lines merged are one
    // branch's, which only the graph can tell
    FromInto ParseSubject(string subject, Func<bool> isOneLine)
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

        if (IsMatchMergeTag(match))
        { // 'git pull <url> tag <tag>' writes "Merge tag 'v1.0' of <url>", which reads as a pull merge,
            // but a tag is no branch, merged into itself or not, so a tag's merge is never one. Swapped
            // as one, it ran git's master into the history of the repository the tag was pulled from.
            return new FromInto(
                From: TrimBranchName(match.Groups[indexes.from].Value),
                Into: TrimBranchName(match.Groups[indexes.into].Value),
                false,
                false,
                IsWeak: true
            );
        }

        if (IsMatchPullMerge(match) || (!hasBothTrunks && IsMatchPullMergeOfTrunk(match)))
        {
            if (!isOneLine())
            { // Reads as a pull merge, but the lines merged are not one branch's: a branch of another
                // repository merged in, e.g. "Merge branch 'master' of https://github.com/j6t/gitk" into
                // git's master. Named after that repository, e.g. 'gitk/master', as a pull request's
                // branch is after its owner, and left in git's order, since the swap would run this
                // branch's line into the other repository's history.
                var name = TrimBranchName(match.Groups[indexes.from].Value);
                var repository = RepositoryName(match.Groups[indexes.direction].Value);
                return new FromInto(
                    From: repository != "" ? $"{repository}/{name}" : match.Groups[indexes.from].Value,
                    Into: TrimBranchName(match.Groups[indexes.into].Value),
                    false,
                    false
                );
            }

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
            false,
            IsWeak: !IsMatchQuotedOrKeyword(match)
        );
    }

    // Whether the first-parent lines of a merge's two parents, in git's order, meet in the log. The
    // local and the remote line of a branch meet where they parted, a while before the pull. A branch
    // of another repository, whose history was merged in as a project of its own (gitk and git-gui in
    // git, sinatra-contrib in sinatra), has a line of its own that never meets this one. Neither do
    // two lines that part below where a log is cut off, which is taken as one branch, as before.
    bool IsOneLine(WorkCommit c)
    {
        if (repo == null || c.ParentIds.Any(id => id == Repo.TruncatedLogCommitId))
            return true;

        var ends = lineEnds ??= LineEnds(repo);
        return ends.GetValueOrDefault(c.ParentIds[0]) is string end && end == ends.GetValueOrDefault(c.ParentIds[1]);
    }

    // The oldest commit of each commit's first-parent line that is in the log, in git's parent order,
    // which is what the commits have until the pull merges are swapped. Made at the first pull merge,
    // whose parents' lines are of older commits than any swapped before it.
    static Dictionary<string, string> LineEnds(WorkRepo repo)
    {
        Dictionary<string, string> ends = [];
        for (var i = repo.Commits.Count - 1; i >= 0; i--)
        { // Oldest first, so a commit's first parent has its end before the commit does
            var c = repo.Commits[i];
            ends[c.Id] =
                c.ParentIds.Count > 0
                && c.ParentIds[0] != Repo.TruncatedLogCommitId
                && ends.TryGetValue(c.ParentIds[0], out var end)
                    ? end
                    : c.Id;
        }
        return ends;
    }

    // The name of a repository from the 'of <url>' of a merge subject, e.g. 'gitk' for
    // ' of https://github.com/j6t/gitk' or ' of github.com:git-l10n/git-po.git', or "" for none
    static string RepositoryName(string direction)
    {
        var url = direction.Trim();
        url = url.StartsWith("of") ? url[2..].Trim().TrimEnd('/') : "";
        var name = url[(url.LastIndexOfAny(['/', ':']) + 1)..];
        name = name.EndsWith(".git") ? name[..^4] : name;
        return name.Trim('.') == "" ? "" : name; // Not 'git pull .', whose 'of .' names no repository
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

    // 'git merge origin/main' on main, a pull merge made by hand. Git leaves 'into main' and 'into
    // master' out of a merge subject, so this is the remote-tracking name of the trunk merged with no
    // target named. Only this remote's: another remote's main is a fork's upstream. And only in a repo
    // with one trunk name: with both, e.g. moving from master to main, 'git merge origin/master' on
    // main writes the same subject, and it is master merged into main.
    bool IsMatchPullMergeOfTrunk(Match match)
    {
        var from = match.Groups[indexes.from].Value;
        var name = TrimBranchName(from);
        return name != from
            && match.Groups[indexes.into].Value == ""
            && match.Groups[indexes.direction].Value == ""
            && name is "main" or "master";
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

    // Whether the name merged in is quoted, "Merge branch 'dev'", or follows a keyword, "Merge pull
    // request #1 from x/dev", rather than being the word after "Merge"
    static bool IsMatchQuotedOrKeyword(Match match)
    {
        var quote = match.Value.IndexOf('\'');
        return match.Groups["keyword"].Success || (quote >= 0 && quote < match.Groups["from"].Index - match.Index);
    }

    // "Merge tag 'v1.0'" names a tag rather than a branch
    static bool IsMatchMergeTag(Match match) =>
        match.Groups["keyword"].Value.Equals("tag", StringComparison.OrdinalIgnoreCase);

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
