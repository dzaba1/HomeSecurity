package config

import (
	"log/slog"
	"testing"
)

func env(values map[string]string) func(string) string {
	return func(name string) string { return values[name] }
}

func TestFromEnvDefaults(t *testing.T) {
	cfg, err := FromEnv(env(map[string]string{"RABBITMQ_URL": "amqp://u:p@rabbitmq:5672/"}))
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if cfg.Exchange != DefaultExchange || cfg.Queue != DefaultQueue || cfg.RoutingKey != DefaultRoutingKey {
		t.Errorf("topology defaults not applied: %+v", cfg)
	}
	if cfg.Prefetch != DefaultPrefetch || cfg.DeliveryLimit != DefaultDeliveryLimit || cfg.HealthAddr != DefaultHealthAddr {
		t.Errorf("numeric/address defaults not applied: %+v", cfg)
	}
}

func TestFromEnvOverrides(t *testing.T) {
	cfg, err := FromEnv(env(map[string]string{
		"RABBITMQ_URL":            "amqp://x",
		"RABBITMQ_EXCHANGE":       "ex",
		"RABBITMQ_QUEUE":          "q",
		"RABBITMQ_ROUTING_KEY":    "rk",
		"RABBITMQ_PREFETCH":       "7",
		"RABBITMQ_DELIVERY_LIMIT": "3",
		"HEALTH_ADDR":             ":9090",
		"LOG_LEVEL":               "debug",
	}))
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if cfg.Exchange != "ex" || cfg.Queue != "q" || cfg.RoutingKey != "rk" || cfg.Prefetch != 7 || cfg.DeliveryLimit != 3 || cfg.HealthAddr != ":9090" || cfg.LogLevel != slog.LevelDebug {
		t.Errorf("overrides not applied: %+v", cfg)
	}
}

func TestFromEnvRejectsInvalid(t *testing.T) {
	cases := map[string]map[string]string{
		"missing url":    {},
		"bad prefetch":   {"RABBITMQ_URL": "amqp://x", "RABBITMQ_PREFETCH": "abc"},
		"zero prefetch":  {"RABBITMQ_URL": "amqp://x", "RABBITMQ_PREFETCH": "0"},
		"negative limit": {"RABBITMQ_URL": "amqp://x", "RABBITMQ_DELIVERY_LIMIT": "-1"},
		"bad log level":  {"RABBITMQ_URL": "amqp://x", "LOG_LEVEL": "chatty"},
	}
	for name, values := range cases {
		t.Run(name, func(t *testing.T) {
			if _, err := FromEnv(env(values)); err == nil {
				t.Error("expected an error")
			}
		})
	}
}
