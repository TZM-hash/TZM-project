const CACHE_NAME = 'engineering-manager-shell-v2';
const SHELL = ['/css/base.css', '/css/components.css', '/css/pages.css', '/css/themes.css', '/js/site.js', '/js/offline-stage-results.js', '/manifest.webmanifest'];
const SENSITIVE_PREFIXES = ['/api/', '/Finance', '/Payroll', '/EmployeeLedger', '/DataExchange', '/Backups', '/Reminders', '/Projects/Contracts'];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
    .then(() => self.clients.claim()));
});

async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) return cached;
  const response = await fetch(request);
  if (response.ok) (await caches.open(CACHE_NAME)).put(request, response.clone());
  return response;
}

async function networkFirst(request, allowPrivateOfflineCache) {
  try {
    const response = await fetch(request);
    if (allowPrivateOfflineCache && response.ok) (await caches.open(CACHE_NAME)).put(request, response.clone());
    return response;
  } catch (error) {
    const cached = allowPrivateOfflineCache ? await caches.match(request) : null;
    if (cached) return cached;
    throw error;
  }
}

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  if (SHELL.includes(url.pathname)) {
    event.respondWith(cacheFirst(request));
    return;
  }

  if (url.pathname === '/StageResults/Offline') {
    event.respondWith(networkFirst(request, true));
    return;
  }

  if (SENSITIVE_PREFIXES.some(prefix => url.pathname.startsWith(prefix))) {
    event.respondWith(fetch(request));
    return;
  }

  if (request.mode === 'navigate') event.respondWith(networkFirst(request, false));
});
