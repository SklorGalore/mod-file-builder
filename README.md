# Case modification builder

A .NET 10 Blazor Server application with Microsoft Fluent UI 5 components and a responsive Fluent-style equipment editor. Generates PSS®E Python (`.py`) or batch (`.idv`, containing `BAT_` commands) files.

## Run

Install the .NET 10 SDK, then run:

```sh
dotnet run --project src/ModFileBuilder.Web --urls http://localhost:5080
```

Open http://localhost:5080. Choose a format, enter equipment data, add modifications, review/edit/reorder the list, and download the file. Download contains only saved list entries. Session data is lost on refresh or server restart; download before leaving.

## Windows desktop

On Windows, run `dotnet run --project src/ModFileBuilder.Windows`. The desktop window hosts the same builder in WebView2, starts and stops its local service with the window, and uses the Windows 11 Desktop Acrylic backdrop. Windows 11 build 22621 or later supplies Acrylic; earlier Windows versions open the same desktop app without the system backdrop. The WebView2 Evergreen Runtime must be installed.

To publish a self-contained Windows desktop folder, publish the web service first, then the desktop shell into the same output directory:

```sh
dotnet publish src/ModFileBuilder.Web -c Release -r win-x64 --self-contained true -o ./publish/windows
dotnet publish src/ModFileBuilder.Windows -c Release -r win-x64 --self-contained true -o ./publish/windows
```

Run `ModFileBuilder.Windows.exe`. Keep `ModFileBuilder.Web.exe` and its published files beside it; the desktop shell starts that local service automatically.

## Publish a self-contained app

To distribute a runnable copy without requiring the .NET runtime to be installed on the target machine, publish for that machine's operating system and CPU architecture. For example:

```sh
dotnet publish src/ModFileBuilder.Web -c Release -r win-x64 --self-contained true -o ./publish/win-x64
dotnet publish src/ModFileBuilder.Web -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64
dotnet publish src/ModFileBuilder.Web -c Release -r osx-arm64 --self-contained true -o ./publish/osx-arm64
```

Share the contents of the matching `publish/<runtime>` folder. On Windows, run `ModFileBuilder.Web.exe`; on Linux or macOS, run `./ModFileBuilder.Web`. The app starts as a web server. Recipients open its displayed URL in a browser. To let other computers reach it, configure the app to listen on a reachable network address and allow that port through the host firewall. Publish a separate folder for every target platform and architecture.

## Supported equipment

Bus (`bus_data_4`), non-transformer branch (`branch_data_3`), load (`load_data_6`), machine (`machine_data_4`), and fixed shunt (`shunt_data`). All parameter slots for these APIs are exposed, including branch ratings and ownership. Transformers and switched shunts are not yet included.

The mappings follow [the PSS®E API reference](docs/reference/psse_api.md). Optional blank fields use Python default sentinels or empty batch positions. Default behavior is API-specific: existing values are retained where supported, while new equipment receives PSS®E defaults. This is an add/modify workflow, not an equipment deletion tool. Numeric fields accept invariant decimal points and scientific notation; text is limited to printable ASCII without single quotes for batch compatibility.

Run exports in a PSS®E version supporting these APIs with a case already loaded. Python output lets you choose whether to include the `psspy` import, nonzero `ierr` checks, and default-variable definitions; all three are enabled by default. If an option is disabled, the file expects the corresponding names or behavior to be provided by its caller. Python exports do not initialize PSS®E, load/save a case, create generator plants, or solve a power flow. Add prerequisite buses/plants first. IDV is a PSS®E batch file, not a Windows shell `.bat` file; generated commands have no spaces after commas.

## Verification

```sh
dotnet build src/ModFileBuilder.Web
dotnet run --project tests/ModFileBuilder.Checks
```

The dependency-free check runner verifies parameter positions, defaults, formatting under a non-English culture, input rejection, queue copy isolation, and export order. Actual execution requires a licensed PSS®E installation and has not been validated here.

## Repository layout

This is one Git repository because the web and Windows apps share the same core library and are developed and released together. The solution is divided into these modules:

- `src/ModFileBuilder.Core`: equipment schemas, validation, and export generation.
- `src/ModFileBuilder.Web`: the Blazor web app and UI.
- `src/ModFileBuilder.Windows`: the Windows WebView2 desktop host for the web app.
- `tests/ModFileBuilder.Checks`: dependency-free checks for the core module.
- `docs/reference`: the API mapping reference used by the project.

Local PSS®E case files and the vendor PDF can be kept in `_ref/`; Git ignores those files. No case files or reference datasets are served by the app. Run locally; remote multi-user deployment needs authentication and hosting configuration.
