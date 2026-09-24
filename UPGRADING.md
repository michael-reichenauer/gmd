# Upgrading to the next .NET and C#

Gmd builds with the .NET 11 SDK for its C# 15 compiler (union types) while it still targets and
runs on .NET 10. This is what to do when .NET 11 and C# 15 are released (planned for November
2026), and again for the release after that. Every pin named here is found with:

```bash
grep -rn "net10.0\|10.0.x\|dotnet:10\|--channel 11\|LangVersion\|11.0.100" \
  --include=*.json --include=*.props --include=*.csproj --include=*.yml --include=installtools \
  --include=build --include=build.bat . | grep -v "/bin/\|/obj/"
```

## 1. Move to the released SDK (do this at GA in any case)

1. `global.json`: set `sdk.version` to the GA version (`11.0.100`), keep `rollForward:
   latestFeature`, drop `allowPrerelease`.
2. `Directory.Build.props`: `LangVersion` from `preview` to `15`, and shorten its comment.
3. `installtools` and `.devcontainer/devcontainer.json`: drop `--quality preview` from the
   `dotnet-install.sh` line, so the GA SDK is installed beside the image's .NET 10.
4. In the container: run that install line once by hand (or `./installtools`), then check
   `dotnet --version` prints the GA version from `global.json`.
5. CI (`.github/workflows/build-and-release.yml`) needs no change: `global-json-file` picks the
   new pin, and the `dotnet-version: '10.0.x'` line keeps the .NET 10 runtime the tests run on.
6. Update VS Code's C# extension; its bundled Roslyn must know unions or it flags every pattern.
7. Verify: `dotnet build gmd.sln`, `dotnet csharpier check .`, `./test` (all tiers), and a smoke
   run of the built binary as CLAUDE.md describes. Then close the "at .NET 11 GA" item in
   MODERNIZATION.md.

## 2. Decide the target framework

Staying on net10.0 is the default: it is the LTS release (supported to November 2028) and the
runtime the published binaries carry, where .NET 11 is STS (18 months). Nothing else changes; the
polyfill in `gmd/Utils/UnionPolyfill.cs` stays and is harmless.

Moving to net11.0, when wanted (or when net10.0 leaves support):

1. `TargetFramework` in `gmd/gmd.csproj` and `gmdTest/gmdTest.csproj`; `DOTNET` in `./build` and
   `build.bat`; the `gmd/bin/Debug/net10.0/gmd` path in CLAUDE.md.
2. Delete `gmd/Utils/UnionPolyfill.cs` (its `#if !NET11_0_OR_GREATER` already compiles it out) and
   the explicit `LangVersion` in `Directory.Build.props`, since C# 15 is the default for net11.0.
3. CI: remove the `dotnet-version: '10.0.x'` lines and their comment; the SDK from `global.json`
   carries the .NET 11 runtime.
4. Devcontainer: image `mcr.microsoft.com/devcontainers/dotnet:11.0`, and remove the
   `dotnet-install.sh` step from `postCreateCommand` and `./installtools`.
5. Check the release notes of Terminal.Gui, Autofac, DiffPlex and WeCantSpell.Hunspell for the new
   target; `./updatepackages` lists what is behind.
6. Verify as in step 1.7, plus `./build -l`, since the publish settings are per target.

## 3. Optional, when the tooling catches up

- **CSharpier** must parse C# 15 syntax before any of it is used; today it is pinned in three
  places that must move together: `.config/dotnet-tools.json` and the `CSharpier.MsBuild`
  reference in both `.csproj` files. Its changelog says which C# version a release supports.
- With that in place the `union` keyword is available, but `Result` / `Result<T>` in
  `gmd/Utils/Result.cs` are deliberately hand-written structs and can stay so: the keyword form
  would add nothing they lack. Use the keyword for new domain unions (`union MergeOutcome(...)`).
- If the base class library gains standard union types in `System` (an `Option<T>` or a
  `Result<TValue, TError>` were proposed), the build reports any name clash with this project's
  `Result` as an ambiguity (CS0104); resolve it then, not before.
