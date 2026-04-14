package com.pso.timefold.controller;

import com.pso.timefold.dto.GenerateScheduleRequest;
import com.pso.timefold.dto.ScheduledTask;
import com.pso.timefold.service.SolverService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.Map;
import java.util.UUID;

@RestController
@RequestMapping("/jobs")
public class JobsController {

    private static final Logger log = LoggerFactory.getLogger(JobsController.class);

    private final SolverService solverService;

    public JobsController(SolverService solverService) {
        this.solverService = solverService;
    }

    // POST /jobs/run — accepts a schedule request, starts async Timefold solving,
    // returns a job ID immediately. The result is posted to the callback URL on completion.
    @PostMapping("/run")
    public ResponseEntity<UUID> run(@RequestBody GenerateScheduleRequest request) {
        UUID jobId = solverService.submitJob(request);
        log.info("Submitted solver job {}", jobId);
        return ResponseEntity.ok(jobId);
    }

    // GET /jobs/status?id=<uuid>
    @GetMapping("/status")
    public ResponseEntity<Map<String, Object>> status(@RequestParam UUID id) {
        return ResponseEntity.ok(solverService.getStatus(id));
    }

    // GET /jobs/current-solution?id=<uuid>
    @GetMapping("/current-solution")
    public ResponseEntity<List<ScheduledTask>> currentSolution(@RequestParam UUID id) {
        return ResponseEntity.ok(solverService.getCurrentSolution(id));
    }
}
