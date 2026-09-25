package processor

import (
	"context"
	"io"
	"log/slog"
	"testing"

	"github.com/dzaba1/HomeSecurity/src/go/logsprocessor/internal/contracts"
)

func TestProcessAcceptsBatch(t *testing.T) {
	log := slog.New(slog.NewTextHandler(io.Discard, nil))
	if err := New(log).Process(context.Background(), contracts.LogBatchMessage{}); err != nil {
		t.Fatalf("Process must not fail while it is a no-op: %v", err)
	}
}
