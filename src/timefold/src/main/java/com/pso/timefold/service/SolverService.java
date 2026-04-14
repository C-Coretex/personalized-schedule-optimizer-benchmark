package com.pso.timefold.service;

import ai.timefold.solver.core.api.score.buildin.hardsoft.HardSoftScore;
import ai.timefold.solver.core.api.solver.SolverManager;
import ai.timefold.solver.core.api.solver.SolverStatus;
import com.pso.timefold.domain.ScheduleSolution;
import com.pso.timefold.dto.GenerateScheduleRequest;
import com.pso.timefold.dto.ScheduledTask;
import com.pso.timefold.solver.ScheduleProblemBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Orchestrates Timefold solving jobs.
 * Wraps SolverManager (auto-configured by timefold-solver-spring-boot-starter).
 */
@Service
public class SolverService {

    private static final Logger log = LoggerFactory.getLogger(SolverService.class);

    private final SolverManager<ScheduleSolution, UUID> solverManager;
    private final CallbackService callbackService;

    /** Stores the best solution seen so far per job. */
    private final ConcurrentHashMap<UUID, ScheduleSolution> bestSolutions = new ConcurrentHashMap<>();

    public SolverService(SolverManager<ScheduleSolution, UUID> solverManager,
                         CallbackService callbackService) {
        this.solverManager = solverManager;
        this.callbackService = callbackService;
    }

    /**
     * Builds the problem, registers a job, starts async solving.
     * Returns jobId immediately; the callback is fired on completion.
     */
    public UUID submitJob(GenerateScheduleRequest request) {
        UUID jobId = UUID.randomUUID();
        log.info("Starting solver job {} with {} dynamic tasks, {} fixed tasks",
                jobId,
                request.getDynamicTasks() != null ? request.getDynamicTasks().size() : 0,
                request.getFixedTasks() != null ? request.getFixedTasks().size() : 0);

        ScheduleSolution initialSolution = ScheduleProblemBuilder.buildSolution(request);
        bestSolutions.put(jobId, initialSolution);

        solverManager.solveBuilder()
                .withProblemId(jobId)
                .withProblemFinder(id -> bestSolutions.get(id))
                .withBestSolutionConsumer(solution -> {
                    bestSolutions.put(jobId, solution);
                    log.debug("Job {} new best score: {}", jobId,
                            solution.getScore() != null ? solution.getScore() : "null");
                })
                .withFinalBestSolutionConsumer(solution -> {
                    bestSolutions.put(jobId, solution);
                    log.info("Job {} finished. Final score: {}", jobId, solution.getScore());
                    List<ScheduledTask> timeline = ScheduleProblemBuilder.extractTimeline(solution);
                    callbackService.postResultAsync(jobId, timeline);
                })
                .withExceptionHandler((id, ex) ->
                        log.error("Job {} failed with exception", id, ex))
                .run();

        return jobId;
    }

    /**
     * Returns solver status and current score for GET /jobs/status.
     */
    public Map<String, Object> getStatus(UUID jobId) {
        SolverStatus status = solverManager.getSolverStatus(jobId);
        ScheduleSolution solution = bestSolutions.get(jobId);

        String statusStr = status != null ? status.toString() : "NOT_FOUND";
        HardSoftScore score = solution != null ? solution.getScore() : null;

        return Map.of(
                "status", statusStr,
                "score", score != null
                        ? Map.of("hardScore", score.hardScore(), "softScore", score.softScore())
                        : Map.of("hardScore", 0, "softScore", 0)
        );
    }

    /**
     * Returns the current best solution as a list of scheduled tasks.
     */
    public List<ScheduledTask> getCurrentSolution(UUID jobId) {
        ScheduleSolution solution = bestSolutions.get(jobId);
        if (solution == null) return List.of();
        return ScheduleProblemBuilder.extractTimeline(solution);
    }
}
