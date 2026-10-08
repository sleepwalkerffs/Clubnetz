// Loaded in <head> so the stored light/dark choice is applied before Blazor starts (no light flash).
// The mode ('system' | 'light' | 'dark') is managed by ThemeService.
var App = App || {};
App.theme = {
  storageKey: 'bookennis-theme',

  getMode: function () {
    try {
      return localStorage.getItem(App.theme.storageKey) || 'system';
    } catch {
      return 'system';
    }
  },

  setMode: function (mode) {
    try {
      localStorage.setItem(App.theme.storageKey, mode);
    } catch {
      // Storage blocked (e.g. private mode) - the choice only lasts for this session
    }
  },

  apply: function (isDark) {
    document.documentElement.dataset.theme = isDark ? 'dark' : 'light';
    document.documentElement.style.colorScheme = isDark ? 'dark' : 'light';
  }
};

(function () {
  const mode = App.theme.getMode();
  const isDark = mode === 'dark' || (mode === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  App.theme.apply(isDark);
})();
