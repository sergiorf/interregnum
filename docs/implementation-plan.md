# Interregnum implementation plan

**Status:** Adopted working implementation plan; unresolved rules and balance values still require explicit approval

**Baseline inspected:** `main` at `ac6b4f62782b7b3e87a4e8753960be866637e717` (2026-10-24)

**Working method:** Implement one reviewed work package at a time. Do not automatically begin the next milestone.

## 1. Executive direction

Interregnum is a single-player, turn-based, province-level historical strategy game about governing territory during the fragmentation of the Western Roman Empire. Its defining thesis is that conquest is easier than governance. It should demonstrate an enjoyable 12-province campaign before attempting to become a broad grand-strategy simulation.

Retain the six directional milestones with these adjustments:

1. Treat Milestone 1 as a playable economic and governance prototype with a limited survival challenge, not a finished game.
2. Treat Milestone 2 as the first complete game: war, occupation, simple rivals, win/loss, save/load, and one early legitimacy mechanic.
3. Introduce usability, balancing, accessibility fundamentals, testing, and packaging incrementally rather than postponing quality until Milestone 5.
4. Introduce political identity early through legitimacy and occupation, while reserving deeper diplomacy, succession, culture, and religion for Milestone 4.

### Product and technology constraints

- Initial target: offline, single-player Windows desktop with a 2D province map. Other platforms are future possibilities, not current commitments.
- `Interregnum.Core` is the pure C# 14 / .NET 10 authoritative simulation and remains independent from rendering.
- `Interregnum.Game` is the Godot 4.7.2 .NET presentation layer. It never calculates gameplay legality or effects.
- Tests use xUnit, supplemented by focused human Godot smoke tests.
- Scenarios use strict, human-readable JSON with explicit IDs and validated references. Geography and historical borders in `gaul-480.json` remain fictionalized prototypes.
- Preserve the modular monolith. Do not add a server, ECS, dependency-injection framework, event bus, database, generic rules engine, or LLM-driven opponents without demonstrated need.
- The same scenario plus the same ordered commands must produce precisely the same world state and event sequence.
- Every milestone decision should answer: what does the player gain, what does it cost, and what new decision does it create?

## 2. Baseline findings

The repository currently contains immutable-looking domain records, typed IDs, a validated scenario with 12 provinces, 3 factions and 3 armies, a selectable styled map, province details, and four scenario-loading tests. It has no turn engine, action processing, economy, rebellion, combat, AI, persistence, or victory logic.

Known issues to resolve:

- `ProvinceMap` draws starting ownership from `ProvinceDefinition`; it must eventually combine static scenario geometry with live `WorldState` ownership and conditions.
- `Faction.MilitaryStrength` overlaps with the sum of deployed `Army.Strength`, while the Roman starting values differ (34 versus 24). Define it as a distinct reserve/capacity or remove it; never double count it.
- `WorldState` accepts read-only interfaces without taking a defensive snapshot, and province neighbor sets can retain mutable backing collections.
- The current Godot UI uses fixed positions and one-time labels; simulation requires explicit refresh paths and scalable layout.
- Static balance may remove strategic tension. Roman full base tax is 79, administrative burden is 28, and army upkeep is 8, producing a theoretical +43 treasury delta before food and policy modifiers. Balance must be evaluated across multi-turn strategies, not isolated formulas.

## 3. Document ownership and execution rules

- `docs/roadmap.md`: concise directional milestones and gates.
- `docs/implementation-plan.md`: work packages, sequencing, and acceptance criteria.
- `docs/gameplay-principles.md`: player fantasy, intended decisions, target experience, and non-goals.
- `docs/rules-v1.md`: approved M1 actions, exact arithmetic, turn ordering, timing, and boundary cases.
- `docs/balance.md`: named strategies, simulated outcomes, playtest observations, and tuning history.
- `docs/architecture.md`: implemented technical contracts, ownership boundaries, state transitions, and format/version decisions.
- `AGENTS.md`: stable agent constraints and links to the specifications; avoid duplicating the specifications there.

