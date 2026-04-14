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

    public int getDurationMinutes() {
        return (int) ChronoUnit.MINUTES.between(startTime, endTime);
    }
}
