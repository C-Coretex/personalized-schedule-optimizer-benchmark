# personalized-schedule-optimizer-benchmark

Comparative benchmark of universal optimization tools (OR-Tools, Timefold) versus a custom specialized algorithm for personalized schedule planning - a combinatorial optimization problem.

## Problem

Personalized schedule planning is the task of automatically arranging a set of user-defined tasks into a calendar while satisfying a combination of hard constraints and soft objectives. Unlike classical scheduling problems in manufacturing or workforce management, personalized planning must account for highly variable, user-driven input: tasks differ in duration, priority, deadline, allowed time windows, and flexibility. The planner must respect what is fixed and optimize what is not.

## Approach

The project compares two fundamentally different architectural strategies for solving the problem:

**Universal optimization tools** are general-purpose solvers — Google OR-Tools and Timefold — that provide powerful search and constraint propagation engines. They require the developer to manually encode all domain knowledge into the solver's formalism: decision variables, constraints, and objective functions must be expressed explicitly. Their strength lies in theoretical guarantees and mature infrastructure; their cost is the translation burden between domain and model.

**A custom specialized algorithm** embeds domain knowledge directly into the scheduling logic. It uses a two-phase approach: a domain-adapted dispatching rule (based on weighted shortest processing time and apparent tardiness cost) constructs an initial feasible solution, which is then refined using simulated annealing with pairwise interchange. Because the algorithm is designed specifically for this problem, it can exploit structural properties that a general solver cannot assume.

## Projects

| Project | Description |
|---|---|
| `web` | React UI + BFF (Backend for Frontend) that aggregates all optimizer services and exposes a unified API |
| `specialized` | Custom two-phase scheduling algorithm: domain-adapted dispatching for initialization, refined with simulated annealing |
| `ortools` | Solution implemented with [Google OR-Tools](https://developers.google.com/optimization) (CP-SAT solver) |
| `timefold` | Solution implemented with [Timefold Solver](https://timefold.ai) |
