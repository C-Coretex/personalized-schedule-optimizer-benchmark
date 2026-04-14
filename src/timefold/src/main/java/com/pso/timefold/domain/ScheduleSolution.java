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

    @ProblemFactProperty
    private DifficultTaskSchedulingStrategy difficultTaskSchedulingStrategy;

    @ProblemFactCollectionProperty
    private List<DifficultyCapacityEntry> difficultyCapacities;

    @ProblemFactCollectionProperty
    private List<TaskTypePreferenceEntry> taskTypePreferences;

    @PlanningScore
    private HardSoftScore score;

    /**
     * Value range for startMinute planning variable.
     * Covers the full planning horizon at 15-minute granularity.
     * Example: 7-day horizon → range [0, 10080) step 15 → 672 slots.
     */
    @ValueRangeProvider
    public CountableValueRange<Integer> getStartMinuteRange() {
        int horizonMinutes = computeHorizonMinutes();
        return ValueRangeFactory.createIntValueRange(0, horizonMinutes, 15);
    }

    private int computeHorizonMinutes() {
        if (planningHorizon == null) return 7 * 24 * 60;
        return (int) planningHorizon.getDays() * 24 * 60;
    }
}
