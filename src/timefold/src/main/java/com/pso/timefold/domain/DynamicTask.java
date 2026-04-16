package com.pso.timefold.domain;

import com.fasterxml.jackson.annotation.JsonAlias;
import com.fasterxml.jackson.annotation.JsonProperty;
import com.pso.timefold.domain.enums.Category;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.List;

@Getter
@Setter
@NoArgsConstructor
public class DynamicTask extends TaskBase {

    @JsonProperty("isRequired")
    private boolean required;
    @JsonAlias("duration")
    private int durationMinutes;
    private LocalTime windowStart;   // nullable — time-of-day window start
    private LocalTime windowEnd;     // nullable — time-of-day window end
    private LocalDateTime deadline;  // nullable — absolute deadline
    private List<Category> categories;
    private RepeatingSchedule repeating; // nullable — null means schedule once

    private int deadlineMinute = -1;  // horizon-relative minutes; -1 = no deadline

    /**
     * Maximum number of occurrence slots to create for this task.
     * For non-repeating tasks: 1.
     * For repeating tasks: max of per-day and per-week optimal counts.
     */
    public int maxOccurrences() {
        if (repeating == null) return 1;
        return Math.max(repeating.getOptDayCount(), repeating.getOptWeekCount() * 7);
    }
}
