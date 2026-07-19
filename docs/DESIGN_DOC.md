# Cattle Ranch — Game Design & Build Plan

*Working title. A mid-depth, 2.5D isometric cattle ranching simulation. Solo build in Unity.*

---

## 1. Vision

You inherit a small, run-down ranch and a handful of cattle. Over many in-game seasons you build a bloodline, learn to read your grass, and survive the market and the weather. The fantasy is **stewardship**: watching a herd and a piece of land improve under your care, one calving season at a time.

The game is not a clicker and not a hardcore agribusiness spreadsheet. It sits in the middle — real, meaningful tradeoffs delivered through a clean, readable interface. If a player walks away understanding *why* you don't overstock a pasture in a dry year, the design succeeded.

**One-line pitch:** *Stardew Valley's warmth meets a real cattle economy — grow a herd, read the land, beat the market.*

---

## 2. Design Pillars

Every feature decision gets checked against these four. If it doesn't serve a pillar, cut it.

1. **The land is alive.** Grass, water, and soil are finite and recover slowly. The player's central discipline is not exhausting them.
2. **Every animal is an individual.** Cattle have genetics, age, and health you can see and shape. A favorite cow should be nameable and memorable.
3. **The market bites back.** Prices swing. Selling is a decision with regret attached — sell now for safety or hold and gamble.
4. **Time is the pressure.** Seasons are a clock you can't stop. Winter is coming whether your hay barn is full or not.

---

## 3. Core Gameplay Loop

**Macro loop (a game year):**

```
Spring  → calving, turn cattle onto fresh grass, buy/plan
Summer  → grazing rotation, cut & bale hay, heat/water management
Fall    → wean calves, market decision (sell or hold), breed
Winter  → feed from stores, survive weather, minimal income
        ↺ repeat, herd & land carried forward
```

**Micro loop (a play session / in-game week):**

1. Check herd status (health, weight, pregnancies) and pasture condition.
2. Make the week's calls: move cattle to a new paddock, feed hay, call the vet, buy/sell.
3. Advance time; weather and events resolve.
4. Read the outcome, adjust. Bank cash or spend on improvements.

The reward that keeps players in the loop is **generational progress** — better genetics, healthier land, a bigger operation — layered over the tension of **not knowing what the season will bring.**

---

## 4. Core Systems

Five systems carry the game. They're interdependent by design — the fun lives in the tension between them.

### 4.1 The Herd & Genetics
- Each animal is an object with: ID, name (optional), sex, age, breed, weight, body-condition score, health state, temperament, and pregnancy status.
- **Heritable traits** (0–100 with variance): weight-gain/growth, calving ease, marbling/meat quality, milk (mothering), heat tolerance, disease resistance, fertility.
- **Breeding:** offspring traits = blended parent averages ± random variance, with a small chance of a standout ("hybrid vigor") or a dud. This is the long-game hook — a visibly improving bloodline over 10+ generations.
- Calving season has risk: complications scale down with the dam's calving-ease trait and up with a mismatched bull.

### 4.2 Land & Grass
- Ranch divided into **paddocks**. Each tracks: grass biomass, forage quality, soil health, water access.
- **Stocking rate** is the master dial: too many head strips the grass faster than it regrows, degrading soil for future seasons; too few wastes capacity and money.
- **Rotational grazing:** move cattle between paddocks so grazed ones recover. Rewards planning and reading regrowth.
- **Hay:** cut and bale surplus summer grass into winter stores. Running short means buying feed at bad prices mid-winter.

### 4.3 Economy
- **Revenue:** selling cattle (weaned calves, culls, finished animals) at the sale barn at fluctuating prices; optional later: direct beef sales, breeding-stock premiums.
- **Costs:** feed, fuel, vet, equipment upkeep, hired labor, land payments.
- **Market model:** a base price that drifts with seasonal cycles + random shocks (drought spikes feed cost and floods the market with sold-off cattle, crashing prices). Optional depth: forward contracts to lock a price.
- Cash-flow pressure is the point — winters drain the account, so fall sales must carry you.

### 4.4 Weather & Seasons
- The engine that drives all the others. Seasonal baseline + variability: drought, heat waves, blizzards, wet springs.
- Weather modifies grass regrowth, animal stress/weight, calving loss, and market prices. It should *regularly* disrupt plans — that's what makes the sim breathe.
- A forecast system (imperfect, improvable via upgrades) lets players plan without eliminating risk.

### 4.5 Health & Risk
- Per-animal health states: healthy, sick, injured, parasites, pregnant-at-risk.
- **Interventions:** vaccination schedules (preventive), vet visits (reactive, costly), culling.
- Risk events: disease outbreaks (contagion spreads through a paddock), predators, birthing complications, injury.
- Neglect compounds — a skipped vaccination round raises outbreak odds next season.

### 4.6 The Central Tension
The whole design converges on one triangle:

> **Stocking rate ↔ grass health ↔ market timing**

Overstock and you starve the herd and wreck the soil. Understock and you leave money on the table. And the market punishes whichever mistake you make, whenever you're forced to sell. Nearly every session decision is really a move on this triangle. **This is the game.** Everything else is texture.

---

## 5. Scope & Feature Tiers

Ruthless prioritization for a solo dev. Build MVP first; treat everything below it as optional.

**MVP (must exist for the game to be a game)**
- Herd of individual animals with basic stats and aging
- Simple breeding with heritable traits
- Paddocks with grass biomass + stocking-rate consequence
- Season clock (4 seasons) with basic weather effects
- Buy/sell at a fluctuating market
- Win/lose pressure: go broke = game over; survive = keep growing
- Save/load

