# Repository Guidelines

## Project Structure & Modules
- `Artemis.Plugins.Devices.Nanoleaf/` contains the Nanoleaf device plugin; core entry points are `NanoleafBootstrapper.cs` and `NanoleafDeviceProvider.cs`.
  - `Helper/` — network discovery (`NanoleafDiscoveryHelper.cs`: SSDP for panel devices, mDNS/Zeroconf for Matter WiFi Essentials).
  - `Settings/` — persisted device definitions (`DeviceDefinition.cs`, `PendingRestoreState.cs`).
  - `RGB.NET/` — device abstractions and Nanoleaf REST client:
    - `API/` — `NanoleafAPI.cs` (REST client wrapping the Nanoleaf Open API on port 16021) and `NanoleafInfo.cs` (response models).
    - `Generic/` — `INanoleafDeviceDefinition`, `NanoleafRGBDevice`, `NanoleafDeviceUpdateQueue` (RGB.NET integration layer).
    - `Attributes/`, `Enum/`, `Helper/` — shape/ext-control metadata and enum extensions.
  - `ViewModels/` and `Views/` — Avalonia `.axaml` UI for configuration dialogs; assets live in `Resources/`.
- `Artemis.Plugins.Nodes.DateTime/` contains a visual-scripting node plugin; entry points are `Bootstrapper.cs` and `DateTimeNodesProvider.cs`. Nodes live in `Nodes/` (`ConvertToDateTimeNode`, `SplitToDateTimePartsNode`).
- Build outputs land in `<project>/bin/x64/{Debug,Release}/net10.0/` alongside `plugin.json` and (for Nanoleaf) `nanoleaf.png`.

## Build, Test, and Development Commands
- `dotnet restore Artemis.Plugins.jonilala796.sln` — restore NuGet packages.
- `dotnet build Artemis.Plugins.jonilala796.sln -c Debug -p:Platform=x64` — fast local compile for development.
- `dotnet build Artemis.Plugins.Devices.Nanoleaf/Artemis.Plugins.Devices.Nanoleaf.csproj -c Release -p:Platform=x64` — production build; output can be copied into your Artemis plugins directory.
- Launch Artemis with the built plugin to validate device discovery and UI flows; logging is surfaced through Serilog in the host.

## Key Dependencies
- `ArtemisRGB.UI.Shared` / `ArtemisRGB.Plugins.BuildTask` — Artemis host SDK (shared by both plugins).
- `RGB.NET.Core` — RGB device abstraction layer (Nanoleaf plugin).
- `Zeroconf` — mDNS discovery for Matter WiFi Essentials devices (Nanoleaf plugin).

## Coding Style & Naming Conventions
- Implicit C# language version (via `net10.0` TFM); no explicit `<LangVersion>` is set. 4-space indentation, braces on new lines, and nullable reference types enabled.
- Favor primary constructors for lightweight services (see `NanoleafDeviceProvider`, `NanoleafDeviceDefinition`), `var` for obvious types, and PascalCase for public members.
- Keep namespaces aligned to folder paths (`Artemis.Plugins.Devices.Nanoleaf.*`, `Artemis.Plugins.Nodes.DateTime.*`) and place related views/view-models side by side.
- Asset and manifest names (`nanoleaf.png`, `plugin.json`) should stay lowercase to match existing packaging.
- The Nanoleaf plugin distinguishes between panel-based devices (Shapes, Canvas, Lines, etc.) and Matter WiFi Essentials devices (lightstrips, bulbs). `NanoleafAPI.IsMatterEssentialsDevice()` and the model-number set drive this branching throughout `NanoleafRGBDeviceProvider` and `NanoleafRGBDevice`.

## Testing Guidelines
- No automated test project exists yet; rely on `dotnet build` and runtime verification in Artemis.
- When adding tests, prefer xUnit with project names ending in `.Tests`, and mirror source namespaces for fixture placement.
- Manual checks: confirm Nanoleaf discovery succeeds on first load, brightness settings apply, and configuration dialogs open without binding errors.

## Commit & Pull Request Guidelines
- Use concise, imperative commit messages (e.g., “Add panel shape attribute mapping”); keep related changes squashed before opening a PR.
- PRs should describe behavior changes, mention affected device models, and include screenshots of UI updates when applicable.
- Link issues or tasks, call out breaking changes to device discovery or settings schema, and note any required cleanup (e.g., clearing stored device definitions).

## Security & Configuration Tips
- Never commit Nanoleaf auth tokens or local IPs; rely on user-provided settings stored by Artemis.
- Validate network operations defensively (ping checks already present); log warnings rather than exceptions for unreachable devices to avoid crashing the host.
