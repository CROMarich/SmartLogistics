# Smart Logistics Planner

Web application that plans the transport of construction machines between locations: it assigns
transport orders to two trucks, orders the pickups and deliveries of every truck and minimises the
total distance, with a mandatory return to the depot. The planner is **exact** and every plan is
checked by an **independent validator** before it is shown.

| Workday interpretation | Total | Truck A | Truck B | Orders planned | Validator |
|------------------------|------:|--------:|--------:|---------------:|-----------|
| `Total` – driving + actions ≤ 9 h (default) | **200.1 km** | 37.2 km | 162.9 km | 10 / 10 | 0 violations |
| `DrivingOnly` – driving ≤ 9 h | **182.2 km** | 0 km (unused) | 182.2 km | 10 / 10 | 0 violations |

Full routes (location, action, order, load after every action, km, minutes): [docs/rezultati.md](docs/rezultati.md).

The user interface and the project documents in `docs/` are in Croatian; code, comments and this
README are in English.

---

## Quick start

**Prerequisites:** .NET 10 SDK (SDK 11 also builds the solution). No database, no external services.

```bash
dotnet build              # whole solution
dotnet test               # 89 tests
dotnet run --project src/SmartLogistics.Web
```

Open the URL printed by `dotnet run` (launch profile: `http://localhost:5186`). In Visual Studio or
VS Code open `SmartLogistics.slnx` and start `SmartLogistics.Web`.

Input data lives in [`data/scenario.json`](data/scenario.json). The app reads it on every request,
so an edited file is planned again on the next page load; a malformed file is shown as an error
message instead of crashing the app.

## What the application shows

| Page (menu) | Content |
|-------------|---------|
| **Pregled** (overview) | Project progress, key figures, documents, pending decisions |
| **Dokumentacija** | Task, specification, decision log, AI log, results, time spent, design system – rendered from `docs/*.md` |
| **Pravila i provjere** (rules) | Rules R1–R39 from the specification, which of them the validator checks, and a live validator run |
| **Ulazni podaci** (input data) | Trucks, machines (which truck they fit on), locations, orders, 10×10 distance matrix, map of locations and orders |
| **Plan ruta** (route plan) | Switch between `Total` and `DrivingOnly`; per truck: km, driving/action/total time, workday usage, and the full route with the load after every action; infeasible orders with a reason; validator result; computation time and search statistics |
| **Karta** (map) | SVG map of both routes with direction arrows |

## Domain rules and assumptions

The task was turned into 39 numbered, testable rules in [docs/spec.md](docs/spec.md). The most
important ones:

- Truck A: 3500 kg / 2 machines, truck B: 10000 kg / 3 machines – the limits hold **after every action**.
- The same truck picks up and delivers an order; pickup before delivery.
- Every route starts and ends at the depot L0; a truck may stay unused.
- A leg is the Euclidean distance **rounded to one decimal** (`MidpointRounding.AwayFromZero`) before summing.
- 60 km/h (1 km = 1 min); every pickup or delivery takes 20 min, also at the depot.
- **"The driver may drive 9 h a day" is ambiguous.** Default `WorkdayMode.Total` counts driving + actions
  (≤ 540 min); `WorkdayMode.DrivingOnly` counts driving only. The app shows both.
- A feasible order may not be dropped to save kilometres: the objective is *most planned orders, then
  fewest km*. An order that cannot be planned is reported with a category (K1 heavier than every truck,
  K2 does not fit into the workday even alone, K3 does not fit together with the other orders, K4 invalid data).

Every assumption, deviation and proposal is recorded with its reason and status in
[docs/odluke.md](docs/odluke.md); the list of decisions still waiting for approval is at the top of that file.

## How the planner works

`Core/Planning/ExactDpPlanner.cs` (the XML comment of the class contains the full description and proofs).

1. **Assignment.** An order may only go to a truck that can carry its machine. Orders N06–N10 (3900–9400 kg)
   fit only truck B; for the k orders both trucks can carry (N01–N05) **all 2^k = 32 assignments** are tried.
2. **Shortest route per truck – dynamic programming.** State = (current location, status of every order:
   0 not picked up, 1 on the truck, 2 delivered), with the status encoded as a base-3 number. Value =
   minimum remaining km to finish all orders and return to L0, memoised in a dictionary. Transitions: pick up
   an order with status 0 if weight and machine count still fit, or deliver an order with status 1. The load is
   derived from the status, not stored. The route is reconstructed from the memo.
3. **Working day.** For a fixed set of orders the number of actions is fixed (2 per order) and driving time
   grows with km, so the shortest route is also the fastest. The workday limit is therefore checked after the
   shortest route is found; an assignment that breaks it on any truck is infeasible.
4. The feasible assignment with the smallest total km wins. If no assignment of all orders is feasible, the
   search is extended with an "unassigned" option and the planner maximises the number of planned orders first.
5. **Self-check.** Before returning, the plan goes through `PlanValidator`; any violation throws (it would be a bug).

Distances are kept in **whole tenths of a km** (`int`) inside the planner, so sums and tie-breaking are exact
and the result is deterministic. For the task data the search tries 32 assignments, solves 63 distinct DPs
(284,366 states) and takes about **40–90 ms**.

