// ── State ────────────────────────────────────────────────────────────────────

let _mode = null;       // 'add' | 'view' | 'edit'
let _currentId = null;
let _cache = {};        // id → patient record

// ── Toast ────────────────────────────────────────────────────────────────────

function showToast(msg, type = 'success') {
  const t = document.getElementById('toast');
  t.textContent = msg;
  t.className = `show ${type}`;
  clearTimeout(t._tid);
  t._tid = setTimeout(() => { t.className = ''; }, 3500);
}

// ── Load & render patients ────────────────────────────────────────────────────

async function loadPatients() {
  try {
    const res = await fetch('/patients');
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    _cache = data.patients || {};
    renderPatients(_cache);
    document.getElementById('server-status').textContent = '● Server attivo';
    document.getElementById('server-status').style.color = '#00C8F0';
  } catch (e) {
    document.getElementById('patient-list').innerHTML =
      `<div class="empty-state">Impossibile contattare il server.<br><small>${e.message}</small></div>`;
    document.getElementById('server-status').textContent = '● Server non raggiungibile';
    document.getElementById('server-status').style.color = '#FF4C6A';
  }
}

function renderPatients(patients) {
  const list = document.getElementById('patient-list');
  const entries = Object.values(patients);

  if (entries.length === 0) {
    list.innerHTML = '<div class="empty-state">Nessun paziente registrato. Aggiungi il primo!</div>';
    return;
  }

  list.innerHTML = entries.map(p => `
    <div class="patient-card${_currentId === p.id ? ' active' : ''}" data-id="${esc(p.id)}">
      <div class="card-row">
        <span class="card-name">${esc(p.display_name)}</span>
        <span class="card-id">${esc(p.id)}</span>
      </div>
      <div class="card-meta">
        <span class="badge">${esc(p.specialty)}</span>
        ${esc(p.diagnosis)} &nbsp;·&nbsp; ${p.age} anni
      </div>
      <div class="card-meta" style="margin-top:4px;font-size:.79rem">
        📋 ${esc(p.planned_procedure)} &nbsp;·&nbsp; 📅 ${esc(p.procedure_date)}
      </div>
    </div>
  `).join('');

  list.querySelectorAll('.patient-card').forEach(card => {
    card.addEventListener('click', () => openView(_cache[card.dataset.id]));
  });
}

