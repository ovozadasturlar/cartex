const APP_CACHE = 'cartex-app-v2';
const WASM_CACHE = 'cartex-wasm-v1';

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(
                keys.filter(k => k !== APP_CACHE && k !== WASM_CACHE).map(k => caches.delete(k))
            ))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    const url = new URL(event.request.url);
    const isFramework = url.pathname.includes('/_framework/') ||
        url.pathname.endsWith('.wasm') ||
        url.pathname.endsWith('.dll') ||
        url.pathname.endsWith('.blat') ||
        url.pathname.endsWith('.dat');

    if (isFramework) {
        event.respondWith(
            caches.open(WASM_CACHE).then(cache =>
                cache.match(event.request).then(cached => {
                    if (cached) return cached;
                    return fetch(event.request).then(response => {
                        if (response.ok) cache.put(event.request, response.clone());
                        return response;
                    });
                })
            )
        );
        return;
    }

    event.respondWith(
        fetch(event.request)
            .then(response => {
                if (response.ok) {
                    caches.open(APP_CACHE).then(cache => cache.put(event.request, response.clone()));
                }
                return response;
            })
            .catch(() => caches.match(event.request))
    );
});
