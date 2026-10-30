window.mongoTuiMenu = {
    open(menu, trigger, x, y) {
        const anchor = trigger.getBoundingClientRect();
        const width = menu.offsetWidth;
        const height = menu.offsetHeight;
        menu.style.left = `${Math.max(8, Math.min(x ?? anchor.right - width, innerWidth - width - 8))}px`;
        menu.style.top = `${Math.max(8, Math.min(y ?? anchor.bottom + 4, innerHeight - height - 8))}px`;
        const items = [...menu.querySelectorAll('[role="menuitem"]')];
        items[0]?.focus();
        menu.onkeydown = event => {
            const index = items.indexOf(document.activeElement);
            let next;
            if (event.key === 'ArrowDown') next = (index + 1) % items.length;
            if (event.key === 'ArrowUp') next = (index - 1 + items.length) % items.length;
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = items.length - 1;
            if (next !== undefined) { event.preventDefault(); items[next]?.focus(); }
        };
    },
    focus(trigger) { trigger?.focus(); }
};
