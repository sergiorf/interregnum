# Directional roadmap

This roadmap describes direction, not delivery commitments. A milestone begins only after the previous slice is playable, tested, and reviewed.

## Milestone 0 — Project foundations

- .NET solution with Core, Game, and Tests projects.
- Godot 4.7.2 .NET application.
- Portable domain vocabulary and validated scenario loading.
- Fictionalized 12-province Gaul scenario.
- Selectable, inspectable province map.
- Test, build, architecture, and design documentation.

## Milestone 1 — Playable Gaul simulation

- Explicit validated tax, recruit, move, and end-turn commands.
- Deterministic food, tax, military, administration, loyalty, rebellion, and stability processing.
- Player controls, action feedback, and numerical turn summary.
- Tests for all rules, rejection paths, end-turn ordering, recovery, and deterministic replay.

## Milestone 2 — Territorial conflict and simple AI

- Hostile movement, resolved combat, ownership transfer, and occupation pressure.
- A small deterministic rival policy able to defend and pursue limited objectives.
- Defeat/victory conditions suitable for the scenario.

## Milestone 3 — Deeper economy and population

- Population groups, local food pressure, infrastructure, development, and trade connections.
- Better administrative capacity and regional economic identity.

## Milestone 4 — Diplomacy and political systems

- Treaties, legitimacy, succession, leading figures, internal factions, culture, and religion.
- Political consequences integrated with expansion and provincial government.

## Milestone 5 — Content, balance, and presentation

- A researched Western Europe map and expanded scenario content.
- Save/load, accessibility, localization readiness, onboarding, audio, art direction, animation, and performance work.
- Evaluate a supported modding surface after data formats stabilize.
