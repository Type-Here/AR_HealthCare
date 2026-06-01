/* Chat page — AI chat only */

document.addEventListener('DOMContentLoaded', () => {
  Nav.render('chat');

  const ta    = document.getElementById('chat-log');
  const badge = document.getElementById('chat-status');

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

  document.getElementById('btn-clear').addEventListener('click', () => {
    ta.value = '';
  });
});

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
