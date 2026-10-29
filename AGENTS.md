# Interregnum — Agent Instructions

## Mission

Interregnum is a single-player, turn-based historical strategy game set during the fragmentation of the Western Roman Empire, initially in a fictionalized Gaul scenario around 480 AD.

Design thesis: Conquering territory is easier than governing it. Every major mechanic should deepen the tension between expansion, resources, administration, loyalty, and stability.

Favor a small, understandable, playable game over speculative frameworks or broad feature scaffolding.

## Read before changing code

- `README.md` — setup, supported tools, and current state.
- `docs/architecture.md` — architectural boundaries and simulation contracts.
- `docs/game-design.md` — gameplay goals and intentional simplifications.
- `docs/roadmap.md` — milestone scope and sequencing.
- `docs/implementation-plan.md` — current work packages, gates, and acceptance criteria.

Treat these as the source of truth. If implementation and documentation conflict, explain the discrepancy rather than silently changing the design.

## Technology and layout

- .NET 10 / C# 14; SDK pinned by `global.json`.
- `src/Interregnum.Core/`: pure C# domain, rules, scenario loading, simulation; no Godot dependency.
- `src/Interregnum.Game/`: Godot 4.7.2 .NET UI, visualization, input, and presentation.
- `tests/Interregnum.Tests/`: xUnit tests, especially for rules and determinism.
- Scenarios are human-readable JSON validated at load time.
- Respect nullable reference types, warnings-as-errors, and deterministic build settings.

## Non-negotiable architecture rules

1. Core is authoritative. Godot displays world state and submits commands. It never implements taxation, economy, movement legality, recruitment costs, loyalty, rebellion, or other gameplay rules.
2. Use explicit state transitions. Process player actions as validated commands. Reject invalid commands without partial state changes. Return actionable error codes and enough information to explain the result.
3. Keep simulation deterministic. Same initial scenario and ordered commands must produce the same resulting state and events. No wall-clock dependence, hidden randomness, frame-rate dependence, or unordered iteration affecting results. Introduce an explicit seeded random source only when an approved mechanic requires it.
4. Preserve invariants. Validate references, ownership, adjacency, resource limits, and numeric bounds at the appropriate boundaries. Avoid exposing mutable domain collections to callers.
5. Keep rules explainable. Resource and loyalty changes must be traceable through domain events or turn summaries with the actual numbers used in calculations. Use integer arithmetic unless a documented rule requires otherwise.
6. Do not over-engineer. No ECS, DI container, event bus, networking layer, database, plugin framework, generic rules engine, or microservices unless a concrete approved requirement justifies them.
7. Separate content from rules. Keep scenario-specific values in validated data, not scattered through engine or UI code. Do not present invented borders or demographic values as historical facts.

## Current milestone policy

The repository is implementing Milestone 1. Tax changes, the deterministic economy pipeline, and loyalty/rebellion/stability processing are implemented through M1.3. The next target is M1.4: a bounded map presentation and interaction pass before recruitment and friendly movement move forward in M1.5.

For Milestone 1:

- Follow the command and turn-pipeline design in `docs/architecture.md`.
- Complete the M1.4 map foundation before adding recruitment or movement rules. Keep it to a functional terrain, overlay, zoom, label, selection, and layout pass; final art polish remains M5.
- Keep rivals passive; do not add combat, diplomacy, conquest, trade, characters, or AI as part of unrelated work.
- Keep the smallest end-to-end interaction playable in Godot: issue an action, see feedback, advance the turn, and understand why values changed.
- Do not silently advance to later milestones. If the milestone has changed, update this section and the roadmap together.

## Workflow for every coding task

1. Inspect the affected code and relevant docs before editing. Keep the requested scope small.
2. For nontrivial changes, state a brief plan and the gameplay invariant or user outcome being protected.
3. Prefer the simplest local change over new abstractions. Extend existing types and patterns where appropriate.
4. Add or update tests for domain behavior, invalid inputs, boundary cases, and regression risks. For turn logic, test ordering, arithmetic, rebellion/recovery, and deterministic replay.
5. Run the applicable checks. Never claim a build, test, or manual Godot run succeeded unless it actually ran.
6. Summarize what changed, how it was validated, remaining risks, and any design decision that needs review.

## Collaboration and pushback

- Do not silently guess when a requirement is unclear, contradictory, historically questionable, or likely to undermine the design thesis or architecture. Explain the ambiguity or concern and ask for clarification before making a consequential choice.
- Push back respectfully when you disagree with a requested design or implementation approach. Use concrete evidence from the repository, approved rules, player outcome, complexity cost, or testability; propose the smallest viable alternative.
- Distinguish factual conflicts and architectural violations from taste or balance judgment. Clearly label assumptions, recommendations, and unapproved formulas.
- Make safe, reversible local assumptions when they do not materially change behavior or scope, and state them in the handoff. Do not use minor ambiguity as a reason to stall useful work.

## Verification commands

From the repository root:

```powershell
dotnet restore Interregnum.sln
dotnet build Interregnum.sln --no-restore
dotnet test Interregnum.sln --no-build
```

For UI changes, also inspect the behavior using the Godot .NET editor when available. If Godot cannot be run in the current environment, say so and describe the manual check needed; a passing C# build does not prove correct rendering or interaction.

## Change discipline

- Do not delete or rewrite unrelated work, generated art, or scenario data.
- Do not add third-party dependencies or change engine/SDK versions without explaining the need.
- Maintain backward compatibility with existing scenario files unless a deliberate migration is agreed.
- Update tests and documentation when behavior, rules, commands, or formats change.
- Flag historical assumptions, game-balance choices, and proposed formulas for human review; do not pass them off as established requirements.
- Do not commit or push unless explicitly requested.

## Definition of done

A change is done when its intended player-visible behavior is clear, architectural boundaries remain intact, affected rules have tests, applicable checks pass (or limitations are disclosed), and the relevant documentation reflects the result.
