package com.pso.timefold.dto;

import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDateTime;
import java.util.UUID;

/**
 * Output DTO representing one scheduled task in the result timeline.
 * Used in the callback payload and GET /jobs/current-solution response.
 *
 * Matches the web callback contract: { "JobId": "...", "TasksTimeline": [...] }
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
public class ScheduledTask {

    private UUID id;
    private String name;
    private LocalDateTime startTime;
    private LocalDateTime endTime;
    private int priority;
    private int difficulty;
    private boolean fixed;
}
