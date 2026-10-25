# Milestone 1 rules vocabulary

**Status:** The M1.1 tax-command subset and provisional M1.2 economy rules are adopted; loyalty, rebellion, recruitment, and movement recommendations remain draft until their respective reviews

This document defines the minimum vocabulary and timing needed to implement Milestone 1 without hidden defaults. Items labelled **Recommendation** require human approval. Loyalty changes, rebellion thresholds, recruitment values, and stability formulas remain deliberately unresolved until their respective reviews.

## 1. Time and session model

**Recommendation:** One turn represents one year. A world begins at `StartingTurn`; its displayed year is `StartYear + Turn - StartingTurn`.

Milestone 1 has exactly one playable faction and one active actor. Only that actor may submit player commands. Non-playable factions are passive: they issue no commands, recruit no armies, and move no armies.

The player may submit any number of valid tax commands, subject to the no-op rule below, and eligible recruitment and movement commands before ending the turn. `EndTurn` processes a complete world round in a fixed order, publishes one report, advances the turn once, clears per-turn command limits, and returns control to the same playable actor.

Passive factions undergo the same deterministic production, consumption, taxation, upkeep, administration, loyalty, rebellion, and stability pipeline using their standing policies. This keeps one coherent world clock without introducing rival AI. Processing and event ordering use stable faction and province ID order.

Campaign or survival checks run only after a successful `EndTurn` has completed and the turn has advanced. Invalid commands and intermediate pipeline stages never trigger them.

## 2. Authoritative state

`WorldState` is an immutable snapshot. Commands produce either a new complete snapshot or the unchanged input snapshot. Caller-owned collections must not be able to mutate either snapshot.

The authoritative military state is the set of `Army` records. Military readiness is derived from those armies when needed.

**Recommendation:** Remove faction-level `MilitaryStrength` before simulation work because it duplicates army strength without a defined invariant. Treat the current JSON values as obsolete prototype data through an explicit, documented scenario migration; do not silently reinterpret them as recruitable manpower or a second army pool.

State needed to enforce per-turn limits, rebellion status, and deterministic recruited-army IDs must be explicit and saveable. It must not be inferred from wall time, UI state, or process-local counters.

## 3. Commands and results

The Milestone 1 command vocabulary is:

- `SetProvinceTaxRate(actor, province, taxRate)`
- `RecruitArmy(actor, province)`
- `MoveArmy(actor, army, destination)`
- `EndTurn(actor)`

Every command identifies its actor. The engine validates the actor, referenced entities, ownership, current status, resources, adjacency, and per-turn limits before applying any change.

A command result contains success or failure, a stable machine-readable error code, the resulting `WorldState`, and ordered explanatory domain events. A successful end turn additionally contains a turn report. Failure returns the exact input snapshot and emits no success events.

Expected error categories include invalid actor, unknown province, unknown army, foreign province, foreign army, invalid tax rate, unchanged tax rate, insufficient resources, ineligible recruitment location, movement already used, non-adjacent destination, non-friendly destination, and arithmetic overflow. User-facing text is presentation data and is not the stable error identity.

## 4. Tax policy

Tax rates are `Low`, `Normal`, and `High`. A province has one standing rate, which remains in effect until changed.

Changing a rate is immediate, costs no resource, and may be done more than once before `EndTurn`; only the final standing rate affects that turn's processing. A tax command is legal only for a province currently owned by the actor.

**Recommendation:** Setting a province to its existing rate is rejected with `TaxRateUnchanged`. Rejecting the no-op keeps feedback unambiguous and produces no state or event churn.

Tax revenue is collected during `EndTurn`, not when policy changes. Opening rebellion status determines whether a province pays tax for that round. A rebellion or recovery transition produced later in the pipeline applies to the following round.

For the provisional M1 balance, Low/Normal/High taxation collects 30/60/100 percent of base revenue. Multiplication occurs before division by 100 and integer division truncates the non-negative result. For example, base revenue 11 at Normal produces `11 * 60 / 100 = 6`. Loyalty effects remain unresolved for M1.3.

## 5. Food, currency, and mandatory costs

Food and currency are non-negative integer faction resources. Voluntary commands are rejected atomically when their full immediate cost cannot be paid. Milestone 1 has no loans, debt, resource trading, or negative balances.

Food is produced and consumed during `EndTurn`. Each owned province adds its `AgriculturalProduction` to the faction pool and demands `ceil(Population / 4)` food. Production is added before consumption. A faction-wide shortage for that round is the unmet consumption after all available food is consumed. Shortage is a reported turn outcome, not assigned to an arbitrary province and not stored as a permanent `Famine` flag. Its loyalty and stability consequences remain an M1.3 decision.