function esc(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// ── Panel mode helpers ────────────────────────────────────────────────────────

const BADGES = { add: 'Nuovo', view: 'Visualizza', edit: 'Modifica' };

function setMode(mode, title) {
  _mode = mode;

  document.getElementById('panel-title').textContent = title;
  document.getElementById('mode-badge').textContent  = BADGES[mode];

  const panel = document.getElementById('form-panel');
  panel.dataset.mode = mode;
  panel.classList.add('visible');

  document.getElementById('actions-add').hidden  = mode !== 'add';
  document.getElementById('actions-view').hidden = mode !== 'view';
  document.getElementById('actions-edit').hidden = mode !== 'edit';

  clearErrors();

  // Highlight active card
  document.querySelectorAll('.patient-card').forEach(c => {
    c.classList.toggle('active', c.dataset.id === _currentId);
  });
}

function setFieldsDisabled(disabled) {
  ['f-id','f-name','f-age','f-diag','f-spec','f-proc','f-date','f-treat','f-hist','f-notes']
    .forEach(id => { document.getElementById(id).disabled = disabled; });
}

// ── Open modes ────────────────────────────────────────────────────────────────

function openAdd() {
  _currentId = null;
  clearForm();
  setFieldsDisabled(false);
  setMode('add', 'Nuovo paziente');
  document.getElementById('f-id').focus();
}

function openView(patient) {
  _currentId = patient.id;
  fillForm(patient);
  setFieldsDisabled(true);
  setMode('view', patient.display_name);
}

function openEdit() {
  // ID stays locked — can't change a patient's identifier
  setFieldsDisabled(false);
  document.getElementById('f-id').disabled = true;
  setMode('edit', document.getElementById('f-name').value);
  document.getElementById('f-name').focus();
}

function cancelEdit() {
  openView(_cache[_currentId]);
}

function closePanel() {
  document.getElementById('form-panel').classList.remove('visible');
  document.querySelectorAll('.patient-card').forEach(c => c.classList.remove('active'));
  clearForm();
  _mode = null;
  _currentId = null;
}

// ── Form helpers ──────────────────────────────────────────────────────────────

function fillForm(p) {
  document.getElementById('f-id').value    = p.id;
  document.getElementById('f-name').value  = p.display_name;
  document.getElementById('f-age').value   = p.age;
  document.getElementById('f-diag').value  = p.diagnosis;
  document.getElementById('f-spec').value  = p.specialty;
  document.getElementById('f-proc').value  = p.planned_procedure;
  document.getElementById('f-date').value  = p.procedure_date;
  document.getElementById('f-treat').value = p.current_treatment;
  document.getElementById('f-hist').value  = (p.history || []).join(', ');
  document.getElementById('f-notes').value = p.notes || '';
}

function clearForm() {
  ['f-id','f-name','f-age','f-diag','f-spec','f-proc','f-date','f-treat','f-hist','f-notes']
    .forEach(id => { document.getElementById(id).value = ''; });
  clearErrors();
}

function buildPayload(includeId = true) {
  const histRaw = document.getElementById('f-hist').value.trim();
  const p = {
    display_name:      document.getElementById('f-name').value.trim(),
    age:               parseInt(document.getElementById('f-age').value, 10),
    diagnosis:         document.getElementById('f-diag').value.trim(),
    specialty:         document.getElementById('f-spec').value,
    planned_procedure: document.getElementById('f-proc').value.trim(),
    procedure_date:    document.getElementById('f-date').value.trim(),
    current_treatment: document.getElementById('f-treat').value.trim(),
    history: histRaw ? histRaw.split(',').map(s => s.trim()).filter(Boolean) : [],
    notes:   document.getElementById('f-notes').value.trim(),
  };
  if (includeId) p.id = document.getElementById('f-id').value.trim();
  return p;
}

function autoId(el) {
  const pos = el.selectionStart;
  el.value = el.value.toLowerCase().replace(/ /g, '_').replace(/[^a-z0-9_]/g, '');
  el.setSelectionRange(pos, pos);
}

// ── Validation ────────────────────────────────────────────────────────────────

function setErr(id, msg) {
  const el    = document.getElementById(`err-${id}`);
  const input = document.getElementById(`f-${id}`);
  if (msg) { el.textContent = msg; el.classList.add('show'); input.classList.add('err'); }
  else     { el.classList.remove('show'); input.classList.remove('err'); }
}

function clearErrors() {
  ['id','name','age','diag','spec','proc','date','treat'].forEach(k => setErr(k, ''));
}

function validate(checkId = true) {
  clearErrors();
  let ok = true;

  if (checkId) {
    const id = document.getElementById('f-id').value.trim();
    if (!id)                            { setErr('id', 'ID obbligatorio'); ok = false; }
    else if (!/^[a-z0-9_]+$/.test(id)) { setErr('id', 'Solo lettere minuscole, numeri e underscore'); ok = false; }
  }

  const name  = document.getElementById('f-name').value.trim();
  const age   = document.getElementById('f-age').value.trim();
  const diag  = document.getElementById('f-diag').value.trim();
  const spec  = document.getElementById('f-spec').value;
  const proc  = document.getElementById('f-proc').value.trim();
  const date  = document.getElementById('f-date').value.trim();
  const treat = document.getElementById('f-treat').value.trim();

  if (!name)  { setErr('name',  'Nome obbligatorio'); ok = false; }
  if (!diag)  { setErr('diag',  'Diagnosi obbligatoria'); ok = false; }
  if (!spec)  { setErr('spec',  'Seleziona una specialità'); ok = false; }
  if (!proc)  { setErr('proc',  'Procedura obbligatoria'); ok = false; }
  if (!date)  { setErr('date',  'Data obbligatoria'); ok = false; }
  if (!treat) { setErr('treat', 'Trattamento obbligatorio'); ok = false; }

  if (!age) {
    setErr('age', 'Età obbligatoria'); ok = false;
  } else {
    const n = parseInt(age, 10);
    if (isNaN(n) || n < 0 || n > 150) { setErr('age', 'Numero tra 0 e 150'); ok = false; }
  }

  return ok;
}

// ── Delete patient ────────────────────────────────────────────────────────────

async function deletePatient() {
  if (!confirm(`Eliminare il paziente "${_cache[_currentId]?.display_name}"?\nVerranno cancellati anche i dati e le foto dal face_db.`)) return;

  try {
    const res = await fetch(`/patient/${_currentId}`, { method: 'DELETE' });

    if (res.ok) {
      showToast('✓ Paziente eliminato', 'success');
      closePanel();
      await loadPatients();
    } else {
      const body = await res.json().catch(() => ({}));
      showToast(`⚠️ ${body?.detail ?? `Errore server (${res.status})`}`, 'error');
    }
  } catch (e) {
    showToast('⚠️ Errore di rete', 'error');
  }
}

// ── Submit add ────────────────────────────────────────────────────────────────

async function submitAdd() {
  if (!validate(true)) return;

  const btn     = document.getElementById('btn-add');
  const spinner = document.getElementById('spinner-add');
  btn.disabled  = true;
  spinner.classList.add('show');

  try {
    const res = await fetch('/patient', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify(buildPayload(true)),
    });

    if (res.status === 201) {
      const created = await res.json();
      showToast('✓ Paziente aggiunto!', 'success');
      await loadPatients();
      openView(created);
    } else if (res.status === 409) {
      setErr('id', 'ID già esistente — scegline un altro');
    } else {
      const body = await res.json().catch(() => ({}));
      showToast(`⚠️ ${body?.detail ?? `Errore server (${res.status})`}`, 'error');
    }
  } catch (e) {
    showToast('⚠️ Errore di rete', 'error');
  } finally {
    btn.disabled = false;
    spinner.classList.remove('show');
  }
}

// ── Submit edit ───────────────────────────────────────────────────────────────

async function submitEdit() {
  if (!validate(false)) return;

  const btn     = document.getElementById('btn-edit');
  const spinner = document.getElementById('spinner-edit');
  btn.disabled  = true;
  spinner.classList.add('show');

  try {
    const res = await fetch(`/patient/${_currentId}`, {
      method:  'PUT',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify(buildPayload(false)),
    });

    if (res.ok) {
      const updated = await res.json();
      showToast('✓ Paziente aggiornato!', 'success');
      await loadPatients();
      openView(updated);
    } else {
      const body = await res.json().catch(() => ({}));
      showToast(`⚠️ ${body?.detail ?? `Errore server (${res.status})`}`, 'error');
    }
  } catch (e) {
    showToast('⚠️ Errore di rete', 'error');
  } finally {
    btn.disabled = false;
    spinner.classList.remove('show');
  }
}

// ── Init ──────────────────────────────────────────────────────────────────────

loadPatients();
