package com.pso.timefold.solver;

import ai.timefold.solver.core.api.score.buildin.hardsoft.HardSoftScore;
import ai.timefold.solver.core.api.score.stream.Constraint;
import ai.timefold.solver.core.api.score.stream.ConstraintCollectors;
import ai.timefold.solver.core.api.score.stream.ConstraintFactory;
import ai.timefold.solver.core.api.score.stream.ConstraintProvider;
import ai.timefold.solver.core.api.score.stream.Joiners;
import com.pso.timefold.domain.*;
import com.pso.timefold.domain.enums.DifficultTaskSchedulingStrategy;

public class ScheduleConstraintProvider implements ConstraintProvider {

    @Override
    public Constraint[] defineConstraints(ConstraintFactory factory) {
        return new Constraint[]{
                // Hard constraints
                hc1NoOverlapBetweenAssignments(factory),
                hc1bNoOverlapWithFixedTasks(factory),
                hc2RequiredTasksMustBeScheduled(factory),
                hc3TaskWithinTimeWindow(factory),
                hc4TaskBeforeDeadline(factory),
                hc5TaskWithinCategoryWindows(factory),
                hc6aRepeatingZeroOccurrencesInWeek(factory),
                hc6bRepeatingUnderMinWeekCount(factory),
                hc6cRepeatingOverOptWeekCount(factory),
                hc7aRepeatingZeroOccurrencesInDay(factory),
                hc7bRepeatingUnderMinDayCount(factory),
                hc7cRepeatingOverOptDayCount(factory),
                //hc8TaskWithinPlanningHorizon(factory),   // range enforces this
                //hc9NonRepeatingTaskAtMostOnce(factory), //already enforced
                // Soft constraints
                sc1MaximizeScheduledTaskPriority(factory),
                sc2MinimizeDifficultyAboveCapacity(factory),
                sc3ClusterDifficultTasks(factory),
                sc3EvenDifficultTasks(factory),
                sc4TaskTypePreferences(factory),
                sc5aWeekRepeatingZeroOccurrences(factory),
                sc5bWeekRepeatingUnderOptCount(factory),
                sc6aDayRepeatingZeroOccurrences(factory),
                sc6bDayRepeatingUnderOptCount(factory),
                sc7DaysWithTasks(factory),
                sc7DaysWithoutTasks(factory),
                sc7GlobalCorrection(factory)
        };
    }

    // -------------------------------------------------------------------------
    // Hard Constraints
    // -------------------------------------------------------------------------

    /**
     * HC1: No two dynamic task assignments may overlap in time.
     * Penalty unit: count of overlapping pairs.
     */
    private Constraint hc1NoOverlapBetweenAssignments(ConstraintFactory factory) {
        return factory.forEachUniquePair(TaskAssignment.class,
                        Joiners.filtering((a, b) ->
                                a.getStartMinute() != null && b.getStartMinute() != null &&
                                a.getStartMinute() < b.getEndMinute() &&
                                b.getStartMinute() < a.getEndMinute()))
                .penalize(HardSoftScore.ONE_HARD)
                .asConstraint("HC1: No overlap between assignments");
    }

    /**
     * HC1b: No dynamic task assignment may overlap with a fixed task.
     * Penalty unit: count of overlapping (assignment, fixedTask) pairs.
     */
    private Constraint hc1bNoOverlapWithFixedTasks(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(ta -> ta.getStartMinute() != null)
                .join(FixedTask.class,
                        Joiners.filtering((ta, ft) ->
                                ft.getStartMinuteFromHorizon() >= 0 &&
                                ta.getStartMinute() < ft.getEndMinuteFromHorizon() &&
                                ta.getEndMinute() > ft.getStartMinuteFromHorizon()))
                .penalize(HardSoftScore.ONE_HARD)
                .asConstraint("HC1b: No overlap with fixed tasks");
    }

    /**
     * HC2: All required non-repeating DynamicTasks must be scheduled (startMinute != null).
     * For required repeating tasks, HC6/HC7 enforce the minimum occurrence counts.
     * Penalty unit: count of unscheduled required non-repeating tasks.
     */
    private Constraint hc2RequiredTasksMustBeScheduled(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(a -> a.getTask().isRequired()
                          && a.getTask().getRepeating() == null
                          && a.getStartMinute() == null)
                .penalize(HardSoftScore.ONE_HARD)
                .asConstraint("HC2: Required tasks must be scheduled");
    }

