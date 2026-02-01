# WNG CLI - Package Management Tool

**WNG** is a powerful command-line interface tool for managing and evaluating package versions across multiple ecosystems: .NET Framework, NuGet packages, and npm packages.

## Features

- 📦 **Multi-Platform Support**: Manage NuGet, npm, and .NET Framework versions from a single CLI
- 🔄 **Update Management**: Easily update packages to latest versions with flexible version targeting
- 🔍 **Version Analysis**: Evaluate current vs. available package versions
- 🎯 **Selective Updates**: Filter packages by name for targeted updates
- 🔒 **Silent Mode**: Automate operations without confirmation prompts
- 🌐 **URL Support**: Display package URLs for quick reference

## Installation

```bash
dotnet tool install -g wng
```

## Commands

### 1. NPM Command

Manage and evaluate npm package versions in your Node.js projects.

#### Usage

```bash
wng npm [path] [options]
```

#### Arguments

- `[path]` - (Optional) The path to package.json or its folder. Defaults to current directory.

#### Options

| Option | Description |
|--------|-------------|
| `-p, --packages <packages>` | Comma-separated list of package names to filter during analysis |
| `-g, --ignore <packages>` | Comma-separated list of package names to ignore during analysis |
| `-m, --major <number>` | Find the latest version of the specified major number |
| `-n, --minor` | Only evaluate the latest version of the current major |
| `-i, --install` | Run npm install even without --update (useful for vulnerability checks) |
| `--pre` | Include prerelease versions (Alpha, Beta, RC, Nightly, etc.) |
| `--url` | Include package URL in the list |
| `--projectUrl` | Include package project URL in the list |
| `--versionUrl` | Include package version URL in the list |
| `--update` | Update packages to desired version |
| `--silent` | Execute update and install without confirmation prompts |
| `--debug` | Include additional debugging columns in output |

#### Examples

```bash
# Evaluate npm packages in current directory
wng npm

# Update all packages
wng npm --update

# Include prerelease versions
wng npm --pre

# Only check minor version updates
wng npm --minor

# Update to latest minor versions
wng npm --minor --update

# Filter specific packages
wng npm --packages "@date-fns,typescript"

# Find latest version of major 4 for typescript
wng npm --packages "typescript" --major 4

# Update specific packages in a project
wng npm "C:\project\app\package.json" --packages "tslib,typescript" --update
```

---

### 2. NuGet Command

Manage and evaluate NuGet package versions in your .NET projects.

#### Usage

```bash
wng nuget [path] [options]
```

#### Arguments

- `[path]` - (Optional) The path to .csproj, packages.props, or solution folder. Defaults to current directory.

#### Options

| Option | Description |
|--------|-------------|
| `-p, --packages <packages>` | Comma-separated list of package names to filter during analysis |
| `-g, --ignore <packages>` | Comma-separated list of package names to ignore during analysis |
| `-m, --major <number>` | Find the latest version of the specified major number |
| `-n, --minor` | Only evaluate the latest version of the current major |
| `-i, --install` | Run restore command even without --update |
| `-b, --build` | Run build command to verify code compatibility |
| `-r, --restore` | Run restore command before evaluating packages |
| `-u, --update` | Update packages to desired version |
| `--pre` | Include prerelease versions (Alpha, Beta, RC, Nightly, etc.) |
| `--url` | Include package URL in the list |
| `--projectUrl` | Include package project URL in the list |
| `--versionUrl` | Include package version URL in the list |
| `--silent` | Execute update and install without confirmation prompts |
| `--debug` | Include additional debugging columns in output |

#### Examples

```bash
# Evaluate NuGet packages in current directory
wng nuget

# Update all packages
wng nuget --update

# Include prerelease versions
wng nuget --pre

# Only check minor version updates
wng nuget --minor

# Update to latest minor versions
wng nuget --minor --update

# Filter specific packages
wng nuget --packages "System.Data,Microsoft"

# Find latest version of major 4 for Azure packages
wng nuget --packages "Azure" --major 4

# Update specific packages in a project
wng nuget "C:\MyProject" --packages "Azure.Core,Microsoft.Graph" --update
```

---

### 3. Dotnet Command ⚠️ Experimental

Manage and evaluate .NET Framework versions in your projects.

> **Note**: This command is still experimental and not yet feature complete.

#### Usage

```bash
wng dotnet [path] [options]
```

#### Arguments

- `[path]` - (Optional) The path to the .csproj file or solution folder. Defaults to current directory.

#### Options

| Option | Description |
|--------|-------------|
| `-p, --projects <projects>` | Comma-separated list of project names to filter during analysis |
| `-i, --install <version>` | Install a new version of the dotnet Framework (use with --sdk or --runtime) |
| `-u, --update <version>` | Update dotnet version of project(s). Use "latest" for newest version |
| `--sdk` | List available dotnet SDK releases and compare with installed versions |
| `--runtime` | List available dotnet Runtime releases and compare with installed versions |
| `--silent` | Execute all operations without confirmation prompts |
| `--debug` | Include additional debugging columns in output |

#### Examples

```bash
# Evaluate dotnet versions in current directory
wng dotnet

# Update to dotnet 10.0
wng dotnet --update 10.0

# Update to latest version
wng dotnet --update latest

# Install latest SDK
wng dotnet --sdk --install latest

# Install specific runtime version
wng dotnet --runtime --install 10.0

# Update specific projects to latest version
wng dotnet "C:\MyProject" --projects "MyProject.Core,MyProject.Web" --update latest
```

#### Validation Rules

- Cannot use both `--sdk` and `--runtime` simultaneously
- `--update` cannot be used with `--sdk` or `--runtime`
- `--projects` can only be used when updating projects
- `--install` requires either `--sdk` or `--runtime` to be specified

## Common Workflows

### Check for Updates

```bash
# Check NuGet updates
wng nuget

# Check npm updates
wng npm

# Check .NET Framework versions
wng dotnet
```

### Update Packages

```bash
# Update all NuGet packages to latest
wng nuget --update

# Update all npm packages to latest
wng npm --update

# Update .NET Framework to latest
wng dotnet --update latest
```

### Selective Updates

```bash
# Update only specific NuGet packages
wng nuget --packages "Azure.Core,Microsoft.Graph" --update

# Update only specific npm packages
wng npm --packages "typescript,tslib" --update
```

### Safe Minor Updates

```bash
# Update NuGet packages within current major version
wng nuget --minor --update

# Update npm packages within current major version
wng npm --minor --update
```

## Version Targeting

WNG supports flexible version targeting:

- **Latest**: `--update` or `--update latest` - Updates to the newest available version
- **Minor Updates**: `--minor --update` - Updates to latest version within current major
- **Major Specific**: `--major 4 --update` - Updates to latest version of specified major

## Output Options

Enhance your package listing with URL information:

- `--url` - Display package repository URL
- `--projectUrl` - Display package project/homepage URL
- `--versionUrl` - Display specific version URL

## Silent Mode

Use `--silent` to automate operations without confirmation prompts - ideal for CI/CD pipelines:

```bash
wng nuget --update --silent
wng npm --update --silent
wng dotnet --update latest --silent
```

## Version

Current version: **1.0.0**

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.
