using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// What git and ssh ask, a passphrase, a password or whether to trust a host, asked in a dialog of
// gmd: git gets no terminal to ask on, ssh asks gmd as its askpass, and that asks the gmd that ran
// the git (Askpass, AskpassServer). Only for a command the user asked for: the fetch on opening,
// in the background, asks nothing and says what to do.
//
// The ssh is a stand-in, origin's core.sshCommand, which asks as OpenSSH does, SSH_ASKPASS when
// SSH_ASKPASS_REQUIRE is force and the terminal otherwise, and once the answer is right runs the git
// command it was given on the origin folder next door, as the server would. Git first runs it with
// -G, to tell which kind of ssh it is.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class LoginDlgTest
{
    const string Passphrase = "Enter passphrase for key '/home/anna/.ssh/id_ed25519': ";

    const string UnknownHost = """
        The authenticity of host 'example.invalid (192.0.2.1)' can't be established.
        ED25519 key fingerprint is SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU.
        This key is not known by any other names.
        Are you sure you want to continue connecting (yes/no/[fingerprint])?
        """;

    // A repo one commit ahead of an origin that the stand-in ssh serves once asked and answered
    static async Task<TempRepo> CreateAsync(string question, string answer)
    {
        var repo = await E2eRepo.CreateWithOriginAsync();
        var ssh = Path.Join(repo.Path, ".git", "stand-in-ssh");
        File.WriteAllText(
            ssh,
            $$"""
            #!/bin/sh
            [ "$1" = "-G" ] && exit 0
            for last; do :; done
            question=$(cat <<'END'
            {{question}}
            END
            )
            if [ "$SSH_ASKPASS_REQUIRE" = "force" ] && [ -n "$SSH_ASKPASS" ]; then
                answer=$("$SSH_ASKPASS" "$question")
            else
                printf '%s' "$question" > /dev/tty
                read answer < /dev/tty
            fi
            if [ "$answer" != '{{answer}}' ]; then
                echo "git@example.invalid: Permission denied (publickey)." >&2
                exit 255
            fi
            exec sh -c "git ${last#git-}"

            """
        );
        File.SetUnixFileMode(ssh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await repo.GitAsync($"remote set-url origin \"ssh://git@example.invalid{repo.Path}-origin\"");
        await repo.GitAsync($"config core.sshCommand \"{ssh}\"");
        return repo;
    }

    static async Task<string> OriginMainAsync(TempRepo repo) =>
        (await repo.GitAsync($"-C \"{repo.Path}-origin\" rev-parse main")).Trim();

    // The fetch on opening asks nothing, and says that r would; a push asks, in a dialog with the
    // passphrase hidden, and pushes with it
    [TestMethod]
    public async Task TestAPassphraseIsAskedInADialog()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");
        using var repo = await CreateAsync(Passphrase, "secret");
        using var gmd = TmuxSession.StartGmd(repo);

        // The screen is in place, with the top bar as the first row, and nothing was asked
        var screen = gmd.WaitFor("The remote wants a login");
        StringAssert.StartsWith(ScreenText.Rows(screen, repo.Path, 0, 1), " Gmd {repo}, ●main, ▲1");
        Assert.AreEqual(
            "Fetch failed: The remote wants a login: r fetches and asks for it",
            ScreenText.LastLine(screen)
        );
        Assert.IsFalse(screen.Contains("Passphrase"), "Not asked by the fetch no one asked for");

        gmd.Send("p");
        var dialog = gmd.WaitFor("Enter passphrase");
        Assert.AreEqual(
            """
                                     ╭ Passphrase ────────────────────────────────────────────────────────╮
                                     │ Enter passphrase for key '/home/anna/.ssh/id_ed25519':             │
                                     │                                                                    │
                                     │ │                                                                │ │
                                     │ └────────────────────────────────────────────────────────────────┘ │
                                     │ Tip: ssh-add /home/anna/.ssh/id_ed25519 in a terminal, and it is   │
                                     │ not asked again                                                    │
                                     │ Asked by: git push origin refs/heads/main:refs/heads/main          │
                                     │                                                                    │
                                     │                        [◦ OK ◦] [ Cancel ]                         │
                                     ╰────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(dialog, repo.Path, 14, 11)
        );

        gmd.SendText("secret");
        Assert.IsFalse(gmd.WaitFor("******").Contains("secret"), "Hidden as it is typed");
        gmd.Send("Enter");

        Assert.AreEqual("Pushed 'main'", ScreenText.LastLine(gmd.WaitFor("Pushed 'main'")));
        Assert.AreEqual((await repo.GitAsync("rev-parse main")).Trim(), await OriginMainAsync(repo));
    }

    // Cancel, here Esc, says so on the status line, and nothing is pushed
    [TestMethod]
    public async Task TestACancelledLoginIsSaid()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");
        using var repo = await CreateAsync(Passphrase, "secret");
        var originMain = await OriginMainAsync(repo);
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("The remote wants a login");

        gmd.Send("p");
        gmd.WaitFor("Enter passphrase");
        gmd.Send("Escape");

        Assert.AreEqual("The login was cancelled", ScreenText.LastLine(gmd.WaitFor("The login was cancelled")));
        Assert.AreEqual(originMain, await OriginMainAsync(repo), "Nothing pushed");
    }

    // A host ssh has not met is trusted by Yes, which answers 'yes', as typed in a terminal. No is
    // the default, since trusting a host is a decision.
    [TestMethod]
    public async Task TestAnUntrustedHostIsAskedYesOrNo()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");
        using var repo = await CreateAsync(UnknownHost, "yes");
        using var gmd = TmuxSession.StartGmd(repo);
        Assert.AreEqual(
            "Fetch failed: The host example.invalid is not trusted yet: r fetches and asks whether to trust it",
            ScreenText.LastLine(gmd.WaitFor("is not trusted yet"))
        );

        gmd.Send("p");
        var dialog = gmd.WaitFor("Trust Host");
        Assert.AreEqual(
            """
                                ╭ Trust Host ──────────────────────────────────────────────────────────────────╮
                                │The authenticity of host 'example.invalid (192.0.2.1)' can't be               │
                                │established.                                                                  │
                                │ED25519 key fingerprint is                                                    │
                                │SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU.                           │
                                │This key is not known by any other names.                                     │
                                │Are you sure you want to continue connecting                                  │
                                │(yes/no/[fingerprint])?                                                       │
                                │                                                                              │
                                │Yes adds the host to ~/.ssh/known_hosts, as ssh does, so it is not asked again│
                                │                                                                              │
                                │Asked by: git push origin refs/heads/main:refs/heads/main                     │
                                │                                                                              │
                                │                               [ Yes ] [◦ No ◦]                               │
                                ╰──────────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(dialog, repo.Path, 12, 15)
        );
        gmd.Send("Left"); // From No, the default, to Yes
        gmd.WaitForStable();
        gmd.Send("Enter");

        gmd.WaitFor("Pushed 'main'");
        Assert.AreEqual((await repo.GitAsync("rev-parse main")).Trim(), await OriginMainAsync(repo));
    }
}
