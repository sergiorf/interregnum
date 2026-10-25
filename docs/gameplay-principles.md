# Gameplay principles

**Status:** Proposed for Foundation F0.2 review

## Player promise

The player leads a fragile post-Roman polity whose territory is easier to take than to govern. The first playable slice should make every expansion-shaped benefit carry a visible governing cost: provinces provide food, tax, and strategic position, while also demanding administration, military protection, and politically sustainable taxation.

Interregnum should reward deliberate compromise rather than a single permanently correct policy. The player should be able to recover from a poor choice when they recognize it early, but repeated short-term extraction should create consequences that take several turns to reverse.

## Decisions in the first playable slice

Each turn asks the player to make a small set of legible decisions:

- Which provinces can tolerate higher taxation, and which need relief to preserve or restore loyalty?
- Is the realm secure enough to save currency, or is recruitment worth its immediate cost and future upkeep?
- Where should the limited army presence be positioned within friendly territory?
- Is the realm's current income sustainable after food, upkeep, and administrative obligations?

These choices should interact. Taxation must not be an isolated income slider, armies must not be free score, and rebellion must be a foreseeable result of governing pressure rather than an arbitrary punishment.

## Information and feedback

Rules are authoritative in `Interregnum.Core`. The interface displays state, submits commands, and explains results; it does not reproduce legality or arithmetic.

Before an action, the player should see its immediate cost, relevant restrictions, and a Core-owned forecast where one is useful. After an action, success or rejection should be explicit. After ending a turn, the player should be able to reconcile opening and closing food, treasury, loyalty, rebellion, and stability from the reported integer values and causes.

No important consequence should depend on hidden randomness, wall-clock time, frame rate, or iteration order. The same scenario and ordered commands must produce the same state and event sequence.

## Pacing and session target

The Milestone 1 target is a 10–20 turn governance session. One turn is proposed to represent one year, giving the slice a 10–20 year narrative span without claiming historical precision. This duration should be revisited if playtesting shows that policy recovery feels implausibly fast or decisions feel too sparse.

The map remains small enough that the player can understand each province. Additional systems should earn their place by creating a new governing decision, not merely by adding detail.

## Difficulty and recovery

Pressure should come from competing obligations and accumulated policy effects, not opaque bonuses for rivals. Low taxation should aid loyalty but constrain spending. High taxation should create immediate capacity at a delayed political cost. Recruitment should improve readiness while reducing current and future fiscal flexibility.

The first mistakes should be recoverable. Rebellion is a serious loss of revenue and stability, but in Milestone 1 it remains an internal condition that can be reversed through visible policy; it does not create a new faction, combat, or ownership transfer.

## Historical framing

The scenario is a fictionalized strategy model inspired by Gaul around 480 AD. Province values, borders, population units, and political control are gameplay abstractions and must not be presented as settled historical facts. Later historical detail requires research and attribution rather than inference from the prototype data.

## Milestone 1 non-goals

Milestone 1 does not include hostile movement, combat, conquest, diplomacy, trade, rival AI, characters, succession, culture, religion, technology, infrastructure, save/load, or a full victory system. Rivals provide geographic context and undergo only the deterministic passive processing approved in the rules.

Do not add a framework in anticipation of those systems. The desired result is one understandable governance loop, not a broad simulation shell.

## Review questions

F0.2 is approved only after a human confirms:

1. Does one turn represent one year?
2. Should faction-level `MilitaryStrength` be removed in favor of armies as the sole military measure?
3. Should passive rivals undergo the same end-turn economy while taking no commands?
4. Are the timing and command-limit recommendations in `rules-v1.md` suitable for the first prototype?

