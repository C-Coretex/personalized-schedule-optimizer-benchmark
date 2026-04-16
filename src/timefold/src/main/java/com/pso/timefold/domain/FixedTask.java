package com.pso.timefold.domain;

import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDateTime;
import java.time.temporal.ChronoUnit;

@Getter
@Setter
@NoArgsConstructor
public class FixedTask extends TaskBase {

    private LocalDateTime startTime;
    private LocalDateTime endTime;

    private int startMinuteFromHorizon = -1;
    private int endMinuteFromHorizon   = -1;

    public int getDurationMinutes() {
        return (int) ChronoUnit.MINUTES.between(startTime, endTime);
    }
}
