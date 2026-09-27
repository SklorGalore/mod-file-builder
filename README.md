# Case modification builder

A .NET 10 desktop case modification builder with a native WPF interface for Windows. It generates PSS®E Python (`.py`) and batch (`.idv`, containing `BAT_` commands) files. The web application has been removed; the desktop application does not require a browser, WebView2, or a local web server.

## Windows

With the .NET 10 SDK installed on Windows:

```powershell
dotnet run --project src/ModFileBuilder.Windows
```

To build a standalone desktop folder with .NET dependencies included:

```powershell
dotnet publish src/ModFileBuilder.Windows -c Release -r win-x64 --self-contained true -o ./publish/windows-native
```

Copy the entire output folder and run `ModFileBuilder.Windows.exe`. Use a fresh folder rather than an older web-host package.

## Using the builder

Choose Python or IDV, select equipment, fill in its identifiers and desired changes, and add it to the modification list. Select list entries to edit, remove, or reorder them. Preview reflects saved list entries; save or cancel an active edit before exporting through the native Save As dialog. Entries stay in memory for the session; generated files are exports, not reloadable project files. The app prompts before closing with unsaved changes and offers light/dark themes.

Each entry has its own Python error policy: raise an exception, print a console warning and continue, or ignore errors. Status choices are On-Line and Out-Of-Service, exported as 1 and 0. Empty fields show default placeholders without overriding existing data. PSS®E is needed only when executing the generated files.

## Supported equipment

Bus (`bus_data_4`), non-transformer branch (`branch_data_3`), load (`load_data_6`), machine (`machine_data_4`), and fixed shunt (`shunt_data`). All parameter slots for these APIs are exposed, including branch ratings and ownership. Transformers and switched shunts are not yet included.

The mappings follow [the PSS®E API reference](docs/reference/psse_api.md). Optional blank fields use Python default sentinels or empty batch positions. Default behavior is API-specific: existing values are retained where supported, while new equipment receives PSS®E defaults. This is an add/modify workflow, not an equipment deletion tool. Numeric fields accept invariant decimal points and scientific notation; text is limited to printable ASCII without single quotes for batch compatibility.

Run exports in a PSS®E version supporting these APIs with a case already loaded. Python output lets you choose whether to include the psspy import and default-variable definitions; both are enabled by default. Each entry independently handles nonzero ierr results by raising an exception (the default), printing a warning to the console and continuing, or ignoring errors. If import or default definitions are disabled, the caller must provide those names. Status selectors display On-Line and Out-Of-Service and export 1 and 0. Empty inputs show defaults as placeholder text; optional blanks still export API defaults, while blank node numbers and equipment IDs use the builder defaults 0 and 1. Python exports do not initialize PSS®E, load/save a case, create generator plants, or solve a power flow. Add prerequisite buses/plants first. IDV is a PSS®E batch file, not a Windows shell `.bat` file; generated commands have no spaces after commas.

## Verification

```sh
dotnet build ModFileBuilder.slnx -m:1
dotnet run --project tests/ModFileBuilder.Checks
```

The core check runner covers parameter positions, defaults, formatting under a non-English culture, input rejection, queue copy isolation, export order, and per-entry error handling. Windows UI execution requires Windows. Actual PSS®E execution requires a licensed installation and has not been validated here.

## GitHub Actions

Windows releases include a self-contained Velopack installer and update feed. The app checks for updates at startup and offers to restart after downloading one.

Pull requests and pushes to `main` build the solution and run the core check runner. Push a version tag such as `v1.0.0` to create a GitHub Release with Velopack packages for Windows x64. The same release workflow can be run manually for an existing tag from the Actions tab.

## Repository layout

- `src/ModFileBuilder.Core`: shared equipment schemas, validation, and export generation.
- `src/ModFileBuilder.Windows`: native WPF desktop application.
- `tests/ModFileBuilder.Checks`: checks for the core module.
- `docs/reference`: API mapping reference.

Local PSS®E case files and the vendor PDF can be kept in `_ref/`; Git ignores those files.
