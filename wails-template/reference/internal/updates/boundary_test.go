package updates

// Public-boundary tests: update information and installers fetched from the
// distribution source are untrusted input (docs/architecture-wails.md#updates).
// Every rejection must leave the earlier verified state and the staging area as
// they were; only the progress phase reports the failure.

import (
	"bytes"
	"context"
	"crypto/ed25519"
	"encoding/base64"
	"encoding/json"
	"errors"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"reflect"
	"testing"

	"wailstemplate/internal/appstate"
	"wailstemplate/internal/fault"
)

type snapshot struct {
	status   Status
	manifest *Manifest
	staged   string
	cache    []string
}

func takeSnapshot(t *testing.T, s *Service) snapshot {
	t.Helper()
	var cache []string
	err := filepath.WalkDir(s.cfg.CacheDir, func(path string, _ os.DirEntry, err error) error {
		if err != nil {
			return err
		}
		rel, err := filepath.Rel(s.cfg.CacheDir, path)
		cache = append(cache, rel)
		return err
	})
	if err != nil && !errors.Is(err, os.ErrNotExist) {
		t.Fatal(err)
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	return snapshot{status: s.status, manifest: s.manifest, staged: s.staged, cache: cache}
}
func assertRejectedWithoutChange(t *testing.T, s *Service, before snapshot, err error, code string) {
	t.Helper()
	var public *fault.Error
	if !errors.As(err, &public) || public.Code != code {
		t.Fatalf("want rejection %s, got %v", code, err)
	}
	after := takeSnapshot(t, s)
	if after.status.Phase != "failed" {
		t.Fatalf("failure not reported: %#v", after.status)
	}
	after.status.Phase, after.status.Downloaded, after.status.Total = before.status.Phase, before.status.Downloaded, before.status.Total
	if !reflect.DeepEqual(after, before) {
		t.Fatalf("state changed on rejection\nbefore %#v\nafter  %#v", before, after)
	}
	if before.staged != "" {
		if _, err := os.Stat(before.staged); err != nil {
			t.Fatalf("verified installer removed by rejection: %v", err)
		}
	}
}

// staged returns a service whose earlier check and download succeeded, so a
// later rejection can be shown not to invalidate the verified installer.
func staged(t *testing.T) (Config, ed25519.PrivateKey, Manifest, *Service) {
	t.Helper()
	cfg, key, m := fixture(t, "0.2.0")
	s := testUpdater(cfg, &appstate.State{}, nil, func() {})
	if _, err := s.Check(context.Background()); err != nil {
		t.Fatal(err)
	}
	if _, err := s.Download(context.Background()); err != nil {
		t.Fatal(err)
	}
	return cfg, key, m, s
}

// signRaw signs a payload without Sign's validation, as a hostile or broken
// release process could.
func signRaw(t *testing.T, key ed25519.PrivateKey, payload any) []byte {
	t.Helper()
	p, err := json.Marshal(payload)
	if err != nil {
		t.Fatal(err)
	}
	b, err := json.Marshal(Envelope{Payload: base64.StdEncoding.EncodeToString(p), Signature: base64.StdEncoding.EncodeToString(ed25519.Sign(key, p))})
	if err != nil {
		t.Fatal(err)
	}
	return b
}
func TestRejectedManifestKeepsVerifiedInstaller(t *testing.T) {
	_, key, m := fixture(t, "0.2.0")
	unsigned, err := json.Marshal(m)
	if err != nil {
		t.Fatal(err)
	}
	padded, err := Sign(m, key)
	if err != nil {
		t.Fatal(err)
	}
	padded = append(padded, bytes.Repeat([]byte(" "), maxManifestSize)...)
	otherOS, oversizeInstaller := m, m
	otherOS.OS = "linux"
	oversizeInstaller.Size = maxInstallerSize + 1
	for _, tc := range []struct {
		name     string
		manifest []byte
	}{
		{"unsigned envelope", []byte(`{"payload":"` + base64.StdEncoding.EncodeToString(unsigned) + `","signature":""}`)},
		{"bare manifest without envelope", unsigned},
		{"broken JSON", []byte(`{"payload":`)},
		{"manifest over size limit", padded},
		{"signed manifest for other OS", signRaw(t, key, otherOS)},
		{"signed manifest declaring installer over size limit", signRaw(t, key, oversizeInstaller)},
	} {
		t.Run(tc.name, func(t *testing.T) {
			cfg, _, _, s := staged(t)
			if err := os.WriteFile(filepath.Join(cfg.Source, ManifestName), tc.manifest, 0600); err != nil {
				t.Fatal(err)
			}
			before := takeSnapshot(t, s)
			_, err := s.Check(context.Background())
			assertRejectedWithoutChange(t, s, before, err, "UPDATE_UNTRUSTED")
		})
	}
}
func TestRejectedOtherCPUArchitectureKeepsVerifiedInstaller(t *testing.T) {
	cfg, key, m, s := staged(t)
	m.Arch = "arm64"
	b, err := Sign(m, key)
	if err != nil {
		t.Fatal(err)
	}
	if err = os.WriteFile(filepath.Join(cfg.Source, ManifestName), b, 0600); err != nil {
		t.Fatal(err)
	}
	before := takeSnapshot(t, s)
	_, err = s.Check(context.Background())
	assertRejectedWithoutChange(t, s, before, err, "UPDATE_TARGET")
}
func TestInstallerLargerThanDeclaredLeavesStageEmpty(t *testing.T) {
	cfg, _, m := fixture(t, "0.2.0")
	s := testUpdater(cfg, &appstate.State{}, nil, func() {})
	if _, err := s.Check(context.Background()); err != nil {
		t.Fatal(err)
	}
	data, err := os.ReadFile(filepath.Join(cfg.Source, m.Filename))
	if err != nil {
		t.Fatal(err)
	}
	if err = os.WriteFile(filepath.Join(cfg.Source, m.Filename), append(data, "appended"...), 0600); err != nil {
		t.Fatal(err)
	}
	before := takeSnapshot(t, s)
	_, err = s.Download(context.Background())
	assertRejectedWithoutChange(t, s, before, err, "UPDATE_UNTRUSTED")
}

// httpsUpdater keeps the production redirect policy and trusts only the test
// server's certificate.
func httpsUpdater(t *testing.T, cfg Config, srv *httptest.Server) *Service {
	t.Helper()
	cfg.Source = srv.URL + "/" + ManifestName
	s := testUpdater(cfg, &appstate.State{}, nil, func() {})
	s.client.Transport = srv.Client().Transport
	return s
}
func TestHTTPSSourceAcceptsVerifiedRelease(t *testing.T) {
	cfg, _, _ := fixture(t, "0.2.0")
	srv := httptest.NewTLSServer(http.FileServer(http.Dir(cfg.Source)))
	defer srv.Close()
	s := httpsUpdater(t, cfg, srv)
	if status, err := s.Check(context.Background()); err != nil || !status.Available {
		t.Fatalf("check %#v %v", status, err)
	}
	if status, err := s.Download(context.Background()); err != nil || status.Phase != "ready" {
		t.Fatalf("download %#v %v", status, err)
	}
}
func TestHTTPSSourceRejectsNon200Manifest(t *testing.T) {
	cfg, _, _ := fixture(t, "0.2.0")
	srv := httptest.NewTLSServer(http.NotFoundHandler())
	defer srv.Close()
	s := httpsUpdater(t, cfg, srv)
	before := takeSnapshot(t, s)
	_, err := s.Check(context.Background())
	assertRejectedWithoutChange(t, s, before, err, "INTERNAL")
}
func TestHTTPSSourceRejectsNon200InstallerAndLeavesStageEmpty(t *testing.T) {
	cfg, _, _ := fixture(t, "0.2.0")
	files := http.FileServer(http.Dir(cfg.Source))
	srv := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/"+ManifestName {
			http.Error(w, "unavailable", http.StatusServiceUnavailable)
			return
		}
		files.ServeHTTP(w, r)
	}))
	defer srv.Close()
	s := httpsUpdater(t, cfg, srv)
	if _, err := s.Check(context.Background()); err != nil {
		t.Fatal(err)
	}
	before := takeSnapshot(t, s)
	_, err := s.Download(context.Background())
	assertRejectedWithoutChange(t, s, before, err, "INTERNAL")
}
func TestHTTPSSourceRejectsRedirectToPlainHTTP(t *testing.T) {
	cfg, _, _ := fixture(t, "0.2.0")
	plainHit := false
	plain := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		plainHit = true
		http.ServeFile(w, r, filepath.Join(cfg.Source, ManifestName))
	}))
	defer plain.Close()
	srv := httptest.NewTLSServer(http.RedirectHandler(plain.URL+"/"+ManifestName, http.StatusFound))
	defer srv.Close()
	s := httpsUpdater(t, cfg, srv)
	before := takeSnapshot(t, s)
	_, err := s.Check(context.Background())
	assertRejectedWithoutChange(t, s, before, err, "INTERNAL")
	if plainHit {
		t.Fatal("redirect to plain HTTP was followed")
	}
}
func TestHTTPSSourceRejectsUnreachableSource(t *testing.T) {
	cfg, _, _ := fixture(t, "0.2.0")
	srv := httptest.NewTLSServer(http.NotFoundHandler())
	s := httpsUpdater(t, cfg, srv)
	srv.Close()
	before := takeSnapshot(t, s)
	_, err := s.Check(context.Background())
	assertRejectedWithoutChange(t, s, before, err, "INTERNAL")
}
