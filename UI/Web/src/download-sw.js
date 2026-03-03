// Kavita Download Service Worker
// Handles Background Fetch API events for background downloads

self.addEventListener('backgroundfetchsuccess', (event) => {
  event.waitUntil((async () => {
    const clients = await self.clients.matchAll({ includeUncontrolled: true });
    clients.forEach(client => {
      client.postMessage({ type: 'download-complete', id: event.registration.id });
    });
  })());
});

self.addEventListener('backgroundfetchfail', (event) => {
  event.waitUntil((async () => {
    const clients = await self.clients.matchAll({ includeUncontrolled: true });
    clients.forEach(client => {
      client.postMessage({ type: 'download-failed', id: event.registration.id, error: 'Background fetch failed' });
    });
  })());
});

self.addEventListener('backgroundfetchclick', (event) => {
  event.waitUntil(clients.openWindow('/'));
});
