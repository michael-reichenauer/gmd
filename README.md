# Gmd

Gmd is a versatile, cross-platform console UI Git client designed to enhance the Git user experience, particularly when visualizing the branching structure. Key features of Gmd include:

- **Branch Visibility Control**: Toggle which branches are displayed or hidden. Its a similar benefit as 'dynamic and reversible' squash merges, without the need to rewrite history to preserving a clean commit log.
- **Side-by-Side Diff**: View a side-by-side diff of all changes in a commit, or between two branches.
- **Simply Git Commands**: Execute most used Git commands with out needing to remember the syntax.
</br>
</br>

![Gmd Animation](gmd/doc/Animation.gif)
*Screenshot of Gmd in action.*


## Background

Many Git clients tend to clutter the interface with a plethora of branches, complicating the commit log. In response, developers often simplify this log by opting for squashing or rebasing. 

While some clients attempt to minimize branching complexity by concealing commits, Gmd stands out by giving users the option to interactively select which branches they see. This aids developers in focusing solely on branches of relevance for them. For instance, a developer may only want to monitor the main and the current working branch, whereas a team leader may track the main and several specific branches to ensure that the team is on track and coordinated.

Gmd presents an intuitive user interface where both the branch and commit history are easily discernible without the necessity for rebasing or squashing. Common commands are streamlined with context menus and user-friendly dialogs. Additionally, Gmd's console window facilitates both key-based navigation as well as mouse support.

## Documentation and Help File

Access the Gmd [help file here](https://github.com/michael-reichenauer/gmd/blob/main/gmd/doc/help.md).

## Installation
As an alternative to building from source (see below), Gmd is also available as pre-built binaries. 
Download the appropriate version for your platform (`gmd_linux`, `gmd_windows`, or `gmd_osx`) from the [releases page](https://github.com/michael-reichenauer/gmd/releases).  Windows users can conveniently download and run the installer. Admin rights or `sudo` is not required. If Gmd resides in a user data folder, `sudo` isn't necessary for updates either. The tool has an built-in update checker, notifying users of newer available versions and Gmd can then be updated with a single click from within the tool or by using the command line option like:

    ```bash
    > gmd --update
    ```

### Linux and OSX/Mac:

To utilize Gmd from any directory, download the binary to a folder, such as e.g. `~/gmd/`, set executable flag and modify the environment PATH accordingly:

```bash
> curl -sS -L --create-dirs -o ~/gmd/gmd "https://github.com/michael-reichenauer/gmd/releases/latest/download/gmd_linux"
> chmod +x ~/gmd/gmd
> echo 'export PATH=$PATH:~/gmd' >>~/.profile
> . ~/.profile`
```

Alternatively, simply execute:
```bash
> curl -sL https://raw.githubusercontent.com/michael-reichenauer/gmd/main/install.sh | bash
```

### Windows:
Either download the setup file from the releases page or use:

```bash
> curl -o gmdSetup.exe https://github.com/michael-reichenauer/gmd/releases/latest/download/gmdSetup.exe
> gmdSetup
```


## Development 
### Devcontainers or GitHub CodeSpaces
Gmd is primed for use with Devcontainers or GitHub CodeSpaces in conjunction with VS Code. With CodeSpaces, build and run operations can be executed within the browser, eliminating installation requirements. Alternatively, Docker combined with VS Code is suitable for devcontainers.

### Running form source using dotnet
```bash
> dotnet run --project gmd/gmd.csproj
```

### Building

#### Linux:
```bash
> dotnet publish gmd/gmd.csproj -c Release -r linux-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
```
#### Windows:
```bash
> dotnet publish gmd/gmd.csproj -c Release -r win-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
```
#### OSX/Mac:
```bash
> dotnet publish gmd/gmd.csproj -c Release -r osx-x64 -p:PublishReadyToRun=true --self-contained true -p:PublishSingleFile=true
```

#### Test and Build All Platforms:
Use the provided build script to test and build for all platforms:

```bash
> ./build      # (or '> Build.bat' on Windows)
```