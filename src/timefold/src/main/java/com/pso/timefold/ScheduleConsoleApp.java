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
import java.lang.management.ManagementFactory;
import java.lang.management.ThreadMXBean;
import java.util.Comparator;
import java.util.List;
import java.util.concurrent.atomic.AtomicBoolean;

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

    private static long sumThreadCpuNs(ThreadMXBean threadBean) {
        long total = 0;
        for (long id : threadBean.getAllThreadIds()) {
            long t = threadBean.getThreadCpuTime(id);
            if (t > 0) total += t;
        }
        return total;
    }

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

        // ── Performance monitoring setup ─────────────────────────────────────────
        Runtime      runtime     = Runtime.getRuntime();
        ThreadMXBean threadBean  = ManagementFactory.getThreadMXBean();
        int          logicalCores = runtime.availableProcessors();

        long[]   baselineBytes = {0};
        long[]   peakBytes     = {0};
        long[]   totalBytes    = {0};
        double[] peakCpu       = {0};
        double[] totalCpu      = {0};
        long[]   lastCpuNs     = {sumThreadCpuNs(threadBean)};
        long[]   lastTimeNs    = {System.nanoTime()};
        int[]    samples       = {0};
        AtomicBoolean monitoring = new AtomicBoolean(true);

        Thread monitorThread = new Thread(() -> {
            while (monitoring.get()) {
                try { Thread.sleep(100); }
                catch (InterruptedException e) { Thread.currentThread().interrupt(); break; }
                if (baselineBytes[0] == 0) continue;

                long usedHeap     = runtime.totalMemory() - runtime.freeMemory();
                long currentBytes = Math.max(0, usedHeap - baselineBytes[0]);
                peakBytes[0] = Math.max(peakBytes[0], currentBytes);
                totalBytes[0] += currentBytes;

                long   now          = System.nanoTime();
                long   currentCpuNs = sumThreadCpuNs(threadBean);
                double cpuUsedNs    = currentCpuNs - lastCpuNs[0];
                double elapsedNs    = now - lastTimeNs[0];
                double cpuPct       = elapsedNs > 0 ? cpuUsedNs / (elapsedNs * logicalCores) * 100.0 : 0;
                peakCpu[0]    = Math.max(peakCpu[0], cpuPct);
                totalCpu[0]  += cpuPct;
                lastCpuNs[0]  = currentCpuNs;
                lastTimeNs[0] = now;
                samples[0]++;
            }
        });
        monitorThread.setDaemon(true);
        monitorThread.start();
        // ─────────────────────────────────────────────────────────────────────────

        // Override termination from request so the solver honours optimizationTimeInSeconds
        SolverConfig solverConfig = SolverConfig.createFromXmlResource("solverConfig.xml");
        solverConfig.setTerminationConfig(
                new TerminationConfig().withSpentLimit(java.time.Duration.ofSeconds(timeSeconds)));
        SolverFactory<ScheduleSolution> factory = SolverFactory.create(solverConfig);
        Solver<ScheduleSolution> solver = factory.buildSolver();

        long startMs = System.currentTimeMillis();
        baselineBytes[0] = runtime.totalMemory() - runtime.freeMemory();  // baseline right before solve
        ScheduleSolution solution = solver.solve(initialSolution);
        long elapsedMs = System.currentTimeMillis() - startMs;

        monitoring.set(false);
        monitorThread.interrupt();
        monitorThread.join();

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

        // ── Performance report ────────────────────────────────────────────────────
        double peakMb  = peakBytes[0] / 1024.0 / 1024.0;
        double avgMem  = samples[0] > 0 ? totalBytes[0] / (double) samples[0] / 1024.0 / 1024.0 : 0;
        double avgCpu  = samples[0] > 0 ? totalCpu[0] / samples[0] : 0;

        System.out.println();
        System.out.println("╔══════════════════════════════════════════════════════════════╗");
        System.out.println("║                      PERFORMANCE                            ║");
        System.out.println("╚══════════════════════════════════════════════════════════════╝");
        System.out.printf("  Elapsed time:           %02d:%02d.%03d%n",
                elapsedMs / 60000, (elapsedMs / 1000) % 60, elapsedMs % 1000);
        System.out.printf("  Logical cores:          %d%n", logicalCores);
        System.out.printf("  Peak committed memory:  %.2f MB%n", peakMb);
        System.out.printf("  Average committed mem:  %.2f MB%n", avgMem);
        System.out.printf("  Peak CPU usage:         %.1f%%%n", peakCpu[0]);
        System.out.printf("  Average CPU usage:      %.1f%%%n", avgCpu);
        System.out.printf("  Samples taken:          %d (over ~%.1fs)%n", samples[0], samples[0] / 10.0);
    }
}
