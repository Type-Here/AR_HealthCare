/* Monitor page — video feed + AI chat */

document.addEventListener('DOMContentLoaded', () => {
  Nav.render('monitor');
  initVideo();
  initAiChat('chat-log', 'chat-status');
  document.getElementById('btn-clear').addEventListener('click', () => {
    document.getElementById('chat-log').value = '';
  });
});

// ── Video ─────────────────────────────────────────────────────────────────────

function initVideo() {
  const img        = document.getElementById('video-feed');
  const badge      = document.getElementById('video-status');
  const noSignal   = document.getElementById('no-signal-overlay');

  function setNoSignal(on) {
    noSignal.hidden = !on;
    img.style.display = on ? 'none' : 'block';
    badge.textContent = on ? 'No signal' : 'LIVE';
    badge.className   = 'status-badge ' + (on ? 'offline' : 'online');
  }

  img.onload  = () => setNoSignal(false);
  img.onerror = () => {
    setNoSignal(true);
    setTimeout(() => { img.src = '/stream/video?' + Date.now(); }, 3000);
  };

  setNoSignal(false);
  img.src = '/stream/video?' + Date.now();
}

// ── AI chat ───────────────────────────────────────────────────────────────────

function initAiChat(logId, statusId) {
  const ta     = document.getElementById(logId);
  const badge  = document.getElementById(statusId);

  function connect() {
    const es = new EventSource('/stream/ai');

    es.onopen = () => {
      badge.textContent = '● Connesso';
      badge.className   = 'status-badge online';
    };

    es.onmessage = (e) => {
      handleAiEvent(JSON.parse(e.data), ta);
    };

    es.onerror = () => {
      badge.textContent = '● Disconnesso';
      badge.className   = 'status-badge offline';
      es.close();
      setTimeout(connect, 3000);
    };
  }

  connect();
}

function handleAiEvent(data, ta) {
  const ts = new Date().toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });

  if (data.type === 'prompt') {
    ta.value += (ta.value ? '\n' : '') +
      `[${ts}] PROMPT   ${data.text}\n[${ts}] OLLAMA ▶ `;
  } else if (data.type === 'token') {
    ta.value += data.text;
  } else if (data.type === 'done') {
    ta.value += '\n' + '─'.repeat(50) + '\n';
  } else if (data.type === 'error') {
    ta.value += `\n[ERRORE] ${data.text}\n`;
  }

  ta.scrollTop = ta.scrollHeight;
}