The planner is exact, not a heuristic: it enumerates every assignment and solves every truck's routing
optimally, and the two trucks are independent once the assignment is fixed.

## How the plan is validated

`Core/Validation/PlanValidator.cs` was written **only from the specification**, without access to the
planner code (a rule in the agent's instructions, see T5). It replays every route from the depot step by step and **recomputes** km, minutes, load and
machine count instead of trusting the numbers in the plan. It returns a list of **all** violations (rule ID,
truck, step, message); it never stops at the first one. It checks capacity and machine count after every
action, pickup before delivery, same truck, correct locations, return to depot, empty truck at the end,
reported km and minutes, the workday limit for the selected mode, and that every order is planned exactly
once or listed as infeasible with a valid reason. 23 of the 39 rules are checked at plan level; the others
are properties of the input, the options or the UI.

## Tests

`dotnet test` – **89 tests, all green.**

| Test class | Tests | Purpose |
|------------|------:|---------|
| `DistanceTests` | 16 | Reference legs (L0→L1 = 8.5, L3→L8 = 8.5, L5→L9 = 34.9), symmetry, zero diagonal, midpoint rounding |
| `ScenarioDataTests` | 7 | `scenario.json` matches the task; default options |
| `PlanBuilderTests` | 4 | The test helper that builds plans by hand computes the same numbers as a manual calculation |
| `PlanValidatorValidPlanTests` | 15 | Valid plans and boundary cases pass (exactly 10000 kg, exactly 540 min, actions at the depot, unused truck) |
| `PlanValidatorViolationTests` | 38 | Hand-built invalid plans are rejected with the right rule: overload (R11), too many machines (R12), delivery before pickup (R6), picked up by A / delivered by B (R5), no return to depot (R2), machine left on the truck (R28), wrong km (R16), workday exceeded (R20/R21), and more |
| `ReferenceSolutionTests` | 9 | Planner returns 200.1 km (`Total`) and 182.2 km (`DrivingOnly`), its plans pass the validator, one route per truck, deterministic, `DrivingOnly` ≤ `Total` |

Floating-point comparisons use a tolerance. Tests are tagged with the rule they cover (`[Trait("Rule", "R11")]`).

## Project structure

```
SmartLogistics.slnx
data/scenario.json                     input data from the task
docs/                                  task, specification, decisions, results, AI log, time, design system
src/SmartLogistics.Core/               domain logic, no dependency on Web
  Domain/                              records: Location, Truck, Machine, TransportOrder, Scenario,
                                       Stop, TruckRoute, Plan, PlanningOptions, WorkdayMode
  Distance.cs                          rounded leg + distance matrix
  ScenarioLoader.cs                    strict JSON loading
  Planning/IRoutePlanner.cs            planner contract
  Planning/ExactDpPlanner.cs, ...      exact planner
  Validation/PlanValidator.cs, ...     independent validator
src/SmartLogistics.Web/                ASP.NET Core Razor Pages
  Pages/                               pages + partial views in Pages/Shared
  ViewModels/                          everything a page shows, already formatted (km, kg, hh:mm)
  Services/                            scenario loading, plan cache, docs reader, Markdown renderer
  wwwroot/css/site.css                 the whole design system (CSS variables, no inline styles)
tests/SmartLogistics.Tests/            xUnit tests
.claude/agents/                        sub-agent definitions used during development
CLAUDE.md                              project rules for the AI assistant
```

**Design choices:** C# records for the domain; Core has no dependency on Web; pages never render domain
objects directly, only view models; no JavaScript framework (only the Bootstrap bundle for the mobile menu);
no external APIs or CDNs; the SVG maps are generated on the server.

## Use of AI tools

The project was developed with Claude Code, in phases, with a human technical lead deciding assumptions
and architecture and reviewing every phase. Six sub-agents with narrow scopes were used: domain analyst
(specification), validator engineer (not allowed to read the planner), test engineer, solver engineer,
UI builder and reviewer. Validator and tests were written in parallel against a shared contract.

What went wrong with the AI, how it was caught and fixed, and a reflection on the tools:
[docs/ai-log.md](docs/ai-log.md).

## Time spent

About **2 h 08 min** of joint work in the development session, measured from git commits and file
timestamps; the per-phase breakdown is in [docs/vrijeme.md](docs/vrijeme.md).

## Documents

| Document | Content |
|----------|---------|
| [docs/zadatak.md](docs/zadatak.md) | Original task (Croatian) |
| [docs/spec.md](docs/spec.md) | Rules R1–R39, ambiguities D1–D16, derived facts, edge cases |
| [docs/odluke.md](docs/odluke.md) | Decision log T1–T30 with status; pending decisions at the top |
| [docs/rezultati.md](docs/rezultati.md) | Full results for both workday modes |
| [docs/ai-log.md](docs/ai-log.md) | AI mistakes and reflection on AI tools |
| [docs/vrijeme.md](docs/vrijeme.md) | Time spent per phase |
| [docs/ui-dizajn.md](docs/ui-dizajn.md) | Design system of the web UI |
