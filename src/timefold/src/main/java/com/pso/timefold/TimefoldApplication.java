package com.pso.timefold;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.scheduling.annotation.EnableAsync;

@SpringBootApplication
@EnableAsync
public class TimefoldApplication {

    public static void main(String[] args) {
        SpringApplication.run(TimefoldApplication.class, args);
    }
}
