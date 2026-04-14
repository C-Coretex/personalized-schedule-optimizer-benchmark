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
}
