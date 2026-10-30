(() => {
  'use strict';
  const storageKey = 'mongotui.web.session';
  const messages = document.getElementById('auth-messages')?.dataset;
  const read = () => { try { return sessionStorage.getItem(storageKey); } catch { return null; } };
  const save = token => sessionStorage.setItem(storageKey, token);
  const clear = () => { try { sessionStorage.removeItem(storageKey); } catch {} };
  const request = (path, token, body) => fetch(path, {
    method: body === undefined ? 'GET' : 'POST', credentials: 'omit', cache: 'no-store', redirect: 'error',
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) })
  });
  const login = reason => { clear(); location.replace(`/login${reason ? `?reason=${reason}` : ''}`); };
  const showError = key => {
    const element = document.getElementById('auth-error');
    if (element) { element.textContent = messages?.[key] || messages?.unavailable || ''; element.hidden = false; }
  };
  const exchange = async (path, body, failure) => {
    const response = await request(path, null, body);
    if (!response.ok) { showError(response.status === 429 ? 'rate' : response.status === 503 ? 'unavailable' : failure); return false; }
    save((await response.json()).token);
    return true;
  };
  window.mongoTuiAuth = {
    async logout() {
      try { await request('/auth/logout', read(), {}); } finally { login(''); }
    },
    replace(token) { save(token); location.replace('/'); }
  };
  const form = document.getElementById('web-sign-in');
  form?.addEventListener('submit', async event => {
    event.preventDefault();
    const input = form.querySelector('input[type=password]'), button = form.querySelector('button[type=submit]');
    if (button.disabled) return;
    const password = input.value;
    input.value = ''; input.disabled = button.disabled = true;
    try { if (await exchange('/auth/password', { password }, 'password')) location.replace('/'); }
    catch { showError('unavailable'); }
    finally { input.disabled = button.disabled = false; input.focus(); }
  });
  async function start() {
    const launch = new URLSearchParams(location.hash.slice(1)).get('launch');
    if (launch !== null) {
      history.replaceState(null, '', location.pathname + location.search);
      clear();
      try {
        if (!await exchange('/auth/launch', { launch }, 'launch')) {
          if (!messages) location.replace('/login?reason=launch');
          return;
        }
      } catch { if (messages) showError('unavailable'); else location.replace('/login?reason=unavailable'); return; }
    }
    if (messages) {
      if (launch !== null) { location.replace('/'); return; }
      const reason = new URLSearchParams(location.search).get('reason');
      if (['expired', 'launch', 'unavailable'].includes(reason)) showError(reason);
      return;
    }
    const token = read();
    if (!token) { login(''); return; }
    const validate = async () => {
      try { if ((await request('/auth/session', token)).status === 401) { login('expired'); return false; } }
      catch { }
      return true;
    };
    if (!await validate()) return;
    document.getElementById('components-reconnect-modal')?.addEventListener('components-reconnect-state-changed', () => void validate());
    document.addEventListener('visibilitychange', () => { if (!document.hidden) void validate(); });
    setInterval(() => { if (!document.hidden) void validate(); }, 60_000);
    try {
      await Blazor.start({ circuit: { configureSignalR: builder => builder.withUrl('/_blazor', {
        accessTokenFactory: () => token, withCredentials: false
      }) } });
    } catch { if (await validate()) location.replace('/login?reason=unavailable'); }
  }
  void start();
})();
