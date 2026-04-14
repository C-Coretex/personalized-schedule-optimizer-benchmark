package com.pso.timefold.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.pso.timefold.dto.ScheduledTask;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpEntity;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.converter.json.MappingJackson2HttpMessageConverter;
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

    private final RestTemplate restTemplate;

    public CallbackService() {
        ObjectMapper mapper = new ObjectMapper()
                .registerModule(new JavaTimeModule())
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        MappingJackson2HttpMessageConverter converter = new MappingJackson2HttpMessageConverter(mapper);
        RestTemplate rt = new RestTemplate();
        rt.getMessageConverters().removeIf(c -> c instanceof MappingJackson2HttpMessageConverter);
        rt.getMessageConverters().add(0, converter);
        this.restTemplate = rt;
    }

    /**
     * Posts the solver result to the web callback URL asynchronously.
     *
     * Payload format:
     * {
     *   "JobId": "<uuid>",
     *   "TasksTimeline": [
     *     { "id": "...", "name": "...", "startTime": "...", "endTime": "...",
     *       "priority": 3, "difficulty": 5, "fixed": false }
     *   ]
     * }
     */
    @Async
    public void postResultAsync(UUID jobId, List<ScheduledTask> timeline) {
        if (callbackUrl == null || callbackUrl.isBlank()) {
            log.info("No callback URL configured. Job {} result will not be sent.", jobId);
            return;
        }

        try {
            Map<String, Object> payload = Map.of(
                    "JobId", jobId.toString(),
                    "TasksTimeline", timeline
            );

            HttpHeaders headers = new HttpHeaders();
            headers.setContentType(MediaType.APPLICATION_JSON);
            if (sharedSecret != null && !sharedSecret.isBlank()) {
                headers.set("X-Internal-Token", sharedSecret);
            }

            HttpEntity<Map<String, Object>> entity = new HttpEntity<>(payload, headers);
            restTemplate.postForObject(callbackUrl, entity, String.class);
            log.info("Job {} result ({} tasks) sent to callback successfully.",
                    jobId, timeline.size());
        } catch (Exception e) {
            log.error("Failed to send job {} result to callback URL {}.", jobId, callbackUrl, e);
        }
    }
}
