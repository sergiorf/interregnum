# Foundation verification

This document records the Foundation F0.1 verification result. It distinguishes checks that actually ran from checks that still require a suitable environment.

## Local verification — 2026-10-25

Environment: Windows, .NET SDK 10.0.402 selected through `global.json`.

The following commands completed successfully from the repository root:

```powershell
dotnet restore Interregnum.sln
dotnet build Interregnum.sln --no-restore
dotnet test Interregnum.sln --no-build
```

The build completed with zero warnings and zero errors. The test run passed all 4 tests.

## Windows CI

`.github/workflows/ci.yml` mirrors the local restore, build, and test sequence on `windows-latest`. It explicitly installs .NET SDK 10.0.402, matching `global.json`.

GitHub Actions run `40219674221` completed successfully on 2026-10-25 in 51 seconds for commit `0c1da54` (`CI script.`). The run exercised the workflow's restore, build, and test sequence on `windows-latest`.

## Godot editor smoke test

Godot was not installed in the verification environment, so the official portable Godot 4.7.2 .NET Windows build was downloaded to the ignored `.tools` directory. The executable reported `4.7.2.stable.mono.official.ed1daf0bf`.

The editor imported the project and exited cleanly in headless mode. The main scene also started and completed two frames in headless mode without project or C# errors. During the first import, Godot inserted an explicit `net8.0` target into the game project and retained the previous file as `Interregnum.Game.csproj.old`; both generated changes were removed so the repository's shared .NET 10 target remains authoritative. This editor migration behavior is a known integration risk to recheck during the interactive smoke test.

An attempt to capture a rendered PNG with Godot's headless dummy renderer crashed inside the engine's PNG movie writer before producing an image. This does not establish an application defect, but it means the visual and interaction check remains outstanding.

To close the visual check:

1. Open `src/Interregnum.Game/project.godot` in the .NET editor.
2. Confirm the project imports and builds without editor errors.
3. Run the main scene and confirm the 12-province map renders.
4. Select multiple provinces and confirm the inspection panel follows the selection.
5. Record the result and any warnings in this document.
