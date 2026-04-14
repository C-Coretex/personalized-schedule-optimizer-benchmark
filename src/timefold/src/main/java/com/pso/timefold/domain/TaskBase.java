package com.pso.timefold.domain;

import com.pso.timefold.domain.enums.TaskType;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.List;
import java.util.UUID;

@Getter
@Setter
@NoArgsConstructor
public abstract class TaskBase {

    private UUID id;
    private String name;
    private int priority;    // 1–5, higher = more important
    private int difficulty;  // 1–10, higher = harder
    private List<TaskType> types;
}
