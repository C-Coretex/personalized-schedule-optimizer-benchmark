package com.pso.timefold.domain;

import com.pso.timefold.domain.enums.Category;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDateTime;

@Getter
@Setter
@NoArgsConstructor
public class CategoryWindow {

    private Category category;
    private LocalDateTime startDateTime;
    private LocalDateTime endDateTime;

    private int startMinuteFromHorizon = -1;
    private int endMinuteFromHorizon   = -1;
}
