// Package config reads the service's settings from environment variables.
package config

import (
	"errors"
	"fmt"
	"log/slog"
	"strconv"
)

const (
	// DefaultExchange is the durable topic exchange every service publishes its
	// domain events to (ExchangeNames.Events in the C# message-bus library);
	// the routing key says which event a message is.
	DefaultExchange = "homesecurity.events"

	// DefaultQueue is this service's own durable queue. It is deliberately not
	// shared with any other consumer: each consumer service binds its own queue
	// to the exchange so every one of them sees every message.
	DefaultQueue = "homesecurity.logs-processor"

	// DefaultRoutingKey mirrors RoutingKeys.LogsIngested in the C# producer.
	DefaultRoutingKey = "logs.ingested"

	// DefaultPrefetch matches EasyNetQ's default consumer prefetch.
	DefaultPrefetch = 50

	// DefaultDeliveryLimit is how many times the broker redelivers a message
	// that keeps being requeued before dead-lettering it.
	DefaultDeliveryLimit = 5

	DefaultHealthAddr = ":8080"
)

// Config holds everything the service needs to run.
type Config struct {
	RabbitMQURL   string
	Exchange      string
	Queue         string
	RoutingKey    string
	Prefetch      int
	DeliveryLimit int
	HealthAddr    string
	// LogLevel is the minimum level written (LOG_LEVEL: debug, info, warn,
	// error) - the counterpart of Serilog's MinimumLevel in the .NET services.
	LogLevel slog.Level
}

// FromEnv builds a Config from environment variables, using getenv (normally
// os.Getenv) so tests don't have to touch the process environment.
func FromEnv(getenv func(string) string) (Config, error) {
	cfg := Config{
		RabbitMQURL: getenv("RABBITMQ_URL"),
		Exchange:    valueOr(getenv("RABBITMQ_EXCHANGE"), DefaultExchange),
		Queue:       valueOr(getenv("RABBITMQ_QUEUE"), DefaultQueue),
		RoutingKey:  valueOr(getenv("RABBITMQ_ROUTING_KEY"), DefaultRoutingKey),
		HealthAddr:  valueOr(getenv("HEALTH_ADDR"), DefaultHealthAddr),
	}

	if cfg.RabbitMQURL == "" {
		return Config{}, errors.New("RABBITMQ_URL is required")
	}

	// An unset LOG_LEVEL leaves the zero value, which is slog.LevelInfo.
	if raw := getenv("LOG_LEVEL"); raw != "" {
		if err := cfg.LogLevel.UnmarshalText([]byte(raw)); err != nil {
			return Config{}, fmt.Errorf("LOG_LEVEL must be debug, info, warn or error, got %q", raw)
		}
	}

	var err error
	if cfg.Prefetch, err = positiveInt(getenv, "RABBITMQ_PREFETCH", DefaultPrefetch); err != nil {
		return Config{}, err
	}
	if cfg.DeliveryLimit, err = positiveInt(getenv, "RABBITMQ_DELIVERY_LIMIT", DefaultDeliveryLimit); err != nil {
		return Config{}, err
	}

	return cfg, nil
}

func valueOr(value, fallback string) string {
	if value == "" {
		return fallback
	}
	return value
}

func positiveInt(getenv func(string) string, name string, fallback int) (int, error) {
	raw := getenv(name)
	if raw == "" {
		return fallback, nil
	}
	n, err := strconv.Atoi(raw)
	if err != nil || n < 1 {
		return 0, fmt.Errorf("%s must be a positive integer, got %q", name, raw)
	}
	return n, nil
}
