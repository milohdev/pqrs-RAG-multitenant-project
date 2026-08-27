(function () {
  const scriptTag = document.currentScript;
  const tenantKey = scriptTag.getAttribute('data-tenant');
  const apiBase = scriptTag.getAttribute('data-api-base') || '';

  const host = document.createElement('div');
  document.body.appendChild(host);
  const shadow = host.attachShadow({ mode: 'open' }); // aísla estilos del sitio anfitrión

  shadow.innerHTML = `
    <style>
      .pqrs-btn { position: fixed; bottom: 20px; right: 20px; width: 56px; height: 56px;
        border-radius: 50%; background: #4f46e5; color: #fff; border: none; cursor: pointer;
        font-size: 22px; box-shadow: 0 2px 8px rgba(0,0,0,.2); z-index: 999999; }
      .pqrs-panel { position: fixed; bottom: 88px; right: 20px; width: 320px; max-height: 460px;
        background: #fff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,.25);
        display: none; flex-direction: column; overflow: hidden; font-family: sans-serif; z-index: 999999; }
      .pqrs-panel.open { display: flex; }
      .pqrs-body { padding: 12px; overflow-y: auto; flex: 1; }
      .pqrs-input, .pqrs-textarea { width: 100%; box-sizing: border-box; padding: 8px; margin: 4px 0;
        border: 1px solid #ddd; border-radius: 6px; font-family: inherit; }
      .pqrs-send { width: 100%; padding: 8px; background: #4f46e5; color: #fff; border: none;
        border-radius: 6px; cursor: pointer; margin-top: 6px; }
      .pqrs-answer { background: #f3f4f6; padding: 10px; border-radius: 8px; margin: 8px 0; }
      .pqrs-yn button { margin-right: 8px; padding: 6px 10px; }
      .pqrs-state { font-size: 13px; color: #666; }
    </style>
    <button class="pqrs-btn" aria-label="Abrir chat de soporte">💬</button>
    <div class="pqrs-panel"><div class="pqrs-body"></div></div>
  `;

  const btn = shadow.querySelector('.pqrs-btn');
  const panel = shadow.querySelector('.pqrs-panel');
  const body = shadow.querySelector('.pqrs-body');

  btn.addEventListener('click', () => {
    panel.classList.toggle('open');
    if (panel.classList.contains('open') && !body.dataset.rendered) renderChatPhase();
  });

  function renderChatPhase() {
    body.dataset.rendered = '1';
    body.innerHTML = `
      <p>¿En qué podemos ayudarte?</p>
      <input class="pqrs-input" id="pqrs-query" placeholder="Escribí tu pregunta..." />
      <button class="pqrs-send" id="pqrs-ask">Preguntar</button>
      <div id="pqrs-result"></div>
    `;
    body.querySelector('#pqrs-ask').addEventListener('click', askRag);
  }

  async function askRag() {
    const query = body.querySelector('#pqrs-query').value.trim();
    if (!query) return;
    const resultEl = body.querySelector('#pqrs-result');
    resultEl.innerHTML = '<p class="pqrs-state">Buscando...</p>';

    try {
      const res = await fetch(`${apiBase}/api/v1/widget/rag-search`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantKey },
        body: JSON.stringify({ query })
      });
      const data = await res.json();

      if (data.matched) {
        resultEl.innerHTML = `
          <div class="pqrs-answer">${data.answer}</div>
          <p>¿Esta respuesta resolvió tu inquietud?</p>
          <div class="pqrs-yn"><button id="pqrs-yes">Sí</button><button id="pqrs-no">No</button></div>
        `;
        resultEl.querySelector('#pqrs-yes').addEventListener('click', async () => {
          await fetch(`${apiBase}/api/v1/widget/rag-search/feedback`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantKey },
            body: JSON.stringify({ query, matchedArticleId: (data.articleIds || [])[0] || null })
          });
          resultEl.innerHTML = '<p class="pqrs-state">¡Gracias!</p>';
        });
        resultEl.querySelector('#pqrs-no').addEventListener('click', () => renderFormPhase(query, true));
      } else {
        resultEl.innerHTML = '<p class="pqrs-state">No encontramos información. Completá el formulario:</p>';
        renderFormPhase(query, false);
      }
    } catch {
      resultEl.innerHTML = '<p class="pqrs-state">Ocurrió un error. Intentá de nuevo.</p>';
    }
  }

  function renderFormPhase(originalQuery, escalated) {
    body.innerHTML = `
      <input class="pqrs-input" id="pqrs-name" placeholder="Nombre" />
      <input class="pqrs-input" id="pqrs-email" placeholder="Correo" />
      <input class="pqrs-input" id="pqrs-subject" placeholder="Asunto" value="${originalQuery || ''}" />
      <textarea class="pqrs-textarea" id="pqrs-desc" placeholder="Descripción" rows="4"></textarea>
      <button class="pqrs-send" id="pqrs-submit">Enviar solicitud</button>
      <div id="pqrs-form-result"></div>
    `;
    body.querySelector('#pqrs-submit').addEventListener('click', () => submitTicket(escalated));
  }

  async function submitTicket(escalated) {
    const get = (id) => body.querySelector(id).value.trim();
    const payload = {
      customerName: get('#pqrs-name'),
      customerEmail: get('#pqrs-email'),
      subject: get('#pqrs-subject'),
      description: get('#pqrs-desc'),
      escalatedFromRag: escalated
    };
    const resultEl = body.querySelector('#pqrs-form-result');
    resultEl.innerHTML = '<p class="pqrs-state">Enviando...</p>';

    try {
      const res = await fetch(`${apiBase}/api/v1/widget/tickets`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantKey },
        body: JSON.stringify(payload)
      });
      if (!res.ok) throw new Error();
      const data = await res.json();
      resultEl.innerHTML = `<p class="pqrs-state">¡Listo! Tu número de radicado es <b>${data.ticketNumber}</b>.</p>`;
    } catch {
      resultEl.innerHTML = '<p class="pqrs-state">No pudimos enviar tu solicitud. Intentá de nuevo.</p>';
    }
  }
})();