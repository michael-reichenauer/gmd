namespace gmdE2eTest.Demo;

// The repository the demo animation shows: a small web shop that four people work on, with the
// shapes that make gmd worth watching. Merged and still open feature branches, one deleted after
// its merge, tags, a merge of main into a feature branch, and an origin that the current branch
// is ahead of and main is behind.
//
// The current branch, 'feature/login', is where the demo commits: it has uncommitted changes, and
// it was branched off main so that the other branches start out hidden, leaving only the ┣╮
// markers of the ones merged into main. The dates are pinned, as for every E2eRepo fixture, so the
// ids, the time column and the order of the rows are the same on every run.
//
//   main              Update dependencies            (origin only, i.e. main is behind)
//   feature/login     Show login errors              (not pushed, i.e. ahead)
//   feature/login     Remember me option
//   feature/dark-mode Add theme toggle
//   feature/login     Merge branch 'main' into feature/login
//   feature/search    Highlight search matches
//   feature/dark-mode Add dark theme
//   main              Merge branch 'bugfix/cart-total'           v1.1
//   feature/search    Search by category
//   feature/login     Validate email and password
//   feature/login     Add login page
//   feature/search    Add search box
//   bugfix/cart-total Fix rounding of cart total
//   main              Add install steps to README                v1.0
//   main              Merge branch 'feature/checkout'            (branch deleted since)
//   main              Add product images
//   feature/checkout  Add checkout form
//   feature/checkout  Add shopping cart
//   main              Add product catalog
//   main              Initial project setup
static class DemoRepo
{
    // The end of the path is what the application bar shows, the last 30 characters of it, so this
    // is exactly that long and starts with a '/': everything before it is the temp folder's guid.
    public const string RelativePath = "home/anna/projects/acme-store";

    // Who the repository belongs to, i.e. who gmd commits as, and whose branches 'My Active' lists
    const string Owner = "Anna Berg";

    static readonly DateTimeOffset T = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    // The demo's 'now', a little after the last commit. The uncommitted row is dated DateTime.Now,
    // so the recording shows this instead, see DemoTest.
    public static readonly DateTimeOffset Now = T.AddDays(8).AddHours(7).AddMinutes(12);

