// Installable app (PWA) helpers: service worker registration, reloading after a deployment,
// installing the app and push notifications.
var App = App || {};

if ('serviceWorker' in navigator) {
  // updateViaCache 'none': the browser never takes the worker script from the HTTP cache
  navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' }).catch(() => {
    // Not available (e.g. private mode) - the app works without it, only push notifications don't
  });
}

App.appUpdate = {
  storageKey: 'app-reloaded-for',

  // True if the page was already reloaded for this server version. Prevents a reload loop
  // if client and server keep reporting different versions.
  wasReloadedFor: function (serverVersion) {
    try {
      return sessionStorage.getItem(App.appUpdate.storageKey) === serverVersion;
    } catch {
      return true;
    }
  },

  // Loads the new version, optionally at another url of the app
  reload: function (serverVersion, url) {
    try {
      sessionStorage.setItem(App.appUpdate.storageKey, serverVersion);
    } catch {
      return;
    }
    if (url) {
      window.location.assign(url);
    } else {
      window.location.reload();
    }
  }
};

App.install = {
  promptEvent: null,

  isStandalone: function () {
    return window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
  },

  // iPadOS reports itself as a Mac, the touch points give it away
  isIos: function () {
    return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  },

  // 'installed' | 'prompt' (the browser can install it on request) | 'ios' (manual: share > add to home screen) | 'none'
  getState: function () {
    if (App.install.isStandalone()) {
      return 'installed';
    }
    if (App.install.promptEvent) {
      return 'prompt';
    }
    return App.install.isIos() ? 'ios' : 'none';
  },

  // Shows the browser's install dialog. Returns true if the app was installed.
  prompt: async function () {
    const promptEvent = App.install.promptEvent;
    if (!promptEvent) {
      return false;
    }
    App.install.promptEvent = null;
    promptEvent.prompt();
    const choice = await promptEvent.userChoice;
    return choice.outcome === 'accepted';
  }
};

// Chromium browsers hand out the install dialog through this event, it has to be kept for later
window.addEventListener('beforeinstallprompt', event => {
  event.preventDefault();
  App.install.promptEvent = event;
});

App.push = {
  // Remembers that the user switched notifications on, so a subscription the browser dropped can be restored
  enabledKey: 'push-enabled',

  isSupported: function () {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  },

  // Brave has the push service switched off by default (subscribing fails with "push service error")
  isBrave: function () {
    return !!navigator.brave;
  },

  // 'unsupported' | 'needs-install' (iOS only delivers to the installed app) | 'denied' | 'off' | 'on'
  getState: async function () {
    if (!App.push.isSupported()) {
      return App.install.isIos() && !App.install.isStandalone() ? 'needs-install' : 'unsupported';
    }
    if (Notification.permission === 'denied') {
      return 'denied';
    }
    const subscription = await App.push.getBrowserSubscription();
    return subscription && Notification.permission === 'granted' ? 'on' : 'off';
  },

  // Asks for permission and subscribes. Call it directly from a click, browsers only ask on a user gesture.
  // Returns the subscription for the API, or null if the user said no.
  subscribe: async function (publicKey) {
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') {
      return null;
    }
    const subscription = await App.push.ensureSubscription(publicKey);
    App.push.setEnabled(true);
    return App.push.toModel(subscription);
  },

  // On app start: returns the current subscription (re-created if the browser dropped it or the
  // server key changed) so it can be registered again, or null if notifications are off.
  restore: async function (publicKey) {
    if (!App.push.isSupported() || Notification.permission !== 'granted') {
      return null;
    }
    const existing = await App.push.getBrowserSubscription();
    if (!existing && !App.push.isEnabled()) {
      return null;
    }
    const subscription = await App.push.ensureSubscription(publicKey);
    App.push.setEnabled(true);
    return App.push.toModel(subscription);
  },

  // Returns the endpoint that was unsubscribed, or null
  unsubscribe: async function () {
    App.push.setEnabled(false);
    const subscription = await App.push.getBrowserSubscription();
    if (!subscription) {
      return null;
    }
    await subscription.unsubscribe();
    return subscription.endpoint;
  },

  getEndpoint: async function () {
    const subscription = await App.push.getBrowserSubscription();
    return subscription ? subscription.endpoint : null;
  },

  getBrowserSubscription: async function () {
    if (!App.push.isSupported()) {
      return null;
    }
    const registration = await navigator.serviceWorker.getRegistration();
    return registration ? registration.pushManager.getSubscription() : null;
  },

  ensureSubscription: async function (publicKey) {
    const registration = await App.push.getRegistration();
    const key = App.push.decodeKey(publicKey);

    let subscription = await registration.pushManager.getSubscription();
    if (subscription && !App.push.hasKey(subscription, key)) {
      // Subscribed with another server key: it would never receive anything
      await subscription.unsubscribe();
      subscription = null;
    }

    return subscription || App.push.withTimeout(
      registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key }), 20000, 'The push service did not answer.');
  },

  // The active service worker. Registers it again instead of only waiting for "ready": if the worker
  // can't be registered (e.g. an untrusted https certificate), "ready" would never resolve and the
  // caller would wait forever. register() fails with the reason instead.
  getRegistration: async function () {
    await navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
    return App.push.withTimeout(navigator.serviceWorker.ready, 10000, 'The service worker did not start.');
  },

  withTimeout: function (promise, milliseconds, message) {
    let timer;
    const timeout = new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(message)), milliseconds); });
    return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
  },

  hasKey: function (subscription, key) {
    const current = subscription.options && subscription.options.applicationServerKey;
    if (!current) {
      return true;
    }
    const bytes = new Uint8Array(current);
    return bytes.length === key.length && bytes.every((value, index) => value === key[index]);
  },

  toModel: function (subscription) {
    const json = subscription.toJSON();
    return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
  },

  // base64url -> bytes
  decodeKey: function (publicKey) {
    const base64 = (publicKey + '='.repeat((4 - publicKey.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
    return Uint8Array.from(atob(base64), character => character.charCodeAt(0));
  },

  isEnabled: function () {
    try {
      return localStorage.getItem(App.push.enabledKey) === '1';
    } catch {
      return false;
    }
  },

  setEnabled: function (enabled) {
    try {
      if (enabled) {
        localStorage.setItem(App.push.enabledKey, '1');
      } else {
        localStorage.removeItem(App.push.enabledKey);
      }
    } catch {
      // Storage blocked - a dropped subscription just isn't restored automatically
    }
  }
};
