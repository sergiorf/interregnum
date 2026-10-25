# Directional roadmap

This roadmap describes direction, not delivery commitments. A milestone begins only after the previous slice is playable, tested, and reviewed.

## Milestone 0 — Project foundations

- .NET solution with Core, Game, and Tests projects.
- Godot 4.7.2 .NET application.
- Portable domain vocabulary and validated scenario loading.
- Fictionalized 12-province Gaul scenario.
- Selectable, inspectable province map.
- Test, build, architecture, and design documentation.

## Foundation hardening gate

- Verify reproducible local and Windows CI restore, build, and test workflows plus an honest Godot editor smoke test.
- Freeze the minimum rules vocabulary, turn/session model, timing semantics, and player experience principles.
- Harden scenario validation and defensive world snapshots before simulation begins.

This gate stabilizes the already implemented Milestone 0; it is not a new gameplay milestone.

## Milestone 1 — Playable Gaul governance simulation

- Explicit validated tax, recruit, move, and end-turn commands.
- Deterministic food, tax, military, administration, loyalty, rebellion, and stability processing.
- Player controls, action feedback, and numerical turn summary.
- Core-owned forecasts, live map state, basic keyboard/readability support, and a lightweight 15-turn survival assessment.
- Named headless balance strategies and recorded 10–20-turn human playtests.
- Tests for all rules, rejection paths, end-turn ordering, recovery, and deterministic replay.

## Milestone 2 — First complete game

- **Conflict sandbox gate:** versioned save/load, hostile movement, deterministic combat, ownership transfer, occupation pressure, and an initial legitimacy mechanic.
- **Complete campaign gate:** transparent rival policies, defeat/victory conditions, campaign summaries, and restart/reload flows.
- A small deterministic rival policy able to defend and pursue limited objectives.
- A packaged Windows build tested outside the development editor and a small external playtest.

## Milestone 3 — Deeper economy and population

- Population groups, local food pressure, infrastructure, development, and trade connections.
- Better administrative capacity and regional economic identity.

## Milestone 4 — Diplomacy and political systems

- Treaties, deeper legitimacy, succession, leading figures, internal factions, culture, and religion.
- Political consequences integrated with expansion and provincial government.

## Milestone 5 — Content, balance, and presentation

- A researched Western Europe map and expanded scenario content.
- Accessibility completion, localization readiness, onboarding, audio, art direction, animation, and performance work.
- Evaluate a supported modding surface after data formats stabilize.

See [the implementation plan](implementation-plan.md) for work packages, acceptance criteria, cross-cutting tracks, and decisions that require approval.