    /**
     * HC3: Tasks must be within their daily time windows (WindowStart / WindowEnd).
     * Penalty unit: total minutes of violation per assignment.
     * If no window is set, the full day [0, 1440) is assumed.
     */
    private Constraint hc3TaskWithinTimeWindow(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(a -> a.getStartMinute() != null)
                .filter(a -> {
                    int winStart = a.getTask().getWindowStart() != null
                            ? a.getTask().getWindowStart().getHour() * 60 + a.getTask().getWindowStart().getMinute() : 0;
                    int winEnd = a.getTask().getWindowEnd() != null
                            ? a.getTask().getWindowEnd().getHour() * 60 + a.getTask().getWindowEnd().getMinute() : 24 * 60;
                    return a.getTimeOfDayStart() < winStart || a.getTimeOfDayEnd() > winEnd;
                })
                .penalize(HardSoftScore.ONE_HARD, a -> {
                    int winStart = a.getTask().getWindowStart() != null
                            ? a.getTask().getWindowStart().getHour() * 60 + a.getTask().getWindowStart().getMinute() : 0;
                    int winEnd = a.getTask().getWindowEnd() != null
                            ? a.getTask().getWindowEnd().getHour() * 60 + a.getTask().getWindowEnd().getMinute() : 24 * 60;
                    int violation = 0;
                    if (a.getTimeOfDayStart() < winStart) violation += winStart - a.getTimeOfDayStart();
                    if (a.getTimeOfDayEnd() > winEnd) violation += a.getTimeOfDayEnd() - winEnd;
                    return (int) Math.ceil(violation / 60.0);
                })
                .asConstraint("HC3: Task within time window");
    }

    /**
     * HC4: Tasks must complete before their deadline.
     * Penalty unit: minutes past deadline.
     */
    private Constraint hc4TaskBeforeDeadline(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(a -> a.getStartMinute() != null
                          && a.getTask().getDeadlineMinute() >= 0
                          && a.getEndMinute() > a.getTask().getDeadlineMinute())
                .penalize(HardSoftScore.ONE_HARD,
                        a -> (int) Math.ceil(
                                (a.getEndMinute() - a.getTask().getDeadlineMinute()) / 60.0))
                .asConstraint("HC4: Task must complete before deadline");
    }

    /**
     * HC5: Tasks with categories must be placed within at least one matching CategoryWindow.
     * A task is valid if its entire duration fits inside a window [cwStart, cwEnd].
     * Penalty unit: count of tasks not covered by any matching window.
     */
    private Constraint hc5TaskWithinCategoryWindows(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(a -> a.getStartMinute() != null
                          && a.getTask().getCategories() != null
                          && !a.getTask().getCategories().isEmpty())
                .ifNotExists(CategoryWindow.class,
                        Joiners.filtering((ta, cw) ->
                                cw.getStartMinuteFromHorizon() >= 0 &&
                                ta.getTask().getCategories().contains(cw.getCategory()) &&
                                ta.getStartMinute() >= cw.getStartMinuteFromHorizon() &&
                                ta.getEndMinute() <= cw.getEndMinuteFromHorizon()))
                .penalize(HardSoftScore.ONE_HARD)
                .asConstraint("HC5: Task within category windows");
    }

