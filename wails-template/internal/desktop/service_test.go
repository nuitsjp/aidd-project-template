package desktop

import (
	"testing"
	"time"
)

func TestQuitHandshake(t *testing.T) {
	notices := 0
	done := make(chan struct{}, 1)
	c := &Controls{Emit: func(string, any) { notices++ }, Quit: func() { done <- struct{}{} }}
	if !c.ShouldQuit() {
		t.Fatal("quit blocked before UI ready")
	}
	c.Ready.Store(true)
	if c.ShouldQuit() || notices != 1 {
		t.Fatal("did not ask UI")
	}
	c.ApproveQuit()
	if !c.ShouldQuit() {
		t.Fatal("approved quit blocked")
	}
	select {
	case <-done:
	case <-time.After(time.Second):
		t.Fatal("quit not called")
	}
}
