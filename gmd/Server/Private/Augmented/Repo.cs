namespace gmd.Server.Private.Augmented;


// record Repo
// {
//     public Repo(
//         DateTime timeStamp,
//         string path,
//         IReadOnlyList<Commit> commits,
//         IReadOnlyList<Branch> branches,
//         IReadOnlyList<Stash> stashes,
//         Status status)
//     {
//         TimeStamp = timeStamp;
//         Path = path;
//         Commits = commits;
//         CommitById = commits.ToDictionary(c => c.Id, c => c);
//         Stashes = stashes;
//         Status = status;
//         Branches = branches.ToDictionary(b => b.Name, b => b);
//     }

//     public DateTime TimeStamp { get; }
//     public string Path { get; }
//     public IReadOnlyList<Commit> Commits { get; }
//     public IReadOnlyDictionary<string, Commit> CommitById { get; }
//     public IReadOnlyList<Stash> Stashes { get; }
//     public IReadOnlyDictionary<string, Branch> Branches { get; }
//     public Status Status { get; init; }

//     public static Repo Empty => new Repo(
//         DateTime.UtcNow,
//         "",
//         new List<Commit>(),
//         new List<Branch>(),
//         new List<Stash>(),
//         new Status(0, 0, 0, 0, 0, false, "", "", new string[0], new string[0], new string[0], new string[0], new string[0], new string[0]));

//     public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status} @{TimeStamp.IsoMs()}";
// }

