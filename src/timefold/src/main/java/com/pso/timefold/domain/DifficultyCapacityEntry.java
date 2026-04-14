package com.pso.timefold.domain;

import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDate;

@Getter
@Setter
@NoArgsConstructor
public class DifficultyCapacityEntry {

    private LocalDate date;
    private int capacity;

    /**
     * Pre-computed sum of difficulties of all FixedTasks scheduled on this day.
     * Set by ScheduleProblemBuilder at build time (not deserialized from JSON).
     * SC2 adds this to the dynamic task difficulty sum before comparing against capacity.
     */
    private int fixedDifficulty;
}
