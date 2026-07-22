const CACHE_NAME = 'engineering-manager-shell-v8';
const SHELL = [
  '/css/base.css', '/css/components.css', '/css/pages.css', '/css/themes.css',
  '/js/site.js', '/js/core/shell.js', '/js/core/effects.js', '/js/pages/settings.js',
  '/js/components/data-table.js', '/js/components/saved-views.js', '/js/components/filter-drawer.js', '/js/components/charts.js', '/js/components/quick-edit.js',
  '/js/offline-stage-results.js', '/js/offline-equipment.js', '/img/icons.svg', '/manifest.webmanifest'
];
const SENSITIVE_PREFIXES = ['/api/', '/Finance', '/Payroll', '/EmployeeLedger', '/Employees', '/Crews', '/DataExchange', '/Backups', '/Reminders', '/Projects/Contracts', '/Equipment/Settlement'];

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
    event.respondWith(networkFirst(request, true));
    return;
  }

  if (url.pathname === '/StageResults/Offline' || url.pathname === '/Equipment/Offline') {
    event.respondWith(networkFirst(request, true));
    return;
  }

  if (SENSITIVE_PREFIXES.some(prefix => url.pathname.startsWith(prefix))) {
    event.respondWith(fetch(request));
    return;
  }

  if (request.mode === 'navigate') event.respondWith(networkFirst(request, false));
});
