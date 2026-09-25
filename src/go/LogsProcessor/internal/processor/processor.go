// Package processor holds the log-batch processing logic. It knows nothing
// about RabbitMQ: the consumer hands it already-decoded messages.
package processor

import (
	"context"
	"log/slog"

	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/contracts"
)

// Processor handles one log batch taken off the queue.
type Processor struct {
	log *slog.Logger
}

// New returns a Processor logging through log.
func New(log *slog.Logger) *Processor {
	return &Processor{log: log.With("component", "processor")}
}

// Process handles a single log batch.
//
// TODO: real data processing will be implemented later - Redis SETNX
// idempotency on MessageId (docs/architecture/07-caching-and-idempotency.md),
// unknown-device detection per (tenant, MAC), and handing the result to
// Devices.Service. For now messages are only taken off the queue.
func (p *Processor) Process(_ context.Context, msg contracts.LogBatchMessage) error {
	events := 0
	for _, d := range msg.Devices {
		events += len(d.Events)
	}
	// Counts only: log-entry contents are never logged (docs/architecture/09-observability.md).
	p.log.Debug("log batch received, nothing to process yet",
		"message_id", msg.MessageId,
		"tenant_id", msg.TenantId,
		"devices", len(msg.Devices),
		"events", events)
	return nil
}