For every task: inspect code and relevant documentation, state the player outcome and protected invariants, implement the smallest vertical slice, add tests, run applicable checks, exercise Godot for UI changes when available, and document results and limitations.

A milestone passes only when its functionality works, automated checks pass, a human can exercise the end-to-end flow, consequences are explainable, balance/playtest notes exist, documentation agrees with behavior, and no unrelated feature creep was introduced.

## 4. Foundation hardening gate

Milestone 0 is already the implemented project foundation. These packages are a hardening gate before Milestone 1, not a claim that M0 is incomplete.

### F0.1 — Establish the baseline and CI

- Verify the pinned .NET 10 SDK and Godot .NET editor in the actual development environment.
- Run solution restore, build, and tests.
- Add a basic GitHub Actions Windows workflow for restore, build, and test with appropriately pinned versions.
- Verify Godot launch manually and record limitations; a C# build does not prove editor or rendering behavior.
- Remove non-project artifacts only after confirming they are accidental.

**Acceptance:** Local and CI checks are reproducible, editor launch instructions are accurate, and known limitations are recorded.

### F0.2 — Freeze the minimum rules vocabulary

Decide and document:

- Turn duration (initial proposal: one year until playtest pacing suggests otherwise).
- The meaning or removal of faction-level military strength.
- Food, currency, debt, famine, tax, and rebellion semantics.
- The session model: active actor, commands allowed before end turn, what a world round means, and how passive rivals later become active.
- Timing semantics for recruitment upkeep, rebellion/recovery, movement limits, and campaign checks.

Write `docs/gameplay-principles.md` and draft `docs/rules-v1.md`. Record unresolved questions rather than allowing implementation to select hidden defaults.

**Acceptance:** The vocabulary needed to model and validate M1 has been explicitly approved; no simulation code is added as part of this package.

### F0.3 — Harden scenario and state boundaries

**Implementation status:** Complete locally; validation, aliasing, build, and test gates passed on 2026-10-25. Interactive Godot verification remains tracked in `docs/foundation-verification.md`.

- Reject or clearly report unknown fields, invalid enum values, explicit null collections, missing references, duplicate IDs, malformed/non-finite polygon coordinates, and invalid economic ranges.
- Decide whether syntax/deserialization failures and semantic validation failures share one public error shape; keep errors actionable and aggregate semantic errors where feasible.
- Enforce the approved playable-faction invariant.
- Ensure snapshots and province neighbor collections cannot be mutated through caller-owned backing collections.
- Keep static map geometry separate from mutable political state without introducing a content framework.

**Acceptance:** Hostile-input and aliasing tests cover these boundaries, while the existing Gaul scenario still loads unchanged unless an approved migration is required.

## 5. Milestone 1 — Playable Gaul governance simulation

**Objective:** The player can spend 10–20 turns making meaningful and understandable taxation, food, recruitment, movement, and loyalty decisions. Rivals remain passive; there is no hostile war or ownership transfer.

### M1.1 — Commands, results, atomic transitions, and first tax slice

**Implementation status:** The tax vertical slice is implemented locally as of 2026-10-25: Core command vocabulary, atomic tax transitions, stable results/events, tests, and Godot tax controls. Restore, build, 23 tests, and a two-frame Godot 4.7.2 headless scene launch pass. M1.1 remains open for interactive visual/input verification and the turn-report contract needed by end-turn processing.

Introduce the smallest useful Core command surface, expected to include explicit tax, recruit, move, and end-turn commands; a simulation engine; structured command results; domain events; and turn reports. Exact filenames should follow the simplest local organization.

- Commands include actor identity and validate the active/playable actor and target.
- One engine entry point returns stable error codes, the resulting snapshot, and explanatory events.
- Invalid commands do not mutate state, consume resources, advance the turn, or emit success events.
- Preserve existing IDs and define deterministic recruited-army IDs using state that can later be saved.
- Define whether repeated/no-op tax commands succeed, return a no-op, or are rejected.
- Exclude wall time, hidden randomness, and unordered enumeration from results.
- First vertical slice: change tax policy on a selected player-owned province in Godot and show the live choice immediately.

