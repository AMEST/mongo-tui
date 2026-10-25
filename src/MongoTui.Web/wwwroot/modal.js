(() => {
  const dialogs = new Map();
  let nextId = 0;
  const focusable = element => [...element.querySelectorAll(
    'button:not(:disabled),input:not(:disabled),select:not(:disabled),textarea:not(:disabled),a[href],[tabindex]:not([tabindex="-1"]),[contenteditable="true"]'
  )].filter(item => item.getClientRects().length && getComputedStyle(item).visibility !== 'hidden' && !item.closest('[inert]'));
  const focus = element => (element.querySelector('[autofocus]:not(:disabled)') || focusable(element)[0] || element).focus({preventScroll: true});
  const activate = () => {
    const stack = [...dialogs.values()];
    stack.forEach(({element}, index) => {
      const backdrop = element.closest('.modal-backdrop');
      if (backdrop) {
        backdrop.inert = index !== stack.length - 1;
        backdrop.style.zIndex = String(100 + index * 2);
      }
    });
    return stack.at(-1)?.element;
  };
  window.mongoTuiModal = {
    open(element) {
      const previous = document.activeElement;
      const keydown = event => {
        if (event.key === 'Escape' && event.defaultPrevented) event.stopPropagation();
        if (event.key !== 'Tab') return;
        const items = focusable(element), first = items[0], last = items.at(-1);
        if (!items.length) { event.preventDefault(); element.focus(); }
        else if (event.shiftKey && (document.activeElement === first || !element.contains(document.activeElement))) {
          event.preventDefault(); last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !element.contains(document.activeElement))) {
          event.preventDefault(); first.focus();
        }
      };
      const id = ++nextId;
      dialogs.set(id, {element, previous, keydown});
      element.addEventListener('keydown', keydown);
      activate();
      focus(element);
      return id;
    },
    close(id) {
      const entry = dialogs.get(id);
      if (!entry) return;
      entry.element.removeEventListener('keydown', entry.keydown);
      dialogs.delete(id);
      const active = activate();
      if (entry.previous?.isConnected && !entry.previous.disabled && (!active || active.contains(entry.previous)))
        entry.previous.focus({preventScroll: true});
      else if (active) focus(active);
    }
  };
})();
