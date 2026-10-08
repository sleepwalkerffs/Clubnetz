// Push notifications only. This worker deliberately has NO fetch handler and never writes to
// Cache Storage, so every request goes to the network and the HTTP cache headers of the server
// stay the single source of truth. Do not add offline caching here: a cached app shell is what
// made deployments show up late in the past.

// Take over right away. Safe because the worker serves no assets, so there is nothing an
// old page could get mixed up with.
self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', event => {
  event.waitUntil(
    // Removes whatever an earlier (caching) worker left behind
    caches.keys()
      .then(keys => Promise.all(keys.map(key => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

// Payload: { title, body, url, tag } (PushNotification in the API)
self.addEventListener('push', event => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    // Not JSON - still show something, browsers revoke subscriptions that receive pushes silently
  }

  event.waitUntil(
    self.registration.showNotification(data.title || 'Clubnetz', {
      body: data.body || '',
      icon: 'icons/icon-192.png',
      badge: 'icons/badge-96.png',
      tag: data.tag || undefined,
      data: { url: data.url || '/' }
    })
  );
});

self.addEventListener('notificationclick', event => {
  event.notification.close();

  // Only ever open pages of this app
  const target = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin);
  const url = target.origin === self.location.origin ? target.href : self.location.origin + '/';

  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const existing = windows.find(client => 'focus' in client);

    if (existing) {
      await existing.focus();
      if ('navigate' in existing) {
        try {
          await existing.navigate(url);
          return;
        } catch {
          // Not controlled by this worker yet - open a new window instead
        }
      }
    }

    await self.clients.openWindow(url);
  })());
});
