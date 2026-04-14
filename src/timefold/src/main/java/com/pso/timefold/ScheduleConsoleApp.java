package com.pso.timefold;

import ai.timefold.solver.core.api.solver.Solver;
import ai.timefold.solver.core.api.solver.SolverFactory;
import ai.timefold.solver.core.config.solver.SolverConfig;
import ai.timefold.solver.core.config.solver.termination.TerminationConfig;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.pso.timefold.domain.ScheduleSolution;
import com.pso.timefold.dto.GenerateScheduleRequest;
import com.pso.timefold.dto.ScheduledTask;
import com.pso.timefold.solver.ScheduleProblemBuilder;

import java.io.File;
import java.io.InputStream;
import java.util.Comparator;
import java.util.List;

/**
 * Standalone CLI entry point for the schedule optimizer.
 *
 * Does NOT use Spring Boot — runs the solver synchronously via SolverFactory.
 * Useful for local testing and benchmarking without a running server.
 *
 * Usage:
 *   mvn exec:java -Dexec.mainClass=com.pso.timefold.ScheduleConsoleApp
 *   mvn exec:java -Dexec.mainClass=com.pso.timefold.ScheduleConsoleApp -Dexec.args="path/to/input.json"
 *
 * If no argument is provided, reads from ./input.json in the working directory.
 */
public class ScheduleConsoleApp {

    public static void main(String[] args) throws Exception {
        System.out.println("=== Personalized Schedule Optimizer (Timefold) ===");

        ObjectMapper mapper = new ObjectMapper()
                .registerModule(new JavaTimeModule())
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);

        // 1. Deserialize request — explicit file path arg, or fall back to classpath input.json
        GenerateScheduleRequest request;
        if (args.length > 0) {
            String inputPath = args[0];
            System.out.println("Reading problem from: " + new File(inputPath).getAbsolutePath());
            request = mapper.readValue(new File(inputPath), GenerateScheduleRequest.class);
        } else {
            System.out.println("No path given — loading input.json from classpath (src/main/resources/input.json)");
            try (InputStream is = ScheduleConsoleApp.class.getClassLoader().getResourceAsStream("input.json")) {
                if (is == null) {
                    throw new IllegalStateException(
                            "input.json not found on classpath. Either place it in src/main/resources/ " +
                            "or pass a file path as the first argument.");
                }
                request = mapper.readValue(is, GenerateScheduleRequest.class);
            }
        }
        System.out.printf("Loaded: %d dynamic tasks, %d fixed tasks, horizon %s → %s%n",
                request.getDynamicTasks() != null ? request.getDynamicTasks().size() : 0,
                request.getFixedTasks() != null ? request.getFixedTasks().size() : 0,
                request.getPlanningHorizon() != null ? request.getPlanningHorizon().getStartDate() : "?",
                request.getPlanningHorizon() != null ? request.getPlanningHorizon().getEndDate() : "?");

        // 2. Build initial solution (all tasks unscheduled)
        ScheduleSolution initialSolution = ScheduleProblemBuilder.buildSolution(request);
        System.out.printf("Created %d task assignment slots%n", initialSolution.getTaskAssignments().size());

        // 3. Solve synchronously using SolverFactory (reads solverConfig.xml from classpath)
        int timeSeconds = request.getOptimizationTimeInSeconds() > 0
                ? request.getOptimizationTimeInSeconds() : 15;
        System.out.printf("Solving for up to %d seconds...%n", timeSeconds);
        long startMs = System.currentTimeMillis();

        // Override termination from request so the solver honours optimizationTimeInSeconds
        SolverConfig solverConfig = SolverConfig.createFromXmlResource("solverConfig.xml");
        solverConfig.setTerminationConfig(
                new TerminationConfig().withSpentLimit(java.time.Duration.ofSeconds(timeSeconds)));
        SolverFactory<ScheduleSolution> factory = SolverFactory.create(solverConfig);
        Solver<ScheduleSolution> solver = factory.buildSolver();
        ScheduleSolution solution = solver.solve(initialSolution);

        long elapsedMs = System.currentTimeMillis() - startMs;
        System.out.printf("Solver finished in %.1f seconds%n", elapsedMs / 1000.0);

        // 4. Print results
        System.out.println();
        System.out.println("Final score: " + solution.getScore());
        System.out.println();

        List<ScheduledTask> timeline = ScheduleProblemBuilder.extractTimeline(solution);
        if (timeline.isEmpty()) {
            System.out.println("No tasks were scheduled (all constraints are placeholders — score is always 0hard/0soft).");
        } else {
            System.out.printf("Scheduled %d tasks:%n", timeline.size());
            System.out.println("─".repeat(80));

            timeline.stream()
                    .sorted(Comparator.comparing(ScheduledTask::getStartTime))
                    .forEach(t -> System.out.printf(
                            "[%s] %-35s %s → %s  (priority=%d, difficulty=%d)%n",
                            t.isFixed() ? "FIXED  " : "DYNAMIC",
                            t.getName(),
                            t.getStartTime(),
                            t.getEndTime(),
                            t.getPriority(),
                            t.getDifficulty()
                    ));
        }

        long unscheduled = solution.getTaskAssignments().stream()
                .filter(a -> a.getStartMinute() == null)
                .count();
        if (unscheduled > 0) {
            System.out.printf("%nUnscheduled assignment slots: %d%n", unscheduled);
        }
    }
}
