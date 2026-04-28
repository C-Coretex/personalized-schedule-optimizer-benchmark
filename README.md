# personalized-schedule-optimizer-benchmark

Comparative benchmark of universal optimization tools (OR-Tools, Timefold) versus a custom specialized algorithm for personalized schedule planning — a combinatorial optimization problem.

---

### Demo: [personalized-schedule-optimizer-benchmark](https://web-production-e08c4.up.railway.app/)

| # | Step | Preview |
|---|------|---------|
| 1 | Click **Load Sample - Week Heavy** | <img width="600" src="https://github.com/user-attachments/assets/cf09b20f-2b36-4d12-b581-602fe87add08"/> |
| 2 | Click **Generate Schedule** and wait ~5 seconds | <img width="600" src="https://github.com/user-attachments/assets/541a5ed7-49b1-4b37-87fa-ed23c046f47d"/> |
| 3 | Click an item in **Generated Schedules** when it stops spinning | <img width="600" src="https://github.com/user-attachments/assets/e79ee0d5-2fb0-4aa0-8e2f-156c0559e6b2"/> |
| 4 | The calendar appears at the top | <img width="600" src="https://github.com/user-attachments/assets/4a9f41ac-156c-40de-b32a-53045954d2a4"/> |


---

## Table of Contents