Military upkeep and provincial administration are mandatory end-turn obligations. Tax is added first, then army upkeep is paid in stable army-ID order, followed by administration in stable province-ID order. Each obligation receives as much of the remaining non-negative treasury as possible. The report exposes due, paid, and unpaid amounts. M1.2 does not remove armies or apply a hidden penalty; consequences for unpaid obligations remain an explicit M1.3 decision.

All arithmetic uses checked integers. An overflow rejects `EndTurn` atomically with an unchanged state and no events.

## 6. Rebellion and recovery

Rebellion is an explicit province condition. Ownership does not change in Milestone 1, no rebel faction or army is created, and no combat occurs.

An opening rebel province produces food, pays no tax, still creates its approved administrative effects, and contributes its approved stability penalty. Tax policy may still be changed there so the player can pursue recovery. Recruitment from a rebel province is not allowed.

Loyalty changes are calculated during `EndTurn`, clamped to the approved bounds, and fully attributed in events and the report. Rebellion and recovery transitions occur after loyalty changes. Their thresholds must use hysteresis or another approved rule that prevents turn-by-turn oscillation.

The rebellion threshold, recovery threshold, tax effects, shortage effects, administrative interaction, and stability formula remain unresolved M1.3 decisions.

## 7. Recruitment

Recruitment creates one basic army in an eligible actor-owned, non-rebel province. Its currency cost, strength, upkeep, eligible settlement rule, and any realm or province cap are scenario/rules values to approve for M1.4. There is no variable unit designer or army type system.

The full recruitment cost is paid immediately. The recruited army exists immediately and may move once in the same turn.

**Recommendation:** A newly recruited army owes upkeep at the next `EndTurn`, including when it was recruited earlier in that same command phase. The forecast must show both the immediate cost and upcoming upkeep.

Recruited IDs are generated deterministically from explicit world state and never reused. Their ordering and format must be covered by replay tests and later persisted by saves.

## 8. Friendly movement

An army may move only to a directly adjacent province owned by the same faction. Movement has no resource cost and causes no combat in Milestone 1.

**Recommendation:** Each army may complete at most one successful move per turn. Failed moves do not consume that allowance. A newly recruited army may use its one move. `EndTurn` clears movement usage after processing.

Legal destinations are a Core-owned query over the current snapshot. The UI may display the result but may not recalculate movement legality.

## 9. End-turn order

The fixed pipeline is:

1. Food production.
2. Food consumption and shortage calculation.
3. Tax revenue using opening rebellion status.
4. Military upkeep.
5. Administrative expenditure.
6. Loyalty changes, including shortage effects.
7. Rebellion and recovery transitions.
8. Faction stability changes.
9. Turn advancement, per-turn-limit reset, campaign check, and report publication.

Every stage completes for all affected factions in stable ID order before the next stage begins. Events are emitted in pipeline stage, faction ID, province or army ID order. No stage reads unordered iteration as a tie-breaker.

Stages 1–5 and 9 are implemented in M1.2. Stages 6–8 remain reserved in this order for M1.3 and do not yet change state.

## 10. Turn report reconciliation

For each faction, the report records at least:

- opening and closing food;
- production, consumption, and unmet consumption;
- opening and closing treasury;
- tax revenue by province and in total;
- military upkeep by army and in total;
- administration by province and in total;
- loyalty changes by province and cause;
- rebellion and recovery transitions;
- opening and closing stability plus each modifier.

Totals must reconcile exactly with their details and with the resulting snapshot. Zero-valued stages retain a visible total so the player can distinguish “none” from missing processing.

## 11. Decisions still requiring approval

F0.2 approval is requested for:

1. One-year turns and the year-display formula.
2. One persistent playable actor and passive rivals processed by the same end-turn pipeline.
3. Removal of faction-level `MilitaryStrength` in favor of armies.
4. Free, repeatable tax changes with rejected no-ops.
5. Non-negative food and treasury, with unaffordable voluntary commands rejected.
6. Explicit rebellion with opening-status timing and recovery through policy.
7. Immediate recruitment with same-round upcoming upkeep and one allowed movement.
8. One successful move per army per turn.
9. Post-turn-only campaign checks and the proposed deterministic event ordering.

Approval of this vocabulary does **not** approve economy coefficients, insufficient-treasury consequences, rebellion/recovery thresholds, recruitment values, or stability formulas. Those values require worked examples before their respective implementation packages.
