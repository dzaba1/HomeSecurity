package consumer

import (
	"context"
	"errors"
	"io"
	"log/slog"
	"testing"

	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/contracts"
)

type fakeProcessor struct {
	err      error
	received []contracts.LogBatchMessage
}

func (f *fakeProcessor) Process(_ context.Context, msg contracts.LogBatchMessage) error {
	f.received = append(f.received, msg)
	return f.err
}

func newTestConsumer(p Processor) *Consumer {
	return &Consumer{processor: p, log: slog.New(slog.NewTextHandler(io.Discard, nil))}
}

// Same shape the C# producer serializes (camelCase, see log_batch_message.json).
const validBatch = `{
  "messageId": "6f1c3a52-0b1e-4c1c-9d55-1a2b3c4d5e6f",
  "tenantId": "0d3f3b7e-1a2b-4c3d-8e9f-0a1b2c3d4e5f",
  "deviceId": "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d",
  "devices": [
    {
      "device": { "macAddress": "AA:BB:CC:DD:EE:FF", "ipAddress": "192.168.1.10", "hostname": "laptop" },
      "events": [
        { "eventId": "11111111-2222-4333-8444-555555555555", "level": "info", "message": "connected", "timestamp": "2026-01-02T03:04:05Z" }
      ]
    }
  ]
}`

func TestProcessAcksAndForwardsValidBatch(t *testing.T) {
	p := &fakeProcessor{}

	got := newTestConsumer(p).process(context.Background(), []byte(validBatch))

	if got != outcomeAck {
		t.Fatalf("outcome = %v, want ack", got)
	}
	if len(p.received) != 1 {
		t.Fatalf("processor called %d times, want 1", len(p.received))
	}
	msg := p.received[0]
	if msg.MessageId != "6f1c3a52-0b1e-4c1c-9d55-1a2b3c4d5e6f" || len(msg.Devices) != 1 || len(msg.Devices[0].Events) != 1 {
		t.Errorf("message not decoded as expected: %+v", msg)
	}
}

func TestProcessRequeuesWhenProcessorFails(t *testing.T) {
	p := &fakeProcessor{err: errors.New("boom")}

	if got := newTestConsumer(p).process(context.Background(), []byte(validBatch)); got != outcomeRequeue {
		t.Fatalf("outcome = %v, want requeue", got)
	}
}

func TestProcessDeadLettersMalformedBodies(t *testing.T) {
	cases := map[string]string{
		"not json":               `not json`,
		"missing required field": `{"messageId":"6f1c3a52-0b1e-4c1c-9d55-1a2b3c4d5e6f"}`,
		"no devices":             `{"messageId":"a","tenantId":"b","deviceId":"c","devices":[]}`,
	}
	for name, body := range cases {
		t.Run(name, func(t *testing.T) {
			p := &fakeProcessor{}

			if got := newTestConsumer(p).process(context.Background(), []byte(body)); got != outcomeDeadLetter {
				t.Fatalf("outcome = %v, want dead-letter", got)
			}
			if len(p.received) != 0 {
				t.Error("processor must not see a malformed message")
			}
		})
	}
}
