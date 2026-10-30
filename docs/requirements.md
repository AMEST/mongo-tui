# Requirements and installation

**English** | [Русский](requirements-ru.md) · [Back to README](../README.md)

[macOS](#macos) · [Linux](#linux) · [Windows](#windows) ·
[Build from source](#build-from-source) · [Publish a build](#publish-a-build)

## What to install

MongoTui needs access to a local or remote MongoDB server, plus an interactive terminal for
the TUI or a browser for the local Web interface (`mongo-tui web --open`).
Install the tools on the machine **running MongoTui**; a local MongoDB server is optional.

| Component | When needed |
|---|---|
| MongoDB Shell (`mongosh`) | JavaScript queries in TUI/Web. Web Browse mode and document operations use the bundled driver. |
| MongoDB Database Tools (`mongoimport`, `mongoexport`) | Collection import and export. Separate from `mongosh`. |
| OpenSSH client 8.4+ (`ssh`) | SSH/TPAM connections, including interactive 2FA. |
| .NET 10 and ASP.NET Core 10 runtimes | Framework-dependent builds only, including TUI mode. Release archives and self-contained builds include both. |
| .NET SDK 10 and Git | Building from source. The SDK includes the runtime. |
| Docker | Integration tests only; MongoDB and `mongosh` run in disposable containers. |
| Node.js/npm | Rebuilding the Web editor bundle only; normal .NET builds use the checked-in bundle. |

External tools must be in the `PATH` used by MongoTui. Standalone MongoDB packages do not
require Node.js/npm. Extract the entire MongoTui archive: SQLite and Oniguruma native
libraries must stay alongside the executable.

## macOS

### MongoDB tools and SSH

With [Homebrew](https://brew.sh/) installed:

```sh
brew tap mongodb/brew
brew install mongosh
brew install mongodb-database-tools
ssh -V
```

These packages come from the [MongoDB Homebrew tap](https://github.com/mongodb/homebrew-brew).
Database Tools are optional if you do not use import/export. For SSH/TPAM, check that
`ssh -V` reports 8.4 or newer. If needed, install a newer client and select it for this terminal:

```sh
brew install openssh
export PATH="$(brew --prefix openssh)/bin:$PATH"
```

Add that `export` to `~/.zshrc` to keep using it in new terminals.
Without Homebrew, use the [Shell](https://www.mongodb.com/try/download/shell) and
[Database Tools](https://www.mongodb.com/try/download/database-tools) ZIP downloads for
your architecture, extract them and add their `bin` directories to `PATH`.

### .NET for other build types

Skip this for release archives. For other builds, open the
[.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0),
choose **macOS → Arm64** for Apple Silicon or **x64** for Intel, and select the runtime or SDK:

| Build type | Installer |
|---|---|
| Framework-dependent | ASP.NET Core Runtime 10 and .NET Runtime 10 |
| Build/run from source | .NET SDK 10 |

For source builds, install Git with `brew install git`. See the
[official macOS .NET instructions](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
for supported OS versions and installation paths.

### Launch a release

Download `mongotui-osx-arm64.tar.gz` for Apple Silicon or `mongotui-osx-x64.tar.gz` for Intel
from [Releases](https://github.com/AMEST/mongo-tui/releases). For example:

```sh
mkdir -p ./mongotui
tar -xzf ./mongotui-osx-arm64.tar.gz -C ./mongotui
cd ./mongotui
xattr -d com.apple.quarantine ./mongo-tui
xattr -d com.apple.quarantine ./libe_sqlite3.dylib
xattr -d com.apple.quarantine ./libonigwrap.dylib
chmod +x ./mongo-tui
./mongo-tui
```

**Downloaded binaries are unsigned and not notarized. Removing quarantine before the
first launch is required for Gatekeeper.** If `xattr` reports `No such xattr`, that file
already has no quarantine attribute; continue with the remaining commands.

## Linux

### MongoDB tools and SSH

Download [MongoDB Shell](https://www.mongodb.com/try/download/shell) and, if needed,
[Database Tools](https://www.mongodb.com/try/download/database-tools). Choose your
distribution and CPU architecture, with the `deb` or `rpm` package type.
For Shell, choose the standard package unless you specifically need shared OpenSSL.

Run from the directory containing the downloaded packages. Keep only one version of each
package there; omit the Database Tools argument if you only downloaded Shell.
This method does not require configuring a MongoDB package repository.

Debian / Ubuntu:

```sh
sudo apt-get update
sudo apt install ./mongodb-mongosh_*.deb ./mongodb-database-tools-*.deb
sudo apt-get install -y openssh-client
```

RHEL / compatible distributions with `dnf`:

```sh
sudo dnf install ./mongodb-mongosh-*.rpm ./mongodb-database-tools-*.rpm
sudo dnf install -y openssh-clients
```

Check `ssh -V`: SSH/TPAM needs 8.4+. MongoDB documents package installation for
[DEB](https://www.mongodb.com/docs/database-tools/installation/?operating-system=linux&package-type=deb)
and [RPM](https://www.mongodb.com/docs/database-tools/installation/?operating-system=linux&package-type=rpm).

### System libraries and .NET

Even a self-contained build needs native .NET system libraries. **Ubuntu 24.04** example:

```sh
sudo apt-get update
sudo apt-get install -y ca-certificates libc6 libgcc-s1 libgssapi-krb5-2 libicu74 libssl3t64 libstdc++6 tzdata zlib1g
```

For a framework-dependent build on **Ubuntu 24.04**, install the runtime:

```sh
sudo apt-get install -y aspnetcore-runtime-10.0
```

For building from source, install the SDK and Git instead:

```sh
sudo apt-get install -y dotnet-sdk-10.0 git
```

These .NET packages are available in Ubuntu 24.04's own feeds. Package names and setup
differ elsewhere: use the direct instructions for
[Ubuntu versions](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install),
[Debian](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian) or
[other distributions](https://learn.microsoft.com/en-us/dotnet/core/install/linux).
The native library list above is specifically for Ubuntu 24.04.

### Launch a release

Choose `mongotui-linux-x64.tar.gz` for x86-64 or `mongotui-linux-arm64.tar.gz` for ARM64
from [Releases](https://github.com/AMEST/mongo-tui/releases). For example:

```sh
mkdir -p ./mongotui
tar -xzf ./mongotui-linux-x64.tar.gz -C ./mongotui
cd ./mongotui
chmod +x ./mongo-tui
./mongo-tui
```

## Windows

### MongoDB tools and PATH

1. Download the **Windows x64 ZIP** from [MongoDB Shell](https://www.mongodb.com/try/download/shell).
   Extract it so that `mongosh.exe` is at `C:\Tools\mongosh\bin\mongosh.exe`.
2. For import/export, download the **Windows x86_64 ZIP** from
   [Database Tools](https://www.mongodb.com/try/download/database-tools). Extract it so that
   `mongoimport.exe` and `mongoexport.exe` are in `C:\Tools\mongodb-database-tools\bin`.
3. In Windows search, open **Edit environment variables for your account → Path → Edit → New**.
   Add `C:\Tools\mongosh\bin` and, if installed, `C:\Tools\mongodb-database-tools\bin`.
   Open a new terminal after saving.

You can choose other directories; add the actual folders containing the executables.
To try the example paths in the current PowerShell session:

```powershell
$env:Path = "C:\Tools\mongosh\bin;C:\Tools\mongodb-database-tools\bin;$env:Path"
```

### SSH/TPAM

Check `ssh -V` in PowerShell. If the client is missing, install the Windows optional
feature from an **administrator PowerShell**:

```powershell
Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0
```

The bundled Windows client can be older than 8.4. If so, download `OpenSSH-Win64.zip`
from the [official Win32-OpenSSH releases](https://github.com/PowerShell/Win32-OpenSSH/releases),
extract its contents to `C:\Tools\OpenSSH`, then select it in the terminal used to launch MongoTui:

```powershell
$env:Path = "C:\Tools\OpenSSH;$env:Path"
ssh -V
```

Keep this folder before older SSH clients in `PATH`; no SSH server installation is needed.
See Microsoft's [OpenSSH client installation](https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse).

### .NET for other build types

Skip this for release archives. For a framework-dependent build, use PowerShell:

```powershell
winget install --id Microsoft.DotNet.AspNetCore.10 --exact
```

For source builds, install the SDK and Git instead:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
winget install --id Git.Git --exact --source winget
```

Reopen the terminal after installation. If WinGet is unavailable, use the
[.NET 10 installers](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and
[Git for Windows installer](https://git-scm.com/install/windows).
The package IDs are listed in Microsoft's [.NET installation guide](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

### Launch a release

Download `mongotui-windows-x64.tar.gz` from [Releases](https://github.com/AMEST/mongo-tui/releases).
Extract it with your archive manager, or use PowerShell from the download directory:

```powershell
New-Item -ItemType Directory -Path .\mongotui
tar -xzf .\mongotui-windows-x64.tar.gz -C .\mongotui
cd .\mongotui
.\mongo-tui.exe
```

## Check dependencies

Run these in the terminal used to launch MongoTui; check only the tools you need:

```sh
mongosh --version
mongoimport --version
mongoexport --version
ssh -V
```

For framework-dependent builds, `dotnet --list-runtimes` must include both
`Microsoft.NETCore.App 10.0.x` and `Microsoft.AspNetCore.App 10.0.x`, even when running the TUI.
For source builds, `dotnet --list-sdks` must include `10.0.x`.
If a tool works in another terminal but MongoTui cannot find it, restart MongoTui from a
terminal with the updated `PATH`.

## Build from source

Install the SDK and Git using the instructions for your OS above, plus the external tools
for the features you use. From a terminal or PowerShell:

```sh
git clone https://github.com/AMEST/mongo-tui.git
cd mongo-tui
dotnet run --project src/MongoTui.TUI
```

To run the local Web interface instead:

```sh
dotnet run --project src/MongoTui.TUI -- web --open
```

Normal builds do not need Node.js/npm. See [Web editor builds](web-interface.md#build-and-distribution)
only when changing the bundled editor.

Build and run tests without Docker:

```sh
dotnet build MongoTui.sln
dotnet test MongoTui.sln --filter "Category!=Integration"
```

For the full suite, install and start Docker first:

| OS | Installation |
|---|---|
| macOS | `brew install --cask docker-desktop`, then open Docker Desktop. |
| Linux | Follow [Docker Engine for Ubuntu](https://docs.docker.com/engine/install/ubuntu/) or [your distribution](https://docs.docker.com/engine/install/). Configure [non-root access](https://docs.docker.com/engine/install/linux-postinstall/) for the user running tests. |
| Windows | Install and open [Docker Desktop with the WSL 2 backend](https://docs.docker.com/desktop/setup/install/windows-install/), using Linux containers. |

Once `docker info` succeeds:

```sh
dotnet test MongoTui.sln
```

## Publish a build

Run from the repository root with .NET SDK 10 installed. Choose the target identifier (RID):

| OS / architecture | RID |
|---|---|
| macOS Apple Silicon | `osx-arm64` |
| macOS Intel | `osx-x64` |
| Linux x86-64 | `linux-x64` |
| Linux ARM64 | `linux-arm64` |
| Windows x64 | `win-x64` |

Self-contained example, matching release packaging. Replace `linux-x64` in both places
with your target RID:

```sh
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:Version=0.8.0 -p:DebugType=none -p:DebugSymbols=false -o publish/linux-x64
```

Distribute the **whole output directory**. Run `./mongo-tui` on macOS/Linux or
`.\mongo-tui.exe` on Windows from that directory. The runtime is included; external MongoDB
tools and Linux system libraries still need to be installed. Web assets are embedded; no separate
`wwwroot` directory is needed. The output includes `LICENSE` and `THIRD-PARTY-LICENSES.md`.

For a framework-dependent build, use the matching RID and install both the .NET 10 and
ASP.NET Core 10 runtimes on the target:

```sh
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r linux-x64 --self-contained false -p:Version=0.8.0 -o publish/framework-dependent
dotnet publish/framework-dependent/mongo-tui.dll
```

Keep the entire output directory for this build too. Downloaded macOS builds require the
[Gatekeeper steps](../README.md#macos--required-before-first-launch).

The release workflow takes the version from a `v*` Git tag; local builds default to `0.8.0`.
See [release preparation](releasing.md) for validation and packaging checks.
