package com.pso.timefold.domain;

import com.pso.timefold.domain.enums.TaskType;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
public class TaskTypeWeight {

    private TaskType type;
    private int weight;
}
