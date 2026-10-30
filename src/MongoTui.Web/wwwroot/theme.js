(() => {
  const storageKey = 'mongotui.theme';
  const themes = Object.freeze({ auto: 'auto', light: 'light', dark: 'dark' });
  const jsonContentType = 'application/json';
  const downloadUrlLifetimeMs = 1000;
  const preference = () => { try { return localStorage.getItem(storageKey) || themes.auto; } catch { return themes.auto; } };
  const apply = value => {
    document.documentElement.dataset.theme = value === themes.auto ? (matchMedia('(prefers-color-scheme: dark)').matches ? themes.dark : themes.light) : value;
  };
  window.mongoTuiTheme = {
    set(value) { try { localStorage.setItem(storageKey, value); } catch {} apply(value); },
    toggle() { this.set(document.documentElement.dataset.theme === themes.dark ? themes.light : themes.dark); },
    language(value) { document.documentElement.lang = value; }
  };
  apply(preference());
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => apply(preference()));
  const download = (name, blob) => {
    const url = URL.createObjectURL(blob), link = document.createElement('a');
    link.href = url; link.download = name; link.click();
    setTimeout(() => URL.revokeObjectURL(url), downloadUrlLifetimeMs);
  };
  window.mongoTuiFiles = {
    downloadText(name, text) { download(name, new Blob([text], {type: jsonContentType})); },
    async downloadStream(name, stream) { download(name, new Blob([await stream.arrayBuffer()], {type: jsonContentType})); },
    copy(text) { return navigator.clipboard.writeText(text); }
  };
})();