    /**
     * HC6a: Repeating task has ZERO scheduled occurrences in a required week.
     * Penalty: minWeekCount (the full minimum is unmet).
     */
    private Constraint hc6aRepeatingZeroOccurrencesInWeek(ConstraintFactory factory) {
        return factory.forEach(WeekRequirement.class)
                .filter(wr -> wr.getMinWeekCount() > 0)
                .ifNotExists(TaskAssignment.class,
                        Joiners.equal(WeekRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((wr, ta) ->
                                ta.getStartMinute() != null &&
                                ta.getTaskDay() != null &&
                                !ta.getTaskDay().isBefore(wr.getWeekStart()) &&
                                !ta.getTaskDay().isAfter(wr.getWeekEnd())))
                .penalize(HardSoftScore.ONE_HARD, WeekRequirement::getMinWeekCount)
                .asConstraint("HC6a: Repeating task zero occurrences in week");
    }

    /**
     * HC6b: Repeating task has 1..(minWeekCount-1) occurrences in a week (under minimum).
     * Penalty: minWeekCount - actualCount.
     */
    private Constraint hc6bRepeatingUnderMinWeekCount(ConstraintFactory factory) {
        return factory.forEach(WeekRequirement.class)
                .filter(wr -> wr.getMinWeekCount() > 0)
                .join(TaskAssignment.class,
                        Joiners.equal(WeekRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((wr, ta) ->
                                ta.getStartMinute() != null &&
                                ta.getTaskDay() != null &&
                                !ta.getTaskDay().isBefore(wr.getWeekStart()) &&
                                !ta.getTaskDay().isAfter(wr.getWeekEnd())))
                .groupBy((wr, ta) -> wr, ConstraintCollectors.countBi())
                .filter((wr, count) -> count < wr.getMinWeekCount())
                .penalize(HardSoftScore.ONE_HARD, (wr, count) -> wr.getMinWeekCount() - count)
                .asConstraint("HC6b: Repeating task under min week count");
    }

    /**
     * HC6c: Repeating task has more than optWeekCount occurrences in a week (over optimal).
     * Penalty: actualCount - optWeekCount.
     */
    private Constraint hc6cRepeatingOverOptWeekCount(ConstraintFactory factory) {
        return factory.forEach(WeekRequirement.class)
                .join(TaskAssignment.class,
                        Joiners.equal(WeekRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((wr, ta) ->
                                ta.getStartMinute() != null &&
                                ta.getTaskDay() != null &&
                                !ta.getTaskDay().isBefore(wr.getWeekStart()) &&
                                !ta.getTaskDay().isAfter(wr.getWeekEnd())))
                .groupBy((wr, ta) -> wr, ConstraintCollectors.countBi())
                .filter((wr, count) -> count > wr.getOptWeekCount())
                .penalize(HardSoftScore.ONE_HARD, (wr, count) -> count - wr.getOptWeekCount())
                .asConstraint("HC6c: Repeating task over opt week count");
    }

    /**
     * HC7a: Repeating task has ZERO scheduled occurrences on a required day.
     * Penalty: minDayCount.
     */
    private Constraint hc7aRepeatingZeroOccurrencesInDay(ConstraintFactory factory) {
        return factory.forEach(DayRequirement.class)
                .filter(dr -> dr.getMinDayCount() > 0)
                .ifNotExists(TaskAssignment.class,
                        Joiners.equal(DayRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((dr, ta) ->
                                ta.getStartMinute() != null &&
                                dr.getDate().equals(ta.getTaskDay())))
                .penalize(HardSoftScore.ONE_HARD, DayRequirement::getMinDayCount)
                .asConstraint("HC7a: Repeating task zero occurrences in day");
    }

    /**
     * HC7b: Repeating task has 1..(minDayCount-1) occurrences on a day (under minimum).
     * Penalty: minDayCount - actualCount.
     */
    private Constraint hc7bRepeatingUnderMinDayCount(ConstraintFactory factory) {
        return factory.forEach(DayRequirement.class)
                .filter(dr -> dr.getMinDayCount() > 0)
                .join(TaskAssignment.class,
                        Joiners.equal(DayRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((dr, ta) ->
                                ta.getStartMinute() != null &&
                                dr.getDate().equals(ta.getTaskDay())))
                .groupBy((dr, ta) -> dr, ConstraintCollectors.countBi())
                .filter((dr, count) -> count < dr.getMinDayCount())
                .penalize(HardSoftScore.ONE_HARD, (dr, count) -> dr.getMinDayCount() - count)
                .asConstraint("HC7b: Repeating task under min day count");
    }

    /**
     * HC7c: Repeating task has more than optDayCount occurrences on a day (over optimal).
     * Penalty: actualCount - optDayCount.
     */
    private Constraint hc7cRepeatingOverOptDayCount(ConstraintFactory factory) {
        return factory.forEach(DayRequirement.class)
                .join(TaskAssignment.class,
                        Joiners.equal(DayRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((dr, ta) ->
                                ta.getStartMinute() != null &&
                                dr.getDate().equals(ta.getTaskDay())))
                .groupBy((dr, ta) -> dr, ConstraintCollectors.countBi())
                .filter((dr, count) -> count > dr.getOptDayCount())
                .penalize(HardSoftScore.ONE_HARD, (dr, count) -> count - dr.getOptDayCount())
                .asConstraint("HC7c: Repeating task over opt day count");
    }

    /**
     * HC8: Assignments must not fall outside the planning horizon.
     * STUB — the value range [0, horizonMinutes) already prevents any out-of-horizon
     * assignment; this constraint can never fire.
     */
    //private Constraint hc8TaskWithinPlanningHorizon(ConstraintFactory factory) {
    //    return factory.forEach(TaskAssignment.class)
    //            .filter(a -> false)
    //            .penalize(HardSoftScore.ONE_HARD)
    //            .asConstraint("HC8: Task within planning horizon");
    //}

    /**
     * HC9: Non-repeating tasks may appear at most once (no duplicate scheduled task IDs).
     * In practice always 0 because ScheduleProblemBuilder creates exactly 1 TaskAssignment
     * per non-repeating task. Implemented for correctness.
     */
    //private Constraint hc9NonRepeatingTaskAtMostOnce(ConstraintFactory factory) {
     //   return factory.forEachUniquePair(TaskAssignment.class,
     //                   Joiners.equal(a -> a.getTask().getId()),
     //                   Joiners.filtering((a, b) ->
     //                           a.getTask().getRepeating() == null &&
     //                           a.getStartMinute() != null && b.getStartMinute() != null))
    //            .penalize(HardSoftScore.ONE_HARD)
    //            .asConstraint("HC9: Non-repeating task at most once");
    //}

    // -------------------------------------------------------------------------
    // Soft Constraints
    // -------------------------------------------------------------------------

    /**
     * SC1: Maximize total priority of scheduled tasks.
     * Penalty: (6 - priority) × 100 per scheduled assignment.
     * Higher-priority tasks have a smaller penalty, so the solver prefers to schedule them.
     */
    private Constraint sc1MaximizeScheduledTaskPriority(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(a -> a.getStartMinute() != null)
                .reward(HardSoftScore.ofSoft(100), a -> 6 - a.getTask().getPriority())
                .asConstraint("SC1: Maximize scheduled task priority");
    }

    /**
     * SC2: Minimize difficulty above daily capacity.
     * For each DifficultyCapacityEntry day, if the sum of difficulties of all tasks
     * (fixed + dynamic) exceeds capacity, penalize the excess × 500.
     * Fixed-task difficulty contributions are pre-computed in DifficultyCapacityEntry.fixedDifficulty.
     */
    private Constraint sc2MinimizeDifficultyAboveCapacity(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(ta -> ta.getStartMinute() != null && ta.getTaskDay() != null)
                .groupBy(TaskAssignment::getTaskDay,
                        ConstraintCollectors.sum(ta -> ta.getTask().getDifficulty()))
                .join(DifficultyCapacityEntry.class,
                        Joiners.equal((day, dynSum) -> day, DifficultyCapacityEntry::getDate))
                .filter((day, dynSum, dc) -> dc.getFixedDifficulty() + dynSum > dc.getCapacity())
                .penalize(HardSoftScore.ofSoft(500),
                        (day, dynSum, dc) -> dc.getFixedDifficulty() + dynSum - dc.getCapacity())
                .asConstraint("SC2: Minimize difficulty above daily capacity");
    }

    /**
     * SC3 (Cluster): When strategy is Cluster, penalize gaps between difficult tasks
     * (difficulty >= 6) on the same day. Only consecutive pairs are counted (no
     * intermediate difficult task exists between a and b), matching the web reference
     * which iterates sorted tasks and sums adjacent gaps only.
     */
    private Constraint sc3ClusterDifficultTasks(ConstraintFactory factory) {
        return factory.forEach(DifficultTaskSchedulingStrategy.class)
                .filter(s -> s == DifficultTaskSchedulingStrategy.Cluster)
                .join(TaskAssignment.class,
                        Joiners.filtering((s, a) ->
                                a.getStartMinute() != null && a.getTask().getDifficulty() >= 6))
                .join(TaskAssignment.class,
                        Joiners.filtering((s, a, b) ->
                                b.getStartMinute() != null &&
                                b.getTask().getDifficulty() >= 6 &&
                                a.getDayOffset() == b.getDayOffset() &&
                                a.getStartMinute() < b.getStartMinute()))
                // Only fire for consecutive pairs: no difficult task c sits between a and b
                .ifNotExists(TaskAssignment.class,
                        Joiners.filtering((s, a, b, c) ->
                                c.getStartMinute() != null &&
                                c.getTask().getDifficulty() >= 6 &&
                                a.getDayOffset() == c.getDayOffset() &&
                                a.getStartMinute() < c.getStartMinute() &&
                                c.getStartMinute() < b.getStartMinute()))
                .penalize(HardSoftScore.ONE_SOFT,
                        (s, a, b) -> Math.max(0, b.getStartMinute() - a.getEndMinute()))
                .asConstraint("SC3: Cluster difficult tasks - penalize gaps");
    }

    /**
     * SC3 (Even): When strategy is Even, reward gaps between difficult tasks
     * (difficulty >= 6) on the same day. Only consecutive pairs are counted (no
     * intermediate difficult task exists between a and b), matching the web reference.
     */
    private Constraint sc3EvenDifficultTasks(ConstraintFactory factory) {
        return factory.forEach(DifficultTaskSchedulingStrategy.class)
                .filter(s -> s == DifficultTaskSchedulingStrategy.Even)
                .join(TaskAssignment.class,
                        Joiners.filtering((s, a) ->
                                a.getStartMinute() != null && a.getTask().getDifficulty() >= 6))
                .join(TaskAssignment.class,
                        Joiners.filtering((s, a, b) ->
                                b.getStartMinute() != null &&
                                b.getTask().getDifficulty() >= 6 &&
                                a.getDayOffset() == b.getDayOffset() &&
                                a.getStartMinute() < b.getStartMinute()))
                // Only fire for consecutive pairs: no difficult task c sits between a and b
                .ifNotExists(TaskAssignment.class,
                        Joiners.filtering((s, a, b, c) ->
                                c.getStartMinute() != null &&
                                c.getTask().getDifficulty() >= 6 &&
                                a.getDayOffset() == c.getDayOffset() &&
                                a.getStartMinute() < c.getStartMinute() &&
                                c.getStartMinute() < b.getStartMinute()))
                .reward(HardSoftScore.ONE_SOFT,
                        (s, a, b) -> Math.max(0, b.getStartMinute() - a.getEndMinute()))
                .asConstraint("SC3: Even difficult tasks - reward gaps");
    }

    /**
     * SC4: Maximize user-defined task type preferences per day.
     * For each scheduled task, look up the TaskTypePreferenceEntry for its day,
     * sum the weights of matching task types, and reward that total.
     */
    private Constraint sc4TaskTypePreferences(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(a -> a.getStartMinute() != null)
                .join(TaskTypePreferenceEntry.class,
                        Joiners.filtering((ta, pref) ->
                                ta.getTaskDay() != null &&
                                ta.getTaskDay().equals(pref.getDate())))
                .reward(HardSoftScore.ONE_SOFT, (ta, pref) -> {
                    if (pref.getPreferences() == null || ta.getTask().getTypes() == null) return 0;
                    return pref.getPreferences().stream()
                            .filter(p -> ta.getTask().getTypes().contains(p.getType()))
                            .mapToInt(TaskTypeWeight::getWeight)
                            .sum();
                })
                .asConstraint("SC4: Task type preferences");
    }

    /**
     * SC5a: Week-repeating task has ZERO scheduled occurrences in a week (under optimal).
     * Penalty (soft): optWeekCount × 50.
     */
    private Constraint sc5aWeekRepeatingZeroOccurrences(ConstraintFactory factory) {
        return factory.forEach(WeekRequirement.class)
                .ifNotExists(TaskAssignment.class,
                        Joiners.equal(WeekRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((wr, ta) ->
                                ta.getStartMinute() != null &&
                                ta.getTaskDay() != null &&
                                !ta.getTaskDay().isBefore(wr.getWeekStart()) &&
                                !ta.getTaskDay().isAfter(wr.getWeekEnd())))
                .penalize(HardSoftScore.ofSoft(50), WeekRequirement::getOptWeekCount)
                .asConstraint("SC5a: Week-repeating zero occurrences in week");
    }

    /**
     * SC5b: Week-repeating task has fewer than optWeekCount occurrences in a week.
     * Penalty (soft): (optWeekCount - actualCount) × 50.
     */
    private Constraint sc5bWeekRepeatingUnderOptCount(ConstraintFactory factory) {
        return factory.forEach(WeekRequirement.class)
                .join(TaskAssignment.class,
                        Joiners.equal(WeekRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((wr, ta) ->
                                ta.getStartMinute() != null &&
                                ta.getTaskDay() != null &&
                                !ta.getTaskDay().isBefore(wr.getWeekStart()) &&
                                !ta.getTaskDay().isAfter(wr.getWeekEnd())))
                .groupBy((wr, ta) -> wr, ConstraintCollectors.countBi())
                .filter((wr, count) -> count < wr.getOptWeekCount())
                .penalize(HardSoftScore.ofSoft(50), (wr, count) -> wr.getOptWeekCount() - count)
                .asConstraint("SC5b: Week-repeating under optimal week count");
    }

    /**
     * SC6a: Day-repeating task has ZERO scheduled occurrences on a day (under optimal).
     * Penalty (soft): optDayCount × 50.
     */
    private Constraint sc6aDayRepeatingZeroOccurrences(ConstraintFactory factory) {
        return factory.forEach(DayRequirement.class)
                .ifNotExists(TaskAssignment.class,
                        Joiners.equal(DayRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((dr, ta) ->
                                ta.getStartMinute() != null &&
                                dr.getDate().equals(ta.getTaskDay())))
                .penalize(HardSoftScore.ofSoft(50), DayRequirement::getOptDayCount)
                .asConstraint("SC6a: Day-repeating zero occurrences in day");
    }

    /**
     * SC6b: Day-repeating task has fewer than optDayCount occurrences on a day.
     * Penalty (soft): (optDayCount - actualCount) × 50.
     */
    private Constraint sc6bDayRepeatingUnderOptCount(ConstraintFactory factory) {
        return factory.forEach(DayRequirement.class)
                .join(TaskAssignment.class,
                        Joiners.equal(DayRequirement::getTask, TaskAssignment::getTask),
                        Joiners.filtering((dr, ta) ->
                                ta.getStartMinute() != null &&
                                dr.getDate().equals(ta.getTaskDay())))
                .groupBy((dr, ta) -> dr, ConstraintCollectors.countBi())
                .filter((dr, count) -> count < dr.getOptDayCount())
                .penalize(HardSoftScore.ofSoft(50), (dr, count) -> dr.getOptDayCount() - count)
                .asConstraint("SC6b: Day-repeating under optimal day count");
    }

    /**
     * SC7a: Days WITH at least one scheduled dynamic task.
     * Penalize (fixedDifficulty + dynamicDifficulty)² for each such day.
     * Combined with SC7b and SC7c this yields Σdi² − ⌊T²/n⌋ = ⌈Σ(di−avg)²⌉,
     * matching the web reference formula exactly.
     */
    private Constraint sc7DaysWithTasks(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(ta -> ta.getStartMinute() != null && ta.getTaskDay() != null)
                .groupBy(TaskAssignment::getTaskDay,
                        ConstraintCollectors.sum(ta -> ta.getTask().getDifficulty()))
                .join(DayFact.class,
                        Joiners.equal((day, dynSum) -> day, DayFact::getDate))
                .penalize(HardSoftScore.ONE_SOFT, (day, dynSum, df) -> {
                    int total = df.getFixedDifficulty() + dynSum;
                    return total * total;
                })
                .asConstraint("SC7a: Difficulty imbalance - days with tasks");
    }

    /**
     * SC7b: Days with NO scheduled dynamic tasks.
     * Penalize fixedDifficulty² so these days are included in the Σdi² sum.
     * The web reference includes all horizon days (even zero-task days) in the variance.
     */
    private Constraint sc7DaysWithoutTasks(ConstraintFactory factory) {
        return factory.forEach(DayFact.class)
                .ifNotExists(TaskAssignment.class,
                        Joiners.equal(DayFact::getDate, TaskAssignment::getTaskDay),
                        Joiners.filtering((df, ta) -> ta.getStartMinute() != null))
                .penalize(HardSoftScore.ONE_SOFT,
                        df -> df.getFixedDifficulty() * df.getFixedDifficulty())
                .asConstraint("SC7b: Difficulty imbalance - days without tasks");
    }

    /**
     * SC7c: Global variance correction — reward ⌊T²/n⌋.
     *
     * The web computes ⌈Σdi² − T²/n⌉. Since Σdi² is always an integer and
     * ⌈X − frac⌉ = X − ⌊frac⌋ when X is integer and 0 < frac < 1, this equals
     * Σdi² − ⌊T²/n⌋. Rewarding ⌊T²/n⌋ here achieves that subtraction.
     *
     * T = totalFixedDifficulty + totalDynamicDifficulty
     * n = numDays (from PlanningHorizonFact)
     */
    private Constraint sc7GlobalCorrection(ConstraintFactory factory) {
        return factory.forEach(TaskAssignment.class)
                .filter(ta -> ta.getStartMinute() != null)
                .groupBy(ConstraintCollectors.sum(ta -> ta.getTask().getDifficulty()))
                .join(PlanningHorizonFact.class)
                .reward(HardSoftScore.ONE_SOFT, (dynTotal, phf) -> {
                    int n = phf.getNumDays();
                    if (n == 0) return 0;
                    int T = phf.getTotalFixedDifficulty() + dynTotal;
                    return (T * T) / n;
                })
                .asConstraint("SC7c: Difficulty imbalance - variance correction");
    }
}
