# Game design

## Premise

In 480 AD, imperial authority in Western Europe is failing. The player leads a surviving Roman polity amid rival kingdoms and must turn fragile control into a state that can endure.

The design thesis is: **conquering territory is easier than governing it.** Expansion adds food, tax base, manpower, and strategic position, but also administrative expense, culturally or politically fragile provinces, and military commitments.

## Core gameplay loop

For the first playable slice, the intended loop is:

1. Inspect provinces and the realm's resources.
2. Choose taxation, recruitment, or an adjacent friendly army move.
3. End the turn.
4. Read an exact account of production, consumption, revenue, upkeep, administration, and loyalty change.
5. Adjust policy to balance immediate survival against long-term stability.

## Milestone 0 mechanics

The foundation provides content, not a simulation turn:

- A 12-province stylized Gaul campaign shown within a broader Western Europe map.
- Roman, Visigothic, and Frankish factions.
- Province ownership, economy inputs, loyalty, administration, settlements, and adjacency.
- Starting faction resources and armies.
- Selection and inspection in Godot.

These values establish the vocabulary and give the next mechanics something visible to operate on.

## Milestone 1 mechanics

The first meaningful decision is taxation. Low tax sacrifices income to restore loyalty, normal tax is broadly sustainable, and high tax exchanges loyalty for immediate treasury. Below a fixed loyalty threshold, a province deterministically rebels: it pays no tax and penalizes realm stability. Lower taxation over later turns can recover it.

Food is produced by provinces and consumed by population. The realm also pays military upkeep and administrative cost. Recruitment spends treasury and creates one basic army. Armies move only between adjacent friendly provinces. Passive rivals establish the strategic context without requiring AI yet.

Every end-turn result must explain its arithmetic. The player should be able to trace a treasury or loyalty change without consulting source code.

## Simplifications

- Province values are abstract integers, not demographic estimates.
- One settlement at most per province.
- One food pool and one currency per faction.
- One basic army type; no tactical combat.
- Rivals are passive in Milestone 1.
- Rebellion has no rebel AI, probability, or battle system.
- Geography and 480 AD political control are fictionalized.
- No culture, religion, characters, succession, diplomacy, technology, trade network, or victory condition yet.

## Future directions

Future work may add conquest and AI, differentiated population, trade, institutions, diplomacy, succession, internal politics, cultural and religious identity, historical events, save/load, and mod support. Each should deepen the governing-versus-expansion tension rather than exist as an isolated feature list.
