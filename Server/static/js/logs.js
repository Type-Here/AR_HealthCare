/* Logs page — server activity + AI history */

document.addEventListener('DOMContentLoaded', () => {
  Nav.render('logs');

  const container = document.getElementById('log-entries');
  const badge     = document.getElementById('log-status');

  container.innerHTML = '<div class="logs-empty">In attesa di eventi…</div>';

  function connect() {
    const es = new EventSource('/stream/logs');

    es.onopen = () => {
      badge.textContent = '● Connesso';
      badge.className   = 'status-badge online';
    };

    es.onmessage = (e) => {
      const data = JSON.parse(e.data);
      appendLogEntry(container, data);
    };

    es.onerror = () => {
      badge.textContent = '● Disconnesso';
      badge.className   = 'status-badge offline';
      es.close();
      setTimeout(connect, 3000);
    };
  }

  connect();

  document.getElementById('btn-clear-logs').addEventListener('click', () => {
    container.innerHTML = '<div class="logs-empty">Log pulito.</div>';
  });
});

function appendLogEntry(container, data) {
  const empty = container.querySelector('.logs-empty');
  if (empty) empty.remove();

  const el = document.createElement('div');
  el.className = `log-entry log-${esc(data.level)}`;
  el.innerHTML =
    `<span class="log-ts">${esc(data.ts)}</span>` +
    `<span class="log-level">${esc(data.level)}</span>` +
    `<span class="log-msg">${esc(data.message)}</span>`;

  container.appendChild(el);
  container.scrollTop = container.scrollHeight;
}

function esc(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}