**Tier 2 (makes it good)**
- Rotational grazing + hay cutting/storage
- Health system with vaccinations, vet, disease events
- Body-condition and weight-gain simulation
- Named/favorite animals, herd screen with sorting
- Weather forecast + more event variety

**Tier 3 (makes it special — post-launch / stretch)**
- Forward contracts / deeper market
- Genetics depth (trait linkage, inbreeding penalty, breed introduction)
- Ranch upgrades (better fences, water systems, equipment)
- Feedlot / vertical integration; direct-to-consumer beef
- Neighbors, rivals, county fair, reputation
- Roundup/branding as interactive set-pieces

---

## 6. Technical Architecture (Unity)

**Engine & setup**
- Unity (LTS release), 2D/URP with an **isometric tilemap** for the 2.5D look.
- Target: PC first (keyboard/mouse), design UI to be mobile-portable later.
- Version control from day one: Git + Git LFS for art assets.

**High-level structure**
- **Time is turn/tick-based under the hood**, even if presented smoothly. A "day" is the atomic tick; systems update on day/season boundaries. This keeps a sim deterministic and debuggable — avoid tying simulation to frame rate.
- **Data-driven design.** Breeds, traits, events, and market curves live in `ScriptableObject` assets, not hardcoded. Lets you tune balance without touching code.
- **Separate simulation from presentation.** A `RanchState` model holds all game data (herd, paddocks, cash, date). The visuals *read* from it. This makes save/load trivial (serialize the model) and lets you unit-test the sim with no scene loaded.

**Suggested core classes/data**
- `Animal` — id, genetics (a `Genome` struct of trait floats), age, weight, health, pregnancy.
- `Paddock` — grass biomass, soil health, water, occupants.
- `GameClock` — current date, advances ticks, fires season events.
- `Market` — price model, generates offers.
- `WeatherSystem` — rolls seasonal weather, exposes modifiers.
- `EventSystem` — queues and resolves random events (disease, predator, birth).
- `RanchState` — the serializable root that owns all of the above.

**Save system**
- Serialize `RanchState` to JSON (Unity's `JsonUtility` or Newtonsoft). Because sim is separate from view, this is close to free.

**Key learning risks to tackle early (solo dev)**
- Isometric tilemaps and sorting order (which sprite draws in front) — prototype this in week one; it's a classic time sink.
- ScriptableObject workflow — learn it before you hardcode content you'll regret.
- Turn/tick loop vs. Unity's frame `Update()` — decide the pattern before building systems on top of it.

---

## 7. Build Roadmap (Phased Milestones)

Sized for a solo hobbyist learning as you go. Estimates are calendar-loose, assuming part-time. The rule: **each phase ends with something playable.**

### Phase 0 — Foundations *(1–2 weeks)*
Learn the tools; don't build the game yet.
- Unity project set up, Git + LFS configured.
- Isometric tilemap rendering a small ranch with correct sprite sorting.
- A cube/placeholder "cow" you can click and select.
- A working `GameClock` that advances days and prints the date.

*Exit test: you can click a placeholder animal and advance time.*

### Phase 1 — The Core Loop Prototype *(3–5 weeks) ⭐ prove the fun*
The most important phase. Ugly is fine; **fun is not optional.**
- `Animal` objects with age/weight; a small herd.
- One paddock with grass biomass that depletes with grazing and regrows over time.
- Season clock with a simple weather modifier on grass.
- A market you can sell into at a fluctuating price.
- Bare-bones breeding: two animals → a calf with blended stats.
- Go-broke = game over.

*Exit test: can you play 5 in-game years and feel a real "should I sell now?" decision? If not, fix the loop before adding anything.*

### Phase 2 — Depth Systems *(4–6 weeks)*
Turn the prototype into a real sim.
- Multiple paddocks + rotational grazing.
- Hay cutting/storage and winter feeding.
- Heritable-trait genetics with variance and hybrid vigor.
- Health system: conditions, vaccinations, vet, one disease event type.
- Body-condition/weight-gain simulation tied to grass quality.

*Exit test: the stocking/grass/market triangle produces genuine dilemmas.*

### Phase 3 — Content & Feel *(4–6 weeks)*
Make it a game people enjoy looking at and living in.
- Real isometric art pass: cattle, terrain, buildings, seasons visibly changing.
- Herd management UI: sortable list, individual animal cards, naming.
- Event variety, weather forecast, sound and music.
- Tutorial / onboarding for the first year.

*Exit test: a friend can sit down cold and understand what to do.*

### Phase 4 — Balance & Polish *(3–5 weeks, ongoing)*
- Tune market curves, grass regrowth, event frequency via ScriptableObjects.
- Playtest, fix the difficulty cliff, smooth the economy.
- Save/load hardening, edge cases, performance.
- Settings, quality-of-life (speed controls, notifications).

### Phase 5 — Release / Stretch *(open-ended)*
- Pick from Tier 3 features based on what playtesters crave.
- If commercial later: Steam page, demo build, wishlists.

**Total to a solid, shippable core: roughly 4–6 months part-time** — realistic for a solo learner if scope stays disciplined. The single biggest risk is scope creep in Phase 2; resist adding Tier 3 ideas until the core is proven fun.

---

## 8. Immediate Next Steps

1. Stand up the Unity project, Git, and an isometric test scene (Phase 0).
2. Build the `GameClock` and a clickable placeholder animal.
3. Get *one* paddock's grass-depletion-and-regrowth curve working — it's the heartbeat of the whole sim.
4. Only then start Phase 1's sell-decision loop.

The discipline that will make or break this project: **prove the core loop is fun in ugly prototype form (end of Phase 1) before you invest a single hour in art or Tier 3 features.**

---

*Living document — revise as prototyping teaches you what's actually fun.*