**Tests:** Success, foreign/unknown province, invalid tax, invalid actor, approved repeated-command behavior, snapshot isolation, and stable error results.

**Acceptance:** Tax changes work end to end without Godot implementing legality or effects.

### M1.2 — Approved economy and one end turn

Freeze exact order and integer arithmetic in `rules-v1.md`. A candidate pipeline is:

1. Food production.
2. Food consumption and shortage calculation.
3. Tax revenue under policy and opening rebellion/famine status.
4. Military upkeep.
5. Administrative expenditure.
6. Loyalty changes and shortage penalties.
7. Rebellion/recovery transitions.
8. Faction stability updates.
9. Turn advance and report publication.

Explicitly define whether status changes affect the current or following turn. The natural interpretation of this order is that opening status controls current revenue and transitions affect the next turn, but this is not approved until `rules-v1.md` says so.

Trial coefficients such as Low/Normal/High revenue of 30/60/100 percent and loyalty changes of +4/−1/−6 are balance proposals, not canon. Specify truncation, clamping, starvation, overflow handling, and whether resources may become negative.

Turn reports must reconcile opening balance, production, consumption, tax, upkeep, administration, unrest losses, and closing balance with explicit causes. Update realm status and the selected province after each turn.

**Tests:** Exact fixed-fixture arithmetic, stable event ordering, rounding boundaries, shortage/zero food, atomic failure, and 20-turn repeatability.

**Acceptance:** End Turn changes state deterministically and displayed balances reconcile exactly with Core-owned reports.

### M1.3 — Loyalty, rebellion, and recovery

- Represent rebellion explicitly or justify a derived representation.
- Use a reviewed threshold plus hysteresis or another rule that prevents oscillation.
- Rebels pay no tax and affect stability, but do not form an independent faction or transfer ownership in M1.
- Make every loyalty cause visible.
- Add at-risk/rebellion map status and alerts.

**Tests:** Threshold crossings, opening-versus-new rebellion revenue timing, recovery, multiple unstable provinces, boundaries, and replay stability.

**Acceptance:** The player can deliberately trigger and reverse rebellion through visible policy choices.

### M1.4 — Recruitment and friendly movement

- Recruitment has an explicit cost, strength, upkeep, location, deterministic unique ID, eligibility rule, and resource check.
- Define recruitment caps and the phase in which new upkeep begins.
- Movement permits an existing player army to move only to an adjacent friendly province.
- Define and centrally enforce any per-turn movement restriction.
- Display location, strength, upkeep, and Core-provided legal destinations.
- Give military readiness a small role in the survival assessment so recruitment is not automatically pointless before combat; do not add combat or an elaborate threat system.

**Tests:** Insufficient resources, invalid/repeated movement, foreign actor, valid movement, ID stability, caps, and upkeep timing.

**Acceptance:** The player can recruit and move an army with complete cost and legality feedback.

### M1.5 — Stable UI and Core-owned forecasts

- Add a lightweight Godot session/controller that owns the engine and publishes refreshed snapshots; keep `Main` as composition root.
- Extract stable panels when useful, without creating a UI framework.
- Render scenario geometry together with live ownership and condition data.
- Establish the functional map-rendering stack: geographic ocean and land masks, subdued shaded relief, generalized terrain/land-cover blending, physical rivers and coastlines, then translucent province ownership and gameplay-status overlays.
- Support bounded cursor-centered zoom, panning, reset controls, and zoom-aware selection. Use level-of-detail rules so labels and terrain decoration remain legible rather than merely scaling every element.
- Add small Core-owned calculations or queries for tax forecasts, recruitment costs/upkeep, and legal destinations. Do not duplicate formulas in Godot.
- Show policy, rebellion risk, resource forecasts, rejection messages, and the last turn breakdown.
- Add an initial pass for keyboard use, readable fonts, and scalable layout.

