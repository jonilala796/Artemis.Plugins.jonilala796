# Repository Guidelines

## Project Structure & Modules
- `Artemis.Plugins.Devices.Nanoleaf/` contains the plugin source; core entry points are `NanoleafBootstrapper.cs` and `NanoleafDeviceProvider.cs`.
- `RGB.NET/` holds Nanoleaf-specific device abstractions and helpers; `Helper/` and `Settings/` manage discovery and stored device definitions.
- UI is under `ViewModels/` and `Views/` (Avalonia `.axaml` views for configuration dialogs); assets live in `Resources/`.
- Build outputs land in `Artemis.Plugins.Devices.Nanoleaf/bin/x64/{Debug,Release}/net10.0/` alongside `plugin.json` and `nanoleaf.png`.

## Build, Test, and Development Commands
- `dotnet restore Artemis.Plugins.jonilala796.sln` — restore NuGet packages.
- `dotnet build Artemis.Plugins.jonilala796.sln -c Debug -p:Platform=x64` — fast local compile for development.
- `dotnet build Artemis.Plugins.Devices.Nanoleaf/Artemis.Plugins.Devices.Nanoleaf.csproj -c Release -p:Platform=x64` — production build; output can be copied into your Artemis plugins directory.
- Launch Artemis with the built plugin to validate device discovery and UI flows; logging is surfaced through Serilog in the host.

## Coding Style & Naming Conventions
- C# 12 targeting `net10.0`; 4-space indentation, braces on new lines, and nullable reference types enabled.
- Favor primary constructors for lightweight services (see `NanoleafDeviceProvider`), `var` for obvious types, and PascalCase for public members.
- Keep namespaces aligned to folder paths (`Artemis.Plugins.Devices.Nanoleaf.*`) and place related views/view-models side by side.
- Asset and manifest names (`nanoleaf.png`, `plugin.json`) should stay lowercase to match existing packaging.

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
