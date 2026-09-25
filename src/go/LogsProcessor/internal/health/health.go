// Package health exposes a readiness endpoint and a probe for it, so the
// container can be health-checked without curl in the runtime image.
package health

import (
	"context"
	"fmt"
	"net"
	"net/http"
	"sync/atomic"
	"time"
)

// Path is the readiness endpoint, same as the .NET services'.
const Path = "/health/ready"

// Readiness reports whether the consumer is currently attached to the queue.
type Readiness struct {
	ready atomic.Bool
}

// SetReady records whether the consumer is consuming.
func (r *Readiness) SetReady(ready bool) {
	r.ready.Store(ready)
}

// Handler serves the readiness endpoint.
func (r *Readiness) Handler() http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("GET "+Path, func(w http.ResponseWriter, _ *http.Request) {
		if !r.ready.Load() {
			http.Error(w, "not ready", http.StatusServiceUnavailable)
			return
		}
		_, _ = w.Write([]byte("ready"))
	})
	return mux
}

// Probe calls the readiness endpoint of a service listening on addr (for
// example ":8080") and returns an error unless it answers 200.
func Probe(ctx context.Context, addr string) error {
	host, port, err := net.SplitHostPort(addr)
	if err != nil {
		return fmt.Errorf("invalid health address %q: %w", addr, err)
	}
	if host == "" {
		host = "localhost"
	}

	ctx, cancel := context.WithTimeout(ctx, 3*time.Second)
	defer cancel()

	req, err := http.NewRequestWithContext(ctx, http.MethodGet, "http://"+net.JoinHostPort(host, port)+Path, nil)
	if err != nil {
		return err
	}
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("health probe returned %s", resp.Status)
	}
	return nil
}