**Acceptance:** A player understands why an action succeeded, failed, or changed a resource without reading source code.

### M1.6 — Balance and limited survival challenge

- Add a small test-based or `tools/` headless harness for named strategies: all low, all normal, all high, mixed, aggressive recruitment, and unrest recovery.
- Target provisional behavior: low tax preserves loyalty but restricts military capacity; high tax gives immediate income but causes later unrest; adaptive play can outperform either extreme.
- Add a lightweight optional 15-turn survival result rather than a full victory subsystem. Assess treasury, stability, rebellions, and military readiness against reviewed collapse criteria.
- Conduct manual 10–20-turn sessions and record confusing actions, dominant strategies, and coefficient changes in `docs/balance.md`.

**Exit gate:** A tested 10–20-turn governance loop has nontrivial policy choices, explainable deterministic reports, unrest/recovery, recruitment/movement, and at least one recorded playtest.

## 6. Milestone 2 — First complete game

**Objective:** Deliver a shareable small campaign with war, occupation, simple rivals, legitimacy, save/load, and coherent victory and defeat.

Before implementation, approve combat, occupation, legitimacy, campaign, AI-turn, and save-state vocabulary. This avoids persisting speculative state or letting combat behavior emerge accidentally.

### M2A — Conflict sandbox

#### M2.1 — Persistence and campaign integrity

- Use explicit versioned save DTOs rather than serializing `WorldState` directly.
- Store scenario ID plus a content version/compatibility identifier, turn/session state, deterministic ID counters, command limits, and approved political state.
- Store files under Godot's user directory; storage and platform concerns remain outside pure rules.
- Validate rehydrated state and report corrupt, incompatible, or missing scenarios gracefully.
- Define early whether incompatible saves are rejected or migrated.

**Acceptance:** Saving, restarting, and continuing produces identical behavior without duplicate IDs or replay drift.

#### M2.2 — Hostile movement and deterministic combat

- Define one adjacent attack order and one deterministic resolution formula.
- Specify defender bonuses, multiple armies, casualties, retreat/destruction, capture, resource effects, and per-turn attack restrictions.
- Keep armies and ownership valid through capture and reconquest.
- Show defenders, a legible expectation/risk indicator, and battle results.

**Tests:** Ties, casualty boundaries, captures, invalid attacks, defeated-army removal, adjacency, and determinism.

#### M2.3 — Occupation and first legitimacy mechanic

- Captured provinces begin with a visible occupation state that increases governing pressure and reduces loyalty or tax efficiency.
- Introduce one realm legitimacy measure affected by a small approved set such as stability, rebellion, and occupation.
- Make overexpansion costly through administration, garrison pressure, unrest, or another single legible model.
- Report occupation duration and legitimacy changes.

**Tests:** Occupation start/expiry, reconquest, penalties, legitimacy boundaries, and unrest interaction.

**M2A gate:** A saved conflict sandbox supports deterministic capture, occupation, reconquest, and restoration without invalid state.

### M2B — Complete campaign

#### M2.4 — Transparent rival AI

- Give each rival one simple deterministic policy using the same validated commands as the player.
- Use stable priorities and tie-breakers for survival, shortages, affordable recruitment, defense, and limited opportunism.
- Produce developer-visible explanations for AI choices.
- Run complete headless campaigns to detect loops, passivity, and runaway advantages.

**Acceptance:** Both rivals defend and make limited attacks without hidden bonuses or opaque decisions.

#### M2.5 — Win/loss and campaign completion

- Define a reviewed objective such as holding key provinces with minimum legitimacy at an end date, plus clear collapse conditions.
- Evaluate conditions only at approved transition boundaries, never during invalid commands or halfway through turn resolution.
- Show progress, a campaign summary, and restart/reload options.

**Acceptance:** Campaigns terminate coherently and players can understand success or failure in the UI.

#### M2.6 — Packaged build and external playtest

- Export a Windows build and validate resources, fonts, controls, save/load, and error logging.
- Test on a second machine or clean environment.
- Observe a small external group for first-turn comprehension, consequences, challenge, and pacing.
- Refine the 12-province campaign before expanding the map.

