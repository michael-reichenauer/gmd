# Upgrading to the next .NET and C#

Gmd targets and runs on .NET 11 and is built with its SDK, whose C# 15 has the union types
`Result` is made of. .NET 11 was released on 2026-11-10, and gmd moved to it the day after: first
to the released SDK, then to the target framework net11.0. This is how that was done, to do again
for the next .NET. Every pin named here is found with:

```bash
grep -rn "net11.0\|11.0.x\|dotnet:11\|--channel\|LangVersion\|11.0.100" \
  --include=*.json --include=*.props --include=*.csproj --include=*.yml --include=installtools \
  --include=build --include=build.bat . | grep -v "/bin/\|/obj/"
```

## 1. Build with the next SDK, before or at its release

Only when a language feature of the next C# is wanted before its .NET is released, as the unions
of C# 15 were: gmd was built with the .NET 11 previews for months while it targeted net10.0.

1. `global.json`: set `sdk.version` to the preview's version and add `"allowPrerelease": true`; at
   the release, the GA version (`11.0.100` it was) with `rollForward: latestFeature` and no
   `allowPrerelease`.
2. `Directory.Build.props`: `<LangVersion>preview</LangVersion>`, at the release the version's
   number, since the language of an older target framework is not the SDK's newest by default.
3. `scripts/installtools` and `.devcontainer/devcontainer.json`: install the SDK beside the image's
   own, `dotnet-install.sh --channel <version> --install-dir /usr/share/dotnet` (with `--quality
   preview` until the release). A cloud session needs nothing: `scripts/cloud-setup` installs the
   version `global.json` pins and the runtime of the target framework.
4. CI (`.github/workflows/build.yml`, `release.yml` and `branch-graph.yml`): `global-json-file`
   picks the pin, and a `dotnet-version: '<target>.0.x'` line beside it adds the runtime of the
   target framework, which a newer SDK does not carry.
5. A type the compiler needs from the next base class library is declared by hand meanwhile, as
   `UnionAttribute` and `IUnion` were (`gmd/Utils/UnionPolyfill.cs`, in git's history), inside
   `#if !NET<version>_0_OR_GREATER`.
6. Update VS Code's C# extension; its bundled Roslyn must know the new syntax or it flags it.
7. Verify: `dotnet build gmd.sln`, `dotnet csharpier check .`, `scripts/test` (all tiers), and a smoke
   run of the built binary as CLAUDE.md describes.

## 2. Move the target framework

Done for net11.0 on 2026-11-11. There was no reason to stay on net10.0: the releases carry their
own runtime, so no user needs one installed; .NET 10 (LTS) and .NET 11 (STS, now two years) are
both supported to 2028-11-14; .NET 11 runs on the same macOS, Linux and glibc versions, and on
Windows drops only versions past their own support (Server 2012, Windows 11 23H2); and the
packages needed no new version.

1. `TargetFramework` in `gmd/gmd.csproj`, `gmdTest/gmdTest.csproj` and `gmdE2eTest/gmdE2eTest.csproj`;
   `DOTNET` in `scripts/build` and `scripts/build.bat`; the `gmd/bin/Debug/net11.0/` paths in
   CLAUDE.md, `.claude/rules/ui.md`, `.vscode/launch.json` and, easily missed since no push runs
   it, the Version step of `.github/workflows/release.yml`; `gmd/bin/Release/net11.0/` in
   CONTRIBUTING.md. Search for the old `net<version>` to find them all.
2. Delete what step 1 added: the polyfill, and the explicit `LangVersion` in
   `Directory.Build.props`, since the SDK's C# is the default for its own target framework.
3. CI: remove the `dotnet-version` lines; the SDK from `global.json` carries its own runtime.
4. Devcontainer: image `mcr.microsoft.com/devcontainers/dotnet:<version>`, and remove the
   `dotnet-install.sh` step from `postCreateCommand` and `scripts/installtools`. Rebuild the
   container afterwards.
5. Check the release notes of Terminal.Gui, Autofac, DiffPlex and
   WeCantSpell.Hunspell for the new target; `scripts/updatepackages` lists what is behind. The
   CSharpier tool is built for older runtimes and rolls forward to the newest one installed.
6. Verify as in step 1.7, with only the new runtime installed, as CI has it, plus `scripts/build
   -l`, since the publish settings are per target. Run the published `artifacts/gmd_linux_x64` and
   the Debug build by hand and press `0`: only the Debug build, a dev build, opens the character
   table (`Build.IsDevInstance`, which .NET 11 broke and the updater relies on). Then make a
   preview and update to it from a released gmd.

## 3. Optional, when the tooling catches up

- **CSharpier** must parse C# 15 syntax before any of it is used; today it is pinned in three
  places that must move together: `.config/dotnet-tools.json` and the `CSharpier.MsBuild`
  reference in the three `.csproj` files. Its changelog says which C# version a release supports.
- With that in place the `union` keyword is available, but `Result` / `Result<T>` in
  `gmd/Utils/Result.cs` are deliberately hand-written structs and can stay so: the keyword form
  would add nothing they lack. Use the keyword for new domain unions (`union MergeOutcome(...)`).
- If the base class library gains standard union types in `System` (an `Option<T>` or a
  `Result<TValue, TError>` were proposed), the build reports any name clash with this project's
  `Result` as an ambiguity (CS0104); resolve it then, not before.
