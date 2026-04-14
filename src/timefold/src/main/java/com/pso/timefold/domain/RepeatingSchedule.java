package com.pso.timefold.domain;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
public class RepeatingSchedule {

    private int minDayCount;

    @JsonSetter(nulls = Nulls.SKIP)
    private int optDayCount = 1;

    private int minWeekCount;

    @JsonSetter(nulls = Nulls.SKIP)
    private int optWeekCount = 1;
}
