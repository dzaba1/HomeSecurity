package health

import (
	"context"
	"net"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestHandlerReflectsReadiness(t *testing.T) {
	var r Readiness
	srv := httptest.NewServer(r.Handler())
	defer srv.Close()

	get := func() int {
		resp, err := http.Get(srv.URL + Path)
		if err != nil {
			t.Fatal(err)
		}
		defer resp.Body.Close()
		return resp.StatusCode
	}

	if got := get(); got != http.StatusServiceUnavailable {
		t.Errorf("before ready: got %d, want 503", got)
	}
	r.SetReady(true)
	if got := get(); got != http.StatusOK {
		t.Errorf("when ready: got %d, want 200", got)
	}
	r.SetReady(false)
	if got := get(); got != http.StatusServiceUnavailable {
		t.Errorf("after not ready: got %d, want 503", got)
	}
}

func TestProbe(t *testing.T) {
	var r Readiness
	srv := httptest.NewServer(r.Handler())
	defer srv.Close()
	addr := srv.Listener.Addr().(*net.TCPAddr).String()

	if err := Probe(context.Background(), addr); err == nil {
		t.Error("probe should fail while not ready")
	}
	r.SetReady(true)
	if err := Probe(context.Background(), addr); err != nil {
		t.Errorf("probe should pass when ready: %v", err)
	}
}