- [The Problem](#the-problem)
  - [Input Model](#input-model)
  - [Hard Constraints](#hard-constraints)
  - [Soft Objectives](#soft-objectives)
- [Solvers](#solvers)
  - [OR-Tools (Google CP-SAT)](#or-tools-google-cp-sat)
  - [Timefold Solver](#timefold-solver)
- [Specialized Algorithm](#specialized-algorithm)
  - [Phase 1 — Construction](#phase-1--construction)
  - [Phase 2 — Simulated Annealing](#phase-2--simulated-annealing)
  - [Reactive Constraint Cache](#reactive-constraint-cache)
- [Scoring & Evaluation](#scoring--evaluation)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Running the Project](#running-the-project)

---

## The Problem

Personalized schedule planning is the task of automatically arranging a set of user-defined tasks into a calendar while satisfying hard constraints and optimizing soft objectives. Unlike classical scheduling problems (manufacturing, workforce), personalized planning must handle highly variable, user-driven input: tasks differ in duration, priority, deadline, allowed time windows, category restrictions, and repetition requirements. The planner must respect what is fixed and optimize what is not.

### Input Model

| Field | Description |
|---|---|
| `FixedTasks` | Pre-placed events with locked start/end times |
| `DynamicTasks` | Tasks to be scheduled by the solver |
| `PlanningHorizon` | `(StartDate, EndDate)` — the scheduling window |
| `CategoryWindows` | Allowed date-time ranges per category (e.g. Work 9–17, Morning 7–9) |
| `DifficultyCapacities` | Max difficulty budget per day |
| `TaskTypePreferences` | Per-day weights for task types |
| `DifficultTaskSchedulingStrategy` | `Cluster` or `Even` — whether to group or spread hard tasks |
| `OptimizationTimeInSeconds` | Solver time budget (default 15 s) |

Each **dynamic task** carries: `Priority` (1–5), `Difficulty` (1–10), `Duration` (minutes), an optional daily `WindowStart`/`WindowEnd`, an optional `Deadline`, a list of `Categories`, a list of `TaskTypes`, and an optional `RepeatingSchedule` (`MinDayCount`, `OptDayCount`, `MinWeekCount`, `OptWeekCount`). A non-repeating task is scheduled at most once; a repeating task may have many occurrences distributed across the horizon.

### Hard Constraints

Violations must be zero for a solution to be considered feasible.

| ID | Constraint |
|---|---|
| HC1 | No two tasks overlap in time |
| HC2 | All required non-repeating tasks must be scheduled |
| HC3 | Tasks must fall within their daily time window (WindowStart/WindowEnd) |
| HC4 | Tasks must complete before their deadline |
| HC5 | Tasks must be placed within at least one of their category windows |
| HC6 | Repeating tasks must meet MinWeekCount / OptWeekCount per week |
| HC7 | Repeating tasks must meet MinDayCount / OptDayCount per day |
| HC8 | All tasks must be within the planning horizon |
| HC9 | Non-repeating tasks appear at most once |

**Aggregate hard score** = HC1 + HC2 + ⌈HC3/60⌉ + ⌈HC4/60⌉ + HC5 + HC6 + HC7 + ⌈HC8/60⌉ + HC9

### Soft Objectives

Optimized once hard constraints are satisfied.

| ID | Objective | Weight |
|---|---|---|
| SC1 | Maximize total priority of scheduled tasks | ×100 |
| SC2 | Minimize difficulty above daily capacity | ×500 |
| SC3 | Follow difficult-task strategy (Cluster: minimize gaps; Even: maximize spread) | ×1 |
| SC4 | Maximize task type preferences per day | ×1 |
| SC5 | Minimize under-scheduling vs. optimal weekly repetition count | ×50 |
| SC6 | Minimize under-scheduling vs. optimal daily repetition count | ×50 |
| SC7 | Minimize difficulty imbalance between days (sum of squared deviations) | ×1 |

**Aggregate soft score** = 100·SC1 + 500·SC2 + SC3 + SC4 + 50·SC5 + 50·SC6 + SC7

---

## Solvers

### OR-Tools (Google CP-SAT)

OR-Tools models the problem as a **Constraint Programming Satisfaction (CP-SAT)** problem. Every dynamic task becomes a set of integer decision variables; the solver searches the variable domain using constraint propagation and branch-and-bound.

**Variables per task:**

| Variable | Domain | Meaning |
|---|---|---|
| `start` | `[0, horizonMax - duration]` | Start minute (offset from horizon start) |
| `end` | `[duration, horizonMax]` | End minute |
| `dayIndex` | `[0, numDays)` | Which day the task lands on |
| `timeFromDayStart` | `[0, 1439]` | Minute within the day |
| `presence` | `{0, 1}` | Whether the task is scheduled (optional tasks only) |

Fixed tasks are pinned as constant-domain intervals. Repeating tasks are expanded into separate variable sets — one per maximum occurrence slot.

**Constraint encoding:**

- HC1 — `model.AddNoOverlap(allIntervals)`
- HC3, HC5 — linear inequalities gated with `OnlyEnforceIf(presence)`, auxiliary `fit` booleans per category window
- HC7 — per-day sum constraint on repeating-task occurrence variables
- SC2 — `overCapacity = max(0, dailyDifficulty − capacity)`, penalized ×500
- SC3 — auxiliary gap-sum variables for difficult tasks per day; penalized or rewarded based on strategy
- SC7 — manual squaring via `AddMultiplicationEquality` to compute Σdᵢ²
- SC1, SC4, SC5, SC6 — linear objective terms weighted by presence booleans

The entire objective is a **single linear combination** handed to the solver. Parallelism is controlled via `num_search_workers`.

---

### Timefold Solver

Timefold uses **Constraint Streams** — a declarative, composable rule engine — combined with its own local search to explore the solution space.

**Planning model:**

- `TaskAssignment` is the planning entity; its `startMinute` variable (5-minute step) is what the solver changes.
- Repeating tasks are pre-expanded into multiple `TaskAssignment` instances in `ScheduleProblemBuilder`.
- `WeekRequirement` and `DayRequirement` problem facts are precomputed and joined in constraint streams to enforce counts without ad-hoc aggregation.

**Constraint patterns used:**

- `forEachUniquePair` + overlap filter → HC1 (no overlaps)
- `ifNotExists` → HC2 (required tasks unscheduled), HC5 (no matching category window), HC6a/HC7a (zero occurrences)
- `join` + `groupBy` + `sum`/`countBi` collectors → HC6b/c, HC7b/c, SC2, SC5, SC6, SC7
- `forEach.join(FixedTask)` → HC1b (dynamic–fixed overlaps)
- `forEach(WeekRequirement).ifNotExists(TaskAssignment)` → zero-occurrence weekly penalty

Scoring uses `HardSoftScore`: hard violations dominate and must reach zero before soft is optimized. Job lifecycle is managed by Spring's `SolverManager` with a per-request `SolverConfigOverride` for the time limit.

---

## Specialized Algorithm

The specialized solver is a **two-phase domain-adapted algorithm**: a greedy construction heuristic builds an initial feasible solution, which is then refined by simulated annealing.

### Phase 1 — Construction

The construction heuristic (`ConstructionHeuristics.cs`) dispatches tasks greedily in priority order:

1. **Daily repeating tasks** (`MinDayCount > 0`): ordered by priority descending, then free-window size ascending. For each shuffled day, fill `MinDayCount` slots per task using `AddScheduledTaskInTimeWindow`.
2. **Weekly repeating tasks** (`MinWeekCount > 0`): group planning days by ISO week, then distribute across weeks respecting `OptDayCount` per day within each week.
3. **Non-repeating required tasks**: same greedy dispatch — iterate shuffled candidate days and place at the first feasible slot.

`AvailableTasksPool` is a `Dictionary<Task, int>` tracking how many instances of each task can still be added. It is decremented on placement and the task is removed when exhausted.

### Phase 2 — Simulated Annealing

`SAEngine.cs` runs for the full `OptimizationTimeInSeconds` budget with a dual-temperature schedule:

**Temperature phases:**

| Phase | Budget share | Hard temp | Soft temp | Purpose |
|---|---|---|---|---|
| Hard phase | 30% | 2 → 0 (linear) | 100% → 50% | Escape infeasibility |
| Soft phase | 70% | 0 (fixed) | 100% → 0 (quadratic) | Fine-tune in feasible space |

**Acceptance criterion (Metropolis):**
- New solution better → always accept (record as best if also best seen)
- New solution worse on hard constraints → accept with probability `exp(−ΔHard / T_hard)`
- New solution worse only on soft → accept with probability `exp(−ΔSoft / T_soft)`

**Move types** (probabilities are adaptive and shift as time decreases):

| Move | Probability | Description |
|---|---|---|
| **RuinRecreate** | 5% → 1% | Destroy a task set (Operational / Tactical / SemiStrategic / Strategic scope), then rebuild with 100×N mini-LAHC iterations |
| **Add** | 10% | Pick an unscheduled task from pool, insert into a free slot; if no space, displace a lower-priority task |
| **Remove** | 10% | Remove a non-required task, return it to the pool |
| **Swap** | 35% | Relocate a task to another time on the same day (Tactical, 75%) or any day (Strategic, 25%); displaced tasks go to pool |
| **CascadeMove** | 40–44% | Move a task and cascade displaced tasks up to N=1–5 times in a chain; chain length sampled from (35%, 30%, 20%, 10%, 5%) |

RuinRecreate scopes control destruction breadth:
- **Operational** (40%): one category, one day
- **Tactical** (30%): all categories, one day
- **SemiStrategic** (20%): one category, multiple random days
- **Strategic** (10%): multiple categories, multiple random days

### Reactive Constraint Cache

`PlanningDomain` maintains all HC/SC values incrementally — adding or removing a task updates only the affected constraints, not the full solution. This makes each move evaluation O(affected days) rather than O(all tasks).

`GetSnapshot()` creates a copy-on-write clone of the domain for branch exploration: moves are tried on the snapshot and the original is restored on rejection.

---

## Scoring & Evaluation

All three solvers are evaluated by the **same scoring logic** in the web BFF (`web/Features/Schedule/Endpoints/GetGenerated/Handler.cs`), independent of how each solver works internally. This ensures a fair apples-to-apples comparison: the BFF computes HC1–HC9 and SC1–SC7 from the raw output and returns both aggregate scores alongside per-constraint breakdowns.

---

## Architecture

```
Browser
  └─► pso.web  (ASP.NET Core BFF + Vanilla JS frontend)
        ├─► POST /jobs/run ──► pso.specialized
        ├─► POST /jobs/run ──► pso.ortools
        └─► POST /jobs/run ──► pso.timefold
              │
              └─► POST /schedule/submit ──► pso.web  (internal callback)
```

- `POST /schedule/generate` — web receives the request, selects a solver, fires a background job
- Solver calls back via `POST /schedule/submit` with the result (`X-Internal-Token` auth)
- `GET /schedule/generated` — web reads the cached result, scores it, returns all HC/SC values

**State:** job metadata is stored in ASP.NET session; solver results are cached in `IMemoryCache` (120-minute sliding TTL).

**Network isolation:** solver services (`pso.specialized`, `pso.ortools`, `pso.timefold`) have no host port mappings — only reachable from `pso.web` within the Docker network.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Web / BFF | ASP.NET Core (.NET 10), C# |
| OR-Tools solver | .NET 10, C#, Google OR-Tools 9.15 (CP-SAT) |
| Timefold solver | Java 21, Maven, Spring Boot, Timefold Solver |
| Specialized solver | .NET 10, C# |
| Frontend | Vanilla JS, HTML/CSS  |
| Orchestration | Docker Compose |

---

## Running the Project

All four services are orchestrated via Docker Compose:

```bash
cd src
docker compose up
```

The web UI is served by `pso.web`. Each solver project also includes a **Console app** for local testing without Docker — place an `input.json` in the respective console project directory and run `dotnet run`.