    public static async Task<TempRepo> CreateAsync()
    {
        var repo = await TempRepo.CreateAsync(RelativePath);
        Assert.AreEqual(30, $"/{RelativePath}".Length, "The application bar would show part of the guid");

        await repo.GitAsync($"config user.name \"{Owner}\"");
        await repo.GitAsync("config user.email anna@acme.example");

        Commit(repo, "Anna Berg", T, "Initial project setup", ("README.md", Readme), ("src/app.js", App));
        Commit(repo, "Anna Berg", T.AddHours(2.3), "Add product catalog", ("src/catalog.js", Catalog));

        await repo.GitAsync("checkout -q -b feature/checkout");
        Commit(repo, "Oskar Lind", T.AddDays(1).AddHours(1.1), "Add shopping cart", ("src/cart.js", Cart));
        Commit(repo, "Oskar Lind", T.AddDays(1).AddHours(6.5), "Add checkout form", ("src/checkout.js", Checkout));

        await repo.GitAsync("checkout -q main");
        Commit(repo, "Maya Chen", T.AddDays(2).AddHours(0.7), "Add product images", ("src/images.js", Images));
        Merge(repo, "Anna Berg", T.AddDays(2).AddHours(5.2), "feature/checkout", "Merge branch 'feature/checkout'");
        await repo.GitAsync("branch -q -d feature/checkout");
        Commit(repo, "Anna Berg", T.AddDays(3).AddHours(0.25), "Add install steps to README", ("README.md", Readme2));
        await repo.GitAsync("tag v1.0");

        await repo.GitAsync("checkout -q -b bugfix/cart-total");
        Commit(repo, "Leo Martin", T.AddDays(3).AddHours(4), "Fix rounding of cart total", ("src/cart.js", Cart2));

        await repo.GitAsync("checkout -q -b feature/search main");
        Commit(repo, "Maya Chen", T.AddDays(4).AddHours(1.5), "Add search box", ("src/search.js", Search));

        await repo.GitAsync("checkout -q -b feature/login main");
        Commit(repo, "Anna Berg", T.AddDays(4).AddHours(5), "Add login page", ("src/login.js", Login));
        Commit(repo, "Anna Berg", T.AddDays(5).AddHours(0.5), "Validate email and password", ("src/login.js", Login2));

        await repo.GitAsync("checkout -q feature/search");
        Commit(repo, "Maya Chen", T.AddDays(5).AddHours(2), "Search by category", ("src/search.js", Search2));

        await repo.GitAsync("checkout -q main");
        Merge(repo, "Anna Berg", T.AddDays(5).AddHours(7), "bugfix/cart-total", "Merge branch 'bugfix/cart-total'");
        await repo.GitAsync("tag v1.1");

        await repo.GitAsync("checkout -q -b feature/dark-mode");
        Commit(repo, "Leo Martin", T.AddDays(6).AddHours(1), "Add dark theme", ("src/theme.css", Theme));

        await repo.GitAsync("checkout -q feature/search");
        Commit(repo, "Maya Chen", T.AddDays(6).AddHours(5.3), "Highlight search matches", ("src/search.js", Search3));

        await repo.GitAsync("checkout -q feature/login");
        Merge(repo, "Anna Berg", T.AddDays(6).AddHours(7), "main", "Merge branch 'main' into feature/login");

        await repo.GitAsync("checkout -q feature/dark-mode");
        Commit(repo, "Leo Martin", T.AddDays(7).AddHours(0), "Add theme toggle", ("src/theme.js", ThemeToggle));

        await repo.GitAsync("checkout -q feature/login");
        Commit(repo, "Anna Berg", T.AddDays(7).AddHours(0.2), "Remember me option", ("src/login.js", Login3));

        // Everything so far is on origin, which is the point where the two sides part: one commit
        // of feature/login that is not pushed yet, and one on origin's main that is not pulled
        await repo.AddOriginAsync();
        await repo.GitAsync("push -q -u origin --all");
        await repo.GitAsync("push -q origin --tags");

        Commit(repo, "Anna Berg", T.AddDays(7).AddHours(2.75), "Show login errors", ("src/login-form.js", LoginForm));

        await repo.GitAsync("checkout -q main");
        Commit(repo, "Oskar Lind", T.AddDays(7).AddHours(4.5), "Update dependencies", ("package.json", Package));
        await repo.GitAsync("push -q origin main");
        await repo.GitAsync("reset -q --hard HEAD~1");

        // And the work in progress the demo commits
        await repo.GitAsync("checkout -q feature/login");
        repo.WriteFile("src/login-form.js", LoginForm2);

        return repo;
    }

