package com.pso.timefold.service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpEntity;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestTemplate;

import java.util.List;
import java.util.Map;
import java.util.UUID;

@Service
public class CallbackService {

    private static final Logger log = LoggerFactory.getLogger(CallbackService.class);

    @Value("${callbacks.schedule-submit-url:}")
    private String callbackUrl;

    @Value("${internal-api.shared-secret:}")
    private String sharedSecret;

    private final RestTemplate restTemplate = new RestTemplate();

    @Async
    public void postEmptyResultAsync(UUID jobId) {
        if (callbackUrl == null || callbackUrl.isBlank()) {
            log.info("No callback URL configured. Job {} result will not be sent.", jobId);
            return;
        }

        try {
            // TODO: replace with actual solver result when implementing the Timefold solver
            Map<String, Object> payload = Map.of(
                "JobId", jobId.toString(),
                "TasksTimeline", List.of()
            );

            HttpHeaders headers = new HttpHeaders();
            headers.setContentType(MediaType.APPLICATION_JSON);
            if (sharedSecret != null && !sharedSecret.isBlank()) {
                headers.set("X-Internal-Token", sharedSecret);
            }

            HttpEntity<Map<String, Object>> entity = new HttpEntity<>(payload, headers);
            restTemplate.postForObject(callbackUrl, entity, String.class);
            log.info("Job {} result sent to callback successfully.", jobId);
        } catch (Exception e) {
            log.error("Failed to send job {} result to callback URL {}.", jobId, callbackUrl, e);
        }
    }
}
