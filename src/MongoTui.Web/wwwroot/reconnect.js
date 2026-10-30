(() => {
  const modal = document.getElementById('components-reconnect-modal');
  if (!modal) return;

  modal.addEventListener('components-reconnect-state-changed', event => {
    if (event.detail.state === 'hide') {
      modal.close();
    } else if (!modal.open) {
      modal.showModal();
    }
  });
  modal.addEventListener('cancel', event => event.preventDefault());
  modal.querySelector('[data-reconnect-reload]').addEventListener('click', () => location.reload());
})();
