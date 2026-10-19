// Basera service worker (ARCHITECTURE §9): caches static assets only, never pages or data.
// Pages and /api always come from the network, so nobody sees a stale balance or another user's page.
// Bump VERSION when the asset list changes; old caches are deleted on activate.
"use strict";

const VERSION = "basera-static-v1";
const PRECACHE = [
    "/lib/bootstrap/css/bootstrap.min.css",
    "/lib/bootstrap/js/bootstrap.bundle.min.js",
    "/lib/htmx/htmx.min.js",
    "/icons/icon-192.png",
    "/manifest.webmanifest",
];
const STATIC_PREFIXES = ["/lib/", "/css/", "/js/", "/icons/"];

self.addEventListener("install", (event) => {
    event.waitUntil(caches.open(VERSION).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(keys.filter((key) => key !== VERSION).map((key) => caches.delete(key))))
            .then(() => self.clients.claim()));
});

self.addEventListener("fetch", (event) => {
    const url = new URL(event.request.url);
    const isStatic = event.request.method === "GET"
        && url.origin === self.location.origin
        && STATIC_PREFIXES.some((prefix) => url.pathname.startsWith(prefix));
    if (!isStatic) {
        return; // the browser handles it: network only
    }

    // Cache first. site.css/site.js carry ?v=hash (asp-append-version), so a new build is a new URL.
    event.respondWith(
        caches.match(event.request).then((cached) => cached || fetch(event.request).then((response) => {
            if (response.ok) {
                const copy = response.clone();
                caches.open(VERSION).then((cache) => cache.put(event.request, copy));
            }
            return response;
        })));
});
