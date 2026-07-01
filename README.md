# kOS-AFBW

A bridge mod for Kerbal Space Program that exposes AFBW's (Advanced Fly-By-Wire) global enable/disable toggle to kOS scripts. AFBW normally only offers this toggle via a right-click toolbar button. This mod adds two kOS suffixes that let scripts read and set it.

No changes to AFBW's source are required. The bridge locates AFBW's internals at runtime using reflection.

## Requirements

- Kerbal Space Program
- [kOS](https://github.com/KSP-KOS/KOS)
- [Advanced Fly-By-Wire](https://github.com/ksp-advanced-flybywire/ksp-advanced-flybywire)

## Installation

Copy `kOS-AFBW.dll` to `GameData/kOS-AFBW/Plugins/`.

## API

Both suffixes are accessed via `ADDONS:AFBW`.

| Suffix | Type | Access | Description |
|--------|------|--------|-------------|
| `AVAILABLE` | bool | read | `TRUE` if AFBW is loaded and the `AdvancedFlyByWire` type was found via reflection. |
| `ENABLED` | bool | read/write | `TRUE` if AFBW's global controller input is enabled. Setting this also syncs the toolbar icon. |

### Notes

- `AVAILABLE` checks whether the AFBW assembly was found at startup. It returns `TRUE` regardless of scene — it does not check whether AFBW's flight-scene `Instance` is active. Do not use `AVAILABLE` as a flight-scene guard.
- The reflection result is cached on first access to any suffix. AFBW must be loaded before `AVAILABLE` or `ENABLED` is first read.
- `ENABLED` returns `FALSE` and the setter is a no-op when AFBW is not found, when the reflected fields are unavailable (e.g., AFBW API changes), or when AFBW's `Instance` is null (outside a flight scene).
- Toolbar icon sync is best-effort. If `ENABLED` is set outside a flight scene, the field value is written but the icon is not updated until AFBW next initializes.

## Usage

```kerboscript
IF ADDONS:AFBW:AVAILABLE {
    PRINT "AFBW enabled: " + ADDONS:AFBW:ENABLED.
    SET ADDONS:AFBW:ENABLED TO FALSE.  // disable controller input
    // ... do something ...
    SET ADDONS:AFBW:ENABLED TO TRUE.   // re-enable
}
```

## Building from source

**Prerequisites:**

- .NET Framework 4.8 / MSBuild
- KSP installed at `~/ksp/`
- kOS installed at `~/ksp/GameData/kOS/`
- AFBW installed at `~/ksp/GameData/ksp-advanced-flybywire/`

The `.csproj` references assemblies at `$(HOME)/ksp/`. If your KSP installation is elsewhere, update the `HintPath` entries in `kOS-AFBW.csproj`.

**Build:**

```sh
dotnet build kOS-AFBW.csproj
```

Output goes to `bin/Debug/kOS-AFBW.dll` (or `bin/Release/` with `-c Release`).

## License

MIT. See [LICENSE](LICENSE).
