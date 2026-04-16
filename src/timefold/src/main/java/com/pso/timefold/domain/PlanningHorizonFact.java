package com.pso.timefold.domain;

import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

/**
 * Problem fact: singleton holding horizon-level constants needed by SC7.
 *
 * numDays            — total calendar days in the planning horizon (inclusive).
 * totalFixedDifficulty — sum of difficulties of ALL FixedTasks in the request.
 *
 * These two values are used in the SC7 variance-correction term (T²/n), where
 * T = totalFixedDifficulty + totalDynamicDifficulty and n = numDays.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
public class PlanningHorizonFact {

    /** Total number of days in the planning horizon. */
    private int numDays;

    /** Sum of difficulties of all FixedTasks (constant across the solve). */
    private int totalFixedDifficulty;
}
