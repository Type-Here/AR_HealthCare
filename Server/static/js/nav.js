/* Shared navigation — call Nav.render('page-id') at the top of each page */

const Nav = (() => {
  const PAGES = [
    { id: 'admin',   label: 'Pazienti', href: '/admin'   },
    { id: 'monitor', label: 'Monitor',  href: '/monitor' },
    { id: 'chat',    label: 'Chat AI',  href: '/chat'    },
    { id: 'logs',    label: 'Log',      href: '/logs'    },
  ];

  function render(activePage) {
    const links = PAGES.map(p =>
      `<a href="${p.href}" class="nav-link${p.id === activePage ? ' active' : ''}">${p.label}</a>`
    ).join('');

    document.getElementById('nav-root').innerHTML =
      `<header>
        <span class="nav-brand">&#9672; HoloMed</span>
        <nav class="nav-links">${links}</nav>
        <span id="server-status" class="server-status">Caricamento…</span>
      </header>`;

    fetch('/health')
      .then(r => _setStatus(r.ok))
      .catch(() => _setStatus(false));
  }

  function _setStatus(online) {
    const el = document.getElementById('server-status');
    if (!el) return;
    el.textContent = online ? '● Server attivo' : '● Server non raggiungibile';
    el.className   = 'server-status ' + (online ? 'online' : 'offline');
  }

  return { render, setStatus: _setStatus };
})();
