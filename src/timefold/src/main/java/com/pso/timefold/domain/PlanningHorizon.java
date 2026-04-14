package com.pso.timefold.domain;

import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDate;
import java.time.temporal.ChronoUnit;

@Getter
@Setter
@NoArgsConstructor
public class PlanningHorizon {

    private LocalDate startDate;
    private LocalDate endDate;

    /** Number of days in the planning horizon (inclusive). */
    public long getDays() {
        return ChronoUnit.DAYS.between(startDate, endDate) + 1;
    }
}
