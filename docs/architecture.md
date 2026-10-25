# Architecture

## Goals and boundaries

Interregnum begins as a modular monolith: one portable simulation library, one Godot client, and one test project. The simulation is authoritative. Godot renders state and submits commands but does not decide whether an action is legal or calculate its effects.

The current Milestone 0 dependency direction is:

```text
Interregnum.Game  ──>  Interregnum.Core  <──  Interregnum.Tests
       Godot             pure C#                 xUnit
```

There is no dependency-injection container, ECS, event bus, persistence framework, or service layer. These would add vocabulary and indirection before the game has enough behavior to justify them.

## Projects

### Interregnum.Core

Targets .NET 10 and references only the base class library. It contains:

- Stable value identifiers: `FactionId`, `ProvinceId`, and `ArmyId`.
- Domain records: `Faction`, `Province`, `Settlement`, and `Army`.
- `WorldState`, the aggregate snapshot used by simulation and presentation.
- Scenario document records, JSON loading, validation, and conversion to a world.

The first model is immutable by construction. Milestone 1 will introduce controlled state transitions through a simulation engine; callers will not mutate collections directly.

### Interregnum.Game

Uses Godot 4.7.2 .NET and references Core. `Main` composes a small native UI. `ProvinceMap` owns only geometry, hit testing, and drawing. It emits a selected scenario province, after which `Main` reads corresponding domain state for display.

The scenario JSON lives under the Godot resource root so it is importable and can later be included in exports. Tests link the same file into their output rather than maintaining a second copy.

### Interregnum.Tests

Uses xUnit. Milestone 0 tests the scenario boundary because malformed external data is the first meaningful failure mode. Milestone 1 will add economic invariants, command rejection, turn order, rebellion/recovery, and replay determinism.

## Domain entities

- **WorldState:** scenario identity, historical start year, turn, factions, provinces, and armies.
- **Faction:** identity, display color, treasury, food, aggregate military strength, stability, and playability.
- **Province:** owner, population, agriculture, base tax, loyalty, administrative burden, tax rate, optional settlement, and neighbor IDs.
- **Army:** owner, location, strength, and upkeep.
- **ScenarioDefinition:** serialization-facing data including map polygons. Geometry remains scenario metadata and is not stored in the authoritative province model.

Integer resources are intentional. They keep initial formulas legible, deterministic, and free from rounding ambiguity. Population is an abstract game-scale unit rather than a literal headcount.

## Scenario validation

Loading is a boundary operation:

1. Deserialize JSON with unknown fields rejected and tax rates parsed by name.
2. Validate required values, unique IDs, ranges, references, friendly army placement, polygons, and symmetric adjacency.
3. Convert validated definitions into typed domain identifiers and read-only dictionaries.

Invalid content produces `ScenarioValidationException` with all discovered errors so content authors can repair a file in one pass.

## Command processing (Milestone 1 design)

The next slice will introduce explicit commands:

- `SetProvinceTaxRate`
- `RecruitArmy`
- `MoveArmy`
- `EndTurn`

A single `SimulationEngine.Execute(GameCommand)` entry point will validate against current state and return a structured `CommandResult` containing success/failure, stable error codes, resulting state, and domain events. An invalid command will never partially update state.

`EndTurn` will run a fixed ordered pipeline: production, consumption, taxation, military upkeep, administration, loyalty, rebellion, faction stability, then turn advancement. Events from each stage will carry the numbers needed to explain the summary UI. No randomness is planned for the first playable slice.

## Godot integration

Godot receives domain snapshots and turns them into controls and draw calls. It may format values, animate changes, and track selection, but it must not compute income, loyalty, costs, legal movement, or rebellion state. This keeps headless tests fast and leaves the simulation portable to tooling or another renderer.

The current scene is intentionally code-composed to keep the first UI review compact. As interaction grows, stable panels should become separate `.tscn` scenes; domain rules still remain in Core.

## Technical choices

- **Godot 4.7.2 .NET:** latest stable maintenance release verified on 2026-10-24; 4.8 is still beta.
- **.NET 10:** current LTS and requested target. `global.json` pins 10.0.402 with patch roll-forward.
- **JSON:** reviewable, diffable, and adequate for a dozen provinces. A custom editor or database is premature.
- **Typed IDs:** prevent accidental cross-entity lookups without introducing entity base classes.
- **No seeded random source yet:** the first rules require no randomness. An interface will be added only when a mechanic needs it.

## Risks and assumptions

- Godot/C# exports should be tested early on the commercial target machines; editor compilation is not equivalent to a packaged Windows export.
- The JSON document currently combines simulation setup and fictionalized interactive province geometry. Presentation-only surrounding geography comes from a reduced Natural Earth 1:50m dataset and is projected by the Godot client; province cells are clipped to its mainland contour so the campaign follows the physical coast. This is convenient for the slice, but a historical map pipeline may later move both into reviewed visual metadata.
- Province polygons are authored in viewport coordinates and assume a stylized fixed map. Camera navigation and scalable map authoring are future work.
- Immutable snapshots favor correctness over allocation efficiency. Measure real turn-processing costs before considering mutable internals or ECS-style storage.
- Historic names, borders, demographics, and balance values are prototypes and need research and design review.
