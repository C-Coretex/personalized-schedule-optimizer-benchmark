package com.pso.timefold.domain;

import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDate;
import java.util.List;

@Getter
@Setter
@NoArgsConstructor
public class TaskTypePreferenceEntry {

    private LocalDate date;
    private List<TaskTypeWeight> preferences;
}
