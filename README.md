# Case modification builder

A .NET 10 Blazor Server application with Microsoft Fluent UI 5 components and a responsive Fluent-style equipment editor. Generates PSS®E Python (`.py`) or batch (`.idv`, containing `BAT_` commands) files.

## Run

Install the .NET 10 SDK, then run:

```sh
dotnet run --project ModFileBuilder.Web --urls http://localhost:5080
```

Open http://localhost:5080. Choose a format, enter equipment data, add modifications, review/edit/reorder the list, and download the file. Download contains only saved list entries. Session data is lost on refresh or server restart; download before leaving.

## Publish a self-contained app

To distribute a runnable copy without requiring the .NET runtime to be installed on the target machine, publish for that machine's operating system and CPU architecture. For example:

```sh
dotnet publish ModFileBuilder.Web -c Release -r win-x64 --self-contained true -o ./publish/win-x64
dotnet publish ModFileBuilder.Web -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64
dotnet publish ModFileBuilder.Web -c Release -r osx-arm64 --self-contained true -o ./publish/osx-arm64
```

Share the contents of the matching `publish/<runtime>` folder. On Windows, run `ModFileBuilder.Web.exe`; on Linux or macOS, run `./ModFileBuilder.Web`. The app starts as a web server. Recipients open its displayed URL in a browser. To let other computers reach it, configure the app to listen on a reachable network address and allow that port through the host firewall. Publish a separate folder for every target platform and architecture.

## Supported equipment

Bus (`bus_data_4`), non-transformer branch (`branch_data_3`), load (`load_data_6`), machine (`machine_data_4`), and fixed shunt (`shunt_data`). All parameter slots for these APIs are exposed, including branch ratings and ownership. Transformers and switched shunts are not yet included.

The mappings follow `_ref/psse_api.md`. Optional blank fields use Python default sentinels or empty batch positions. Default behavior is API-specific: existing values are retained where supported, while new equipment receives PSS®E defaults. This is an add/modify workflow, not an equipment deletion tool. Numeric fields accept invariant decimal points and scientific notation; text is limited to printable ASCII without single quotes for batch compatibility.

Run exports in a PSS®E version supporting these APIs with a case already loaded. Python output lets you choose whether to include the `psspy` import, nonzero `ierr` checks, and default-variable definitions; all three are enabled by default. If an option is disabled, the file expects the corresponding names or behavior to be provided by its caller. Python exports do not initialize PSS®E, load/save a case, create generator plants, or solve a power flow. Add prerequisite buses/plants first. IDV is a PSS®E batch file, not a Windows shell `.bat` file; generated commands have no spaces after commas.

## Verification

```sh
dotnet build ModFileBuilder.Web
dotnet run --project ModFileBuilder.Checks
```

The dependency-free check runner verifies parameter positions, defaults, formatting under a non-English culture, input rejection, queue copy isolation, and export order. Actual execution requires a licensed PSS®E installation and has not been validated here.

`ModFileBuilder.Core` contains schemas, validation, and generation; `ModFileBuilder.Web` contains the UI. No case files or reference datasets are served by the app. Run locally; remote multi-user deployment needs authentication and hosting configuration.
