// Command logs-processor consumes log batches published by the Logs Ingestion
// service. It is written in Go on purpose: services on the message bus share
// only the JSON-Schema contract, not a language or a runtime.
package main

import (
	"context"
	"errors"
	"flag"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/config"
	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/consumer"
	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/health"
	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/processor"
)

func main() {
	healthcheck := flag.Bool("healthcheck", false, "probe the running service's readiness endpoint and exit (for the container HEALTHCHECK; the runtime image has no curl)")
	flag.Parse()

	// slog is Go's standard structured logger (the ILogger<T> counterpart):
	// JSON lines on stdout, like Serilog's console JSON output in the .NET
	// services. Until the config is read, log at the default level.
	log := slog.New(slog.NewJSONHandler(os.Stdout, nil))

	cfg, err := config.FromEnv(os.Getenv)
	if err != nil {
		log.Error("invalid configuration", "error", err)
		os.Exit(2)
	}
	log = slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: cfg.LogLevel}))

	if *healthcheck {
		if err := health.Probe(context.Background(), cfg.HealthAddr); err != nil {
			log.Error("health probe failed", "error", err)
			os.Exit(1)
		}
		return
	}

	if err := run(cfg, log); err != nil {
		log.Error("logs-processor stopped with an error", "error", err)
		os.Exit(1)
	}
	log.Info("logs-processor stopped")
}

func run(cfg config.Config, log *slog.Logger) error {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	// The broker URL carries credentials, so it is deliberately not logged.
	log.Info("logs-processor starting",
		"exchange", cfg.Exchange,
		"queue", cfg.Queue,
		"routing_key", cfg.RoutingKey,
		"prefetch", cfg.Prefetch,
		"delivery_limit", cfg.DeliveryLimit,
		"health_addr", cfg.HealthAddr,
		"log_level", cfg.LogLevel.String())

	healthLog := log.With("component", "health")
	var readiness health.Readiness
	srv := &http.Server{
		Addr:              cfg.HealthAddr,
		Handler:           readiness.Handler(),
		ReadHeaderTimeout: 5 * time.Second,
	}
	serverErr := make(chan error, 1)
	go func() {
		healthLog.Info("health endpoint listening", "addr", cfg.HealthAddr, "path", health.Path)
		if err := srv.ListenAndServe(); !errors.Is(err, http.ErrServerClosed) {
			healthLog.Error("health endpoint failed", "error", err)
			serverErr <- err
		}
	}()

	consumerCtx, cancelConsumer := context.WithCancel(ctx)
	defer cancelConsumer()
	go func() {
		// A dead health endpoint would get the container killed anyway; stop
		// cleanly instead of consuming unobserved.
		select {
		case <-serverErr:
			cancelConsumer()
		case <-consumerCtx.Done():
		}
	}()

	c := consumer.New(cfg, processor.New(log), &readiness, log)
	err := c.Run(consumerCtx)

	shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if shutdownErr := srv.Shutdown(shutdownCtx); shutdownErr != nil {
		healthLog.Warn("health endpoint did not shut down cleanly", "error", shutdownErr)
	}

	select {
	case healthErr := <-serverErr:
		return healthErr
	default:
	}
	return err
}