**Exit gate:** Players can start, save, resume, win, lose, and complete a small campaign against understandable rivals. The product question is: would someone voluntarily play a second campaign?

## 7. Milestone 3 — Economic depth and regional differences

**Objective:** Make provinces qualitatively different without burying the player in statistics.

1. Review M2 evidence and name actual missing decisions before adding mechanics.
2. Add only 2–3 competing infrastructure investments with upfront cost, maintenance, delayed return, and visible province-specific value.
3. Consider evolving the realm food pool toward local pressure and limited adjacency-based supply only if logistics are strategically trivial in M2.
4. Introduce at most two population characteristics initially, each changing a concrete action or consequence.
5. Distinguish fixed administrative overhead from approved scaling pressure such as size, distance, or institutional capacity.

**Exit gate:** Neighboring provinces require meaningfully different policies, choices matter over several turns, and the added complexity remains explainable.

## 8. Milestone 4 — Politics, diplomacy, succession, and identity

**Objective:** Make authority, loyalty, and identity as strategically important as armies.

1. Deepen the approved legitimacy model and introduce one internal constituency with one meaningful demand.
2. Begin diplomacy with a few transparent actions such as truce, tribute, and non-aggression/alliance, using deterministic acceptance criteria and persistent state.
3. Add a minimal ruler/succession model as an occasional strategic event rather than a character simulator.
4. Add culture or religion selectively, grounded in reviewed sources and clearly separated from fictional design values.
5. Test whether legitimate small states and aggressive expanding realms offer distinct viable approaches.

**Exit gate:** Interregnum has a recognizable post-Roman political identity, at least two viable approaches to survival, and no dominant exploit that negates governance.

## 9. Milestone 5 — Content, presentation, and release readiness

**Objective:** Expand a validated design into a documented, installable, comprehensible, and stable release candidate.

1. Define schema evolution and a practical content-authoring workflow with research notes and attribution.
2. Expand beyond stylized Gaul only after the 12-province campaign succeeds; make a second campaign strategically different, not merely larger.
3. Refine the layered terrain map with reviewed texture families, transitions, rivers, relief, zoom-level detail, ownership readability, tooltips, animation, audio, and a coherent historical-atlas art direction. Track provenance and licensing for source and generated assets.
4. Complete keyboard navigation, scalable fonts, accessible color cues, appropriate remapping, localization-ready strings, and Windows scaling checks.
5. Benchmark turns, redraws, and save/load; define save migration policy, privacy-conscious diagnostics, release builds, and a repeatable QA plan.
6. Decide whether demand justifies mod support. Start with documented scenario JSON; a full mod API remains optional.

**Exit gate:** New players can install, understand, and reliably play the intended release scope.

## 10. Cross-cutting tracks

### Testing and determinism

- Unit-test commands, calculations, transitions, rejection paths, and boundaries.
- Contract-test scenario and save versions.
- Use canonical, explicitly ordered serialization for replay/state hashes.
- Test invariants with ordinary deterministic fixtures/loops before adding a property-testing dependency.
- Run named headless strategies and manual Godot smoke tests for player-facing slices.

### Balance and player experience

Track named strategies over 10, 20, and later 40 turns. Treat metrics as diagnostic evidence, not proof of fun. Record whether each feature improves decisions or merely adds cognitive load, and include human playtests from M1 onward.

### UI and documentation

Every rule needs a visible action or state, a readable effect explanation, a report when it applies, and a tested rejection path. UI forecasts use Core-owned calculations or snapshots.

### Map rendering and terrain

Use a layered province-scale map rather than a tactical tile map or one opaque terrain image:

