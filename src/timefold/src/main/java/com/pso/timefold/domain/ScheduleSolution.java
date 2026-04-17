package com.pso.timefold.domain;

import ai.timefold.solver.core.api.domain.solution.PlanningEntityCollectionProperty;
import ai.timefold.solver.core.api.domain.solution.PlanningScore;
import ai.timefold.solver.core.api.domain.solution.PlanningSolution;
import ai.timefold.solver.core.api.domain.solution.ProblemFactCollectionProperty;
import ai.timefold.solver.core.api.domain.solution.ProblemFactProperty;
import ai.timefold.solver.core.api.domain.valuerange.CountableValueRange;
import ai.timefold.solver.core.api.domain.valuerange.ValueRangeFactory;
import ai.timefold.solver.core.api.domain.valuerange.ValueRangeProvider;
import ai.timefold.solver.core.api.score.buildin.hardsoft.HardSoftScore;
import com.pso.timefold.domain.enums.DifficultTaskSchedulingStrategy;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.List;

/**
 * Root planning solution.
 * Contains all task assignments (planning entities) and all problem facts
 * derived from the GenerateScheduleRequest.
 */
@PlanningSolution
@Getter
@Setter
@NoArgsConstructor
public class ScheduleSolution {

    @PlanningEntityCollectionProperty
    private List<TaskAssignment> taskAssignments;

    @ProblemFactCollectionProperty
    private List<FixedTask> fixedTasks;

    @ProblemFactCollectionProperty
    private List<DynamicTask> dynamicTasks;

    @ProblemFactProperty
    private PlanningHorizon planningHorizon;

    @ProblemFactCollectionProperty
    private List<CategoryWindow> categoryWindows;

    /**
     * Single-element list wrapping the DifficultTaskSchedulingStrategy.
     * Stored as a collection so it can be accessed via forEach() in constraint streams.
     * The list always contains exactly one element (or is empty if no strategy is set).
     */
    @ProblemFactCollectionProperty
    private List<DifficultTaskSchedulingStrategy> strategies;

    @ProblemFactCollectionProperty
    private List<DifficultyCapacityEntry> difficultyCapacities;

    @ProblemFactCollectionProperty
    private List<TaskTypePreferenceEntry> taskTypePreferences;

    /**
     * One WeekRequirement per (repeating DynamicTask, week in horizon).
     * Used by HC6 and SC5 constraints.
     */
    @ProblemFactCollectionProperty
    private List<WeekRequirement> weekRequirements;

    /**
     * One DayRequirement per (repeating DynamicTask, day in horizon).
     * Used by HC7 and SC6 constraints.
     */
    @ProblemFactCollectionProperty
    private List<DayRequirement> dayRequirements;

    /**
     * One DayFact per calendar day in the planning horizon.
     * Used by SC7 constraint (difficulty imbalance).
     */
    @ProblemFactCollectionProperty
    private List<DayFact> dayFacts;

    /**
     * Singleton fact holding horizon-level constants (numDays, totalFixedDifficulty).
     * Used by SC7c (variance correction term T²/n).
     */
    @ProblemFactProperty
    private PlanningHorizonFact planningHorizonFact;

    @PlanningScore
    private HardSoftScore score;

    /**
     * Shared value range for all TaskAssignment.startMinute planning variables.
     * Covers the full planning horizon at 1-minute granularity.
     * This is a lazy CountableValueRange — no array is materialized; Timefold
     * computes values by index on demand. Memory use is O(1).
     * Example: 7-day horizon → range [0, 10080) → 10 080 addressable slots.
     */
    @ValueRangeProvider
    public CountableValueRange<Integer> getStartMinuteRange() {
        int horizonMinutes = computeHorizonMinutes();
        return ValueRangeFactory.createIntValueRange(0, horizonMinutes, 5);
    }

    private int computeHorizonMinutes() {
        if (planningHorizon == null) return 7 * 24 * 60;
        return (int) planningHorizon.getDays() * 24 * 60;
    }
}
