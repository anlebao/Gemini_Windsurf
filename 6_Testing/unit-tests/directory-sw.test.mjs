// ============================================================================
// directory-sw.js — regression test for the /_blazor interception defect.
//
// Defect (2026-10-24): the catch-all static-asset branch intercepted
// GET /_blazor?id=... (SignalR ServerSentEvents fallback). cache.put on the
// infinite event-stream never settles; aborting the tee can kill the circuit
// → page frozen at last rendered frame ("Đang tải..." infinite spinner).
//
// Strategy: load the real SW file in a vm context with mocked service-worker
// globals (self.addEventListener, caches, fetch, Request/Response/FetchEvent)
// and assert which requests reach respondWith / caches.put.
//
// Run:  node --test 6_Testing/unit-tests/
// ============================================================================
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const SW_PATH = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../5_WebApps/Directory/wwwroot/directory-sw.js'
);

const ORIGIN = 'https://timlathay.com';

function makeEnv() {
  const listeners = {};
  const cachePuts = [];           // { url, contentType }
  const cacheEntries = new Map(); // url → canned response (simulates populated cache)
  const state = {
    listeners, cachePuts, cacheEntries,
    fetches: [],
    fetchResponse: null,          // Response the mock network returns
    makeResponse: (body, headers) => new MockResponse(body, { headers }),
  };

  class MockHeaders {
    constructor(map) { this._m = map || {}; }
    get(k) { return this._m[k.toLowerCase()] ?? null; }
  }
  class MockResponse {
    constructor(body, { status = 200, headers = {} } = {}) {
      this.status = status;
      this.type = 'basic';
      this.headers = new MockHeaders(
        Object.fromEntries(Object.entries(headers).map(([k, v]) => [k.toLowerCase(), v]))
      );
      this._body = body;
      this._headerMap = Object.fromEntries(Object.entries(headers).map(([k, v]) => [k.toLowerCase(), v]));
    }
    clone() { return new MockResponse(this._body, { status: this.status, headers: this._headerMap }); }
  }
  const mockCache = {
    put: async (req, res) => {
      cachePuts.push({ url: req.url, contentType: res.headers.get('Content-Type') });
    },
    match: async (req) => cacheEntries.get(req.url),
    add: async () => {},
    delete: async () => true,
    keys: async () => [...cacheEntries.keys()],
  };
  const sandbox = {
    console,
    URL,
    Promise,
    setTimeout: (fn) => fn(),
    caches: {
      open: async () => mockCache,
      match: mockCache.match,
      keys: async () => [state.cacheName || 'vanan-directory-v4'],
      delete: async () => true,
    },
    fetch: async (req) => {
      state.fetches.push(req.url);
      if (state.fetchResponse) return state.fetchResponse;
      return new MockResponse('', { headers: { 'Content-Type': 'text/css' } });
    },
    Response: MockResponse,
  };
  sandbox.self = sandbox;
  sandbox.addEventListener = (type, fn) => { listeners[type] = fn; };
  sandbox.self.location = { origin: ORIGIN };
  sandbox.self.skipWaiting = async () => {};
  sandbox.self.clients = { claim: async () => {} };
  vm.createContext(sandbox);
  vm.runInContext(readFileSync(SW_PATH, 'utf8'), sandbox, { filename: 'directory-sw.js' });
  return state;
}

function makeEvent(url, { method = 'GET', mode = 'same-origin' } = {}) {
  const event = {
    request: { method, mode, url },
    _responded: false,
    _promise: null,
    respondWith(p) { this._responded = true; this._promise = p; },
  };
  return event;
}

function fireFetch(state, event) {
  state.listeners.fetch(event);
  return event;
}