    // A commit by one of the team, with its dates pinned (see TempRepo.CommitAtAsync)
    static void Commit(
        TempRepo repo,
        string author,
        DateTimeOffset time,
        string message,
        params (string Name, string Text)[] files
    )
    {
        foreach (var (name, text) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Join(repo.Path, name))!);
            repo.WriteFile(name, text);
        }

        Proc.Ok("git", ["add", "-A"], repo.Path, Env(author, time));
        Proc.Ok("git", ["commit", "-q", "-m", message, "--no-verify", "--no-gpg-sign"], repo.Path, Env(author, time));
    }

    static void Merge(TempRepo repo, string author, DateTimeOffset time, string branch, string message) =>
        Proc.Ok("git", ["merge", "-q", "--no-ff", "-m", message, branch], repo.Path, Env(author, time));

    static IReadOnlyDictionary<string, string> Env(string author, DateTimeOffset time)
    {
        var email = $"{author.Split(' ')[0].ToLowerInvariant()}@acme.example";
        var date = TempRepo.GitDate(time);
        return new Dictionary<string, string>
        {
            ["GIT_AUTHOR_NAME"] = author,
            ["GIT_AUTHOR_EMAIL"] = email,
            ["GIT_COMMITTER_NAME"] = author,
            ["GIT_COMMITTER_EMAIL"] = email,
            ["GIT_AUTHOR_DATE"] = date,
            ["GIT_COMMITTER_DATE"] = date,
        };
    }

    const string Readme = """
        # Acme Store

        The Acme online store.

        """;

    const string Readme2 = """
        # Acme Store

        The Acme online store.

        ## Install

            npm install
            npm start

        """;

    const string Package = """
        {
          "name": "acme-store",
          "version": "1.1.0",
          "dependencies": {
            "express": "^5.1.0"
          }
        }

        """;

    const string App = """
        import { catalog } from './catalog.js';

        export function start() {
          catalog.render();
        }

        """;

    const string Catalog = """
        export const catalog = {
          render() {
            return products.map(p => `<li>${p.name}</li>`);
          },
        };

        """;

    const string Images = """
        export function imageUrl(product, size = 'small') {
          return `/images/${product.id}-${size}.jpg`;
        }

        """;

    const string Cart = """
        export function total(items) {
          return items.reduce((sum, i) => sum + i.price * i.count, 0);
        }

        """;

    const string Cart2 = """
        export function total(items) {
          const sum = items.reduce((s, i) => s + i.price * i.count, 0);
          return Math.round(sum * 100) / 100;
        }

        """;

    const string Checkout = """
        export function checkout(cart, address) {
          return api.post('/orders', { items: cart.items, address });
        }

        """;

    const string Search = """
        export function search(products, query) {
          const q = query.trim().toLowerCase();
          return products.filter(p => p.name.toLowerCase().includes(q));
        }

        """;

    const string Search2 = """
        export function search(products, query, category) {
          const q = query.trim().toLowerCase();
          return products
            .filter(p => !category || p.category === category)
            .filter(p => p.name.toLowerCase().includes(q));
        }

        """;

    const string Search3 = """
        export function search(products, query, category) {
          const q = query.trim().toLowerCase();
          return products
            .filter(p => !category || p.category === category)
            .filter(p => p.name.toLowerCase().includes(q))
            .map(p => ({ ...p, html: highlight(p.name, q) }));
        }

        function highlight(text, q) {
          const i = text.toLowerCase().indexOf(q);
          return i < 0 ? text : `${text.slice(0, i)}<mark>${q}</mark>${text.slice(i + q.length)}`;
        }

        """;

    const string Login = """
        export function login(email, password) {
          return api.post('/login', { email, password });
        }

        """;

    const string Login2 = """
        export function login(email, password) {
          if (!email.includes('@')) throw new Error('Invalid email');
          if (password.length < 8) throw new Error('Password too short');
          return api.post('/login', { email, password });
        }

        """;

    const string Login3 = """
        export function login(email, password, remember = false) {
          if (!email.includes('@')) throw new Error('Invalid email');
          if (password.length < 8) throw new Error('Password too short');
          return api.post('/login', { email, password, remember });
        }

        """;

    const string LoginForm = """
        export function showError(form, error) {
          form.querySelector('.error').textContent = error.message;
        }

        """;

    const string LoginForm2 = """
        export function showError(form, error) {
          form.querySelector('.error').textContent = error.message;
        }

        export function showResetLink(form) {
          form.querySelector('.reset').href = '/reset-password';
        }

        """;

    const string Theme = """
        :root { --bg: #ffffff; --fg: #222222; }
        .dark { --bg: #1e1e1e; --fg: #eeeeee; }

        """;

    const string ThemeToggle = """
        export function toggleTheme() {
          document.body.classList.toggle('dark');
        }

        """;
}
