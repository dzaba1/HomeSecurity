package consumer

import (
	"fmt"

	amqp "github.com/rabbitmq/amqp091-go"

	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/config"
)

// declareTopology idempotently declares everything this consumer needs. The
// producer (EasyNetQ) only declares the exchange, lazily on first publish, so
// the queue, its binding and the dead-letter setup are owned here:
//
//	<exchange> --routing key--> <queue> --(rejected / delivery limit)--> <queue>.dlx --> <queue>.dlq
//
// The queue is a durable quorum queue: it supports x-delivery-limit, which
// gives "retry a few times, then park it in the DLQ" (ADR-0007) without any
// retry code of our own.
func declareTopology(ch *amqp.Channel, cfg config.Config) error {
	dlx := cfg.Queue + ".dlx"
	dlq := cfg.Queue + ".dlq"

	// Must match how EasyNetQ declares it (durable topic), or the broker
	// rejects the redeclaration.
	if err := ch.ExchangeDeclare(cfg.Exchange, amqp.ExchangeTopic, true, false, false, false, nil); err != nil {
		return fmt.Errorf("declare exchange %s: %w", cfg.Exchange, err)
	}

	if err := ch.ExchangeDeclare(dlx, amqp.ExchangeDirect, true, false, false, false, nil); err != nil {
		return fmt.Errorf("declare exchange %s: %w", dlx, err)
	}
	if _, err := ch.QueueDeclare(dlq, true, false, false, false, amqp.Table{"x-queue-type": "quorum"}); err != nil {
		return fmt.Errorf("declare queue %s: %w", dlq, err)
	}
	if err := ch.QueueBind(dlq, dlq, dlx, false, nil); err != nil {
		return fmt.Errorf("bind queue %s: %w", dlq, err)
	}

	queueArgs := amqp.Table{
		"x-queue-type":              "quorum",
		"x-dead-letter-exchange":    dlx,
		"x-dead-letter-routing-key": dlq,
		"x-delivery-limit":          int64(cfg.DeliveryLimit),
	}
	if _, err := ch.QueueDeclare(cfg.Queue, true, false, false, false, queueArgs); err != nil {
		return fmt.Errorf("declare queue %s: %w", cfg.Queue, err)
	}
	if err := ch.QueueBind(cfg.Queue, cfg.RoutingKey, cfg.Exchange, false, nil); err != nil {
		return fmt.Errorf("bind queue %s: %w", cfg.Queue, err)
	}
	return nil
}