// --- never-intercept paths -------------------------------------------------
for (const [label, url, opts] of [
  ['SSE fallback GET /_blazor?id=', `${ORIGIN}/_blazor?id=abc123`],
  ['plain GET /_blazor', `${ORIGIN}/_blazor`],
  ['GET /_blazor/negotiate', `${ORIGIN}/_blazor/negotiate?negotiateVersion=1`],
  ['GET /_blazor/initializers', `${ORIGIN}/_blazor/initializers`],
  ['GET /_blazor/disconnect', `${ORIGIN}/_blazor/disconnect`],
  ['GET /manifest.json', `${ORIGIN}/manifest.json`],
  ['GET /directory-sw.js', `${ORIGIN}/directory-sw.js`],
  ['GET /khachlink-icons/x.png', `${ORIGIN}/khachlink-icons/x.png`],
  ['GET /service-worker-assets.js', `${ORIGIN}/service-worker-assets.js`],
]) {
  test(`SW does not intercept: ${label}`, async () => {
    const state = makeEnv();
    const ev = fireFetch(state, makeEvent(url, opts));
    assert.equal(ev._responded, false, 'respondWith must not be called — request passes through');
    assert.equal(state.cachePuts.length, 0, 'nothing must be written to the cache');
    assert.equal(state.fetches.length, 0, 'SW must not issue its own fetch for this request');
  });
}

test('POST /_blazor (LongPolling) is not intercepted', () => {
  const state = makeEnv();
  const ev = fireFetch(state, makeEvent(`${ORIGIN}/_blazor?id=x`, { method: 'POST' }));
  assert.equal(ev._responded, false);
  assert.equal(state.cachePuts.length, 0);
});

test('POST /_blazor/negotiate is not intercepted', () => {
  const state = makeEnv();
  const ev = fireFetch(state, makeEvent(`${ORIGIN}/_blazor/negotiate`, { method: 'POST' }));
  assert.equal(ev._responded, false);
});

test('cross-origin GET is not intercepted', () => {
  const state = makeEnv();
  const ev = fireFetch(state, makeEvent('https://api2.khachvip.online/x'));
  assert.equal(ev._responded, false);
});

// --- preserved behavior -----------------------------------------------------
test('navigation: network-first via respondWith, caches shell on 200', async () => {
  const state = makeEnv();
  const ev = fireFetch(state, makeEvent(`${ORIGIN}/`, { mode: 'navigate' }));
  assert.equal(ev._responded, true);
  await ev._promise;
  assert.deepEqual(state.fetches, [`${ORIGIN}/`], 'navigation must hit the network');
  assert.equal(state.cachePuts.length, 1, 'shell response cached for offline fallback');
});

test('static asset: cache-first miss → fetch → cached on 200 non-HTML', async () => {
  const state = makeEnv();
  const ev = fireFetch(state, makeEvent(`${ORIGIN}/css/site.css`));
  assert.equal(ev._responded, true);
  await ev._promise;
  assert.equal(state.cachePuts.length, 1);
  assert.equal(state.cachePuts[0].contentType, 'text/css');
});

test('static asset: cache hit serves without network fetch', async () => {
  const state = makeEnv();
  const canned = { status: 200, type: 'basic', headers: { get: () => 'text/css' } };
  state.cacheEntries.set(`${ORIGIN}/js/app.js`, canned);
  const ev = fireFetch(state, makeEvent(`${ORIGIN}/js/app.js`));
  assert.equal(ev._responded, true);
  const res = await ev._promise;
  assert.equal(res, canned);
  assert.equal(state.fetches.length, 0, 'cache hit must not hit network');
});

test('static asset: HTML response is never cached (icon-poisoning guard intact)', async () => {
  const state = makeEnv();
  state.fetchResponse = state.makeResponse('', { 'Content-Type': 'text/html; charset=utf-8' });
  const ev = fireFetch(state, makeEvent(`${ORIGIN}/assets/logo.png`));
  assert.equal(ev._responded, true);
  const res = await ev._promise;
  assert.equal(res.status, 200);
  assert.equal(state.cachePuts.length, 0, 'text/html must not enter the static cache');
});
