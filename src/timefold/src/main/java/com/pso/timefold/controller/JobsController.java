package com.pso.timefold.controller;

import com.pso.timefold.service.CallbackService;
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

    private final CallbackService callbackService;

    public JobsController(CallbackService callbackService) {
        this.callbackService = callbackService;
    }

    // POST /jobs/run — accepts the schedule request, returns a job ID immediately,
    // then posts an empty result to the callback URL in the background.
    // TODO: replace the placeholder with actual Timefold solver invocation.
    @PostMapping("/run")
    public ResponseEntity<UUID> run(@RequestBody Map<String, Object> request) {
        UUID jobId = UUID.randomUUID();
        log.info("Received schedule request, starting job {}", jobId);
        callbackService.postEmptyResultAsync(jobId);
        return ResponseEntity.ok(jobId);
    }

    // GET /jobs/status?id=<uuid>
    @GetMapping("/status")
    public ResponseEntity<Map<String, Object>> status(@RequestParam UUID id) {
        return ResponseEntity.ok(Map.of(
            "status", "InProgress",
            "score", Map.of("hardScore", 0, "softScore", 100000)
        ));
    }

    // GET /jobs/current-solution?id=<uuid>
    @GetMapping("/current-solution")
    public ResponseEntity<Map<String, Object>> currentSolution(@RequestParam UUID id) {
        return ResponseEntity.ok(Map.of());
    }
}
