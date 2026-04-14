package com.pso.timefold.domain;

import ai.timefold.solver.core.api.domain.solution.PlanningEntityCollectionProperty;
import ai.timefold.solver.core.api.domain.solution.PlanningScore;
import ai.timefold.solver.core.api.domain.solution.PlanningSolution;
import ai.timefold.solver.core.api.domain.solution.ProblemFactCollectionProperty;
import ai.timefold.solver.core.api.domain.solution.ProblemFactProperty;
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
}
