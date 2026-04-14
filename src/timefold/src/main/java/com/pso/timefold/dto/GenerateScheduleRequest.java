package com.pso.timefold.dto;

import com.pso.timefold.domain.*;
import com.pso.timefold.domain.enums.DifficultTaskSchedulingStrategy;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.List;

/**
 * JSON deserialization target for POST /jobs/run.
 * Mirrors web/Providers/Models/GenerateScheduleRequest (camelCase fields).
 */
@Getter
@Setter
@NoArgsConstructor
public class GenerateScheduleRequest {

    private List<FixedTask> fixedTasks;
    private List<DynamicTask> dynamicTasks;
    private PlanningHorizon planningHorizon;
    private List<CategoryWindow> categoryWindows;
    private DifficultTaskSchedulingStrategy difficultTaskSchedulingStrategy;
    private List<DifficultyCapacityEntry> difficultyCapacities;
    private List<TaskTypePreferenceEntry> taskTypePreferences;
    private int optimizationTimeInSeconds = 15;
}
