// Package consumer takes log batches off the RabbitMQ queue, decodes them into
// the generated contract type and hands them to a Processor.
package consumer

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"net/url"
	"os"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"

	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/config"
	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/contracts"
)

const (
	initialBackoff = time.Second
	maxBackoff     = 30 * time.Second
)

// Processor handles one decoded log batch. Returning an error asks the broker
// to redeliver the message (until the queue's delivery limit dead-letters it).
type Processor interface {
	Process(ctx context.Context, msg contracts.LogBatchMessage) error
}

// Readiness is told whether the consumer is currently attached to the queue.
type Readiness interface {
	SetReady(ready bool)
}

// outcome is what should happen to a delivery once it has been handled.
type outcome int

const (
	// outcomeAck: processed, remove from the queue.
	outcomeAck outcome = iota
	// outcomeRequeue: processing failed, ask for redelivery.
	outcomeRequeue
	// outcomeDeadLetter: can never succeed (malformed), send to the DLQ now.
	outcomeDeadLetter
)

// Consumer consumes the logs queue until its context is cancelled.
type Consumer struct {
	cfg       config.Config
	processor Processor
	readiness Readiness
	log       *slog.Logger
}

// New creates a Consumer.
func New(cfg config.Config, processor Processor, readiness Readiness, log *slog.Logger) *Consumer {
	return &Consumer{cfg: cfg, processor: processor, readiness: readiness, log: log.With("component", "consumer")}
}

// Run consumes until ctx is cancelled, reconnecting with backoff whenever the
// broker connection is lost. It returns nil on a clean shutdown.
func (c *Consumer) Run(ctx context.Context) error {
	backoff := initialBackoff
	for {
		consuming, err := c.runSession(ctx)
		if ctx.Err() != nil {
			c.log.Info("consumer stopped")
			return nil
		}
		if consuming {
			backoff = initialBackoff
		}
		c.log.Error("consumer session ended, reconnecting", "error", err, "retry_in", backoff)

		select {
		case <-ctx.Done():
			return nil
		case <-time.After(backoff):
		}
		backoff = min(backoff*2, maxBackoff)
	}
}

// runSession runs one connection's lifetime. consuming reports whether it got
// as far as attaching to the queue (so the caller can reset its backoff).
func (c *Consumer) runSession(ctx context.Context) (consuming bool, err error) {
	c.log.Info("connecting to RabbitMQ", "broker", brokerAddress(c.cfg.RabbitMQURL))
	conn, err := amqp.Dial(c.cfg.RabbitMQURL)
	if err != nil {
		return false, fmt.Errorf("connect: %w", err)
	}
	defer conn.Close()
	c.log.Debug("connected to RabbitMQ")

	ch, err := conn.Channel()
	if err != nil {
		return false, fmt.Errorf("open channel: %w", err)
	}
	defer ch.Close()

	if err := declareTopology(ch, c.cfg); err != nil {
		return false, err
	}
	c.log.Debug("topology declared",
		"exchange", c.cfg.Exchange,
		"queue", c.cfg.Queue,
		"routing_key", c.cfg.RoutingKey,
		"delivery_limit", c.cfg.DeliveryLimit)

	if err := ch.Qos(c.cfg.Prefetch, 0, false); err != nil {
		return false, fmt.Errorf("set prefetch: %w", err)
	}

	tag := consumerTag()
	deliveries, err := ch.Consume(c.cfg.Queue, tag, false, false, false, false, nil)
	if err != nil {
		return false, fmt.Errorf("consume %s: %w", c.cfg.Queue, err)
	}

	connClosed := conn.NotifyClose(make(chan *amqp.Error, 1))

	c.readiness.SetReady(true)
	defer c.readiness.SetReady(false)
	c.log.Info("consuming", "queue", c.cfg.Queue, "prefetch", c.cfg.Prefetch)

	// On shutdown, cancel the consumer so the broker stops sending, then keep
	// reading until the deliveries channel closes so anything already
	// delivered is still processed and acked.
	stop := ctx.Done()
	for {
		select {
		case d, ok := <-deliveries:
			if !ok {
				if ctx.Err() != nil {
					return true, nil
				}
				return true, errors.New("deliveries channel closed")
			}
			c.handleDelivery(ctx, d)
		case amqpErr := <-connClosed:
			return true, fmt.Errorf("connection closed: %v", amqpErr)
		case <-stop:
			c.log.Info("shutdown requested, cancelling consumer and finishing in-flight messages")
			if err := ch.Cancel(tag, false); err != nil {
				return true, fmt.Errorf("cancel consumer: %w", err)
			}
			stop = nil
		}
	}
}

func (c *Consumer) handleDelivery(ctx context.Context, d amqp.Delivery) {
	c.log.Debug("delivery received",
		"delivery_tag", d.DeliveryTag,
		"redelivered", d.Redelivered,
		"routing_key", d.RoutingKey,
		"body_bytes", len(d.Body))

	// A message already in hand is finished even if shutdown has begun.
	result := c.process(context.WithoutCancel(ctx), d.Body)

	var err error
	switch result {
	case outcomeAck:
		err = d.Ack(false)
	case outcomeRequeue:
		err = d.Nack(false, true)
	case outcomeDeadLetter:
		err = d.Nack(false, false)
	}
	if err != nil {
		// The channel is gone; the broker redelivers the unacked message.
		c.log.Error("acknowledging delivery failed", "delivery_tag", d.DeliveryTag, "error", err)
		return
	}
	c.log.Debug("delivery settled", "delivery_tag", d.DeliveryTag, "outcome", result.String())
}

// process decodes body and runs the processor, deciding the delivery's fate.
func (c *Consumer) process(ctx context.Context, body []byte) outcome {
	var msg contracts.LogBatchMessage
	if err := json.Unmarshal(body, &msg); err != nil {
		// Redelivery cannot fix a message that does not match the contract.
		// The log-entry contents are deliberately never logged.
		c.log.Error("dropping malformed log batch to the dead-letter queue", "body_bytes", len(body), "error", err)
		return outcomeDeadLetter
	}

	log := c.log.With("message_id", msg.MessageId, "tenant_id", msg.TenantId)
	if err := c.processor.Process(ctx, msg); err != nil {
		log.Warn("processing log batch failed, requeueing", "error", err)
		return outcomeRequeue
	}

	log.Info("log batch consumed", "devices", len(msg.Devices))
	return outcomeAck
}

// String makes outcomes readable in log lines (slog prints an int-based type as a number, so callers pass result.String()).
func (o outcome) String() string {
	switch o {
	case outcomeAck:
		return "ack"
	case outcomeRequeue:
		return "requeue"
	case outcomeDeadLetter:
		return "dead-letter"
	}
	return "unknown"
}

// brokerAddress returns only host:port of the AMQP URL, so the credentials
// embedded in it never reach the logs.
func brokerAddress(rawURL string) string {
	u, err := url.Parse(rawURL)
	if err != nil {
		return "<unparseable>"
	}
	return u.Host
}

func consumerTag() string {
	host, err := os.Hostname()
	if err != nil {
		host = "unknown"
	}
	return "logs-processor-" + host
}