1. Ocean color or texture, optionally including subdued bathymetry.
2. A geographic land mask and coastline derived from reviewable map data.
3. Elevation or shaded-relief raster aligned to the same projection.
4. Generalized terrain masks that blend a small texture vocabulary such as plains, forest, hills, mountains, and arid land. Prefer channel-packed masks and shader blending over hard-edged stacked images.
5. Physical rivers, lakes, coastlines, and restrained terrain decoration appropriate to the current zoom level.
6. Semi-transparent faction, province, selection, rebellion, and other gameplay overlays.
7. Labels, settlements, armies, controls, and feedback above the geographic layers.

The Godot client owns this presentation pipeline. Terrain remains non-authoritative unless a separately approved rule later gives it mechanical effects; Core must never infer gameplay from pixels, textures, or shaders. Keep geographic source data, scenario borders, and live political state separate so each can be revised independently.

Use real elevation and physical geography where practical, but treat reconstructed land cover for 480 AD as generalized art direction rather than established fact. Record the source, version, extraction process, license or public-domain status, and any generated-art provenance for every committed map asset. Do not show modern political borders in the historical presentation.

M1.5 delivers a readable first pass with working zoom/pan, relief, terrain blending, and ownership overlays. M5 owns final texture quality, transitions, zoom-level decoration, accessibility tuning, performance budgets, and cohesive art direction. Manual Godot checks must cover coastline alignment, seams, label behavior, selection accuracy at every zoom level, and ownership readability over each terrain type.

### Architecture and complexity budget

Retain the Core/Godot boundary, modular monolith, and explicit commands. Add an abstraction only after concrete uses justify it. Multiplayer is not a current objective and must not influence the offline architecture accidentally.

## 11. Summary and gates

| Milestone | Essential slices | User-visible proof | Go/no-go question |
| --- | --- | --- | --- |
| Foundation gate | Baseline, vocabulary, validation | Reliable load and reproducible checks | Can we safely extend it? |
| M1 | Commands, economy, unrest, armies, UI, balance | 10–20 turns of governance | Are resource and loyalty decisions interesting? |
| M2A | Saves, combat, capture, occupation | Restorable conflict sandbox | Does conquest create governing pressure? |
| M2B | Legitimacy, AI, goals, export | Complete campaign with wins/losses | Would someone play a second campaign? |
| M3 | Development, supply, population, administration | Provinces require different policies | Does added complexity improve decisions? |
| M4 | Politics, diplomacy, succession, identity | Multiple ways to preserve a realm | Does it feel distinctively post-Roman? |
| M5 | Content pipeline, map, art, onboarding, QA | Installable release candidate | Is it coherent and enjoyable for new players? |

## 12. Decisions requiring approval

1. Turn duration: one year, season, or abstract turn?
2. Is faction military strength recruitable capacity, a reserve, or a redundant aggregate?
3. What are the exact tax, loyalty, clamping, rounding, rebellion, and recovery rules?
4. What happens during bankruptcy, famine, food shortage, and inability to pay upkeep?
5. What limits tax changes, movement, recruitment, and later attacks?
6. How does M1 rebellion work, and how does it evolve into M2 contested territory?
7. What is the active-actor/world-round order, and when are campaign conditions evaluated?
8. What makes the first campaign winnable, losable, and replayable?
9. Which minimal legitimacy mechanism best conveys the post-Roman setting?
10. What research, fictionalization, attribution, and licensing policy governs historical content?
11. What public error shape covers JSON syntax, deserialization, and semantic validation failures?
12. What canonical representation defines replay equality and state hashes?

## 13. Immediate sequence

1. Review and approve the open decisions relevant to the next work package.
2. Write and approve `docs/gameplay-principles.md`.
3. Execute F0.1 baseline checks and CI, including an honest Godot-editor result.
4. Draft and approve `docs/rules-v1.md` for the F0.2 vocabulary decisions.
5. Execute F0.3 scenario and snapshot hardening.
6. Implement M1.1's tax-policy vertical slice.

Before M1.2, freeze exact rules with worked examples. At the end of M1, stop and evaluate gameplay before starting combat. Repeat that gate after M2A, M2B, M3, and M4.

> Build a small game that is strategically interesting and technically robust before expanding its territory, populations, politics, or visual polish.
