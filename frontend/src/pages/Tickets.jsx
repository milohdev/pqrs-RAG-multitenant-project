import { useEffect, useState } from 'react'
import { api } from '../api.js'

const STATUSES = ['Pendiente', 'EnProceso', 'Resuelto'];
const PRIORITIES = ['Baja', 'Media', 'Alta'];
const SENTIMENTS = ['Positivo', 'Neutro', 'Negativo'];

function badgeClass(t) {
  if (t.priority === 'Alta' || t.sentiment === 'Negativo') return 'crit';
  return '';
}

export default function Tickets() {
  const [tickets, setTickets] = useState([]);
  const [status, setStatus] = useState('');
  const [priority, setPriority] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  async function load() {
    setLoading(true);
    try {
      const qs = new URLSearchParams();
      if (status) qs.set('status', status);
      if (priority) qs.set('priority', priority);
      const data = await api(`/tickets${qs.toString() ? `?${qs}` : ''}`);
      setTickets(data);
      setError('');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, [status, priority]);

  async function changeStatus(ticket, newStatus) {
    try {
      const updated = await api(`/tickets/${ticket.id}/status`, { method: 'PATCH', body: newStatus });
      setTickets(prev => prev.map(t => t.id === updated.id ? updated : t));
    } catch (err) {
      alert(err.message);
    }
  }

  return (
    <div>
      <h1>Tickets</h1>

      <div className="filters">
        <label>Estado
          <select value={status} onChange={e => setStatus(e.target.value)}>
            <option value="">Todos</option>
            {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </label>
        <label>Prioridad
          <select value={priority} onChange={e => setPriority(e.target.value)}>
            <option value="">Todas</option>
            {PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}
          </select>
        </label>
        <button onClick={load} disabled={loading}>Actualizar</button>
      </div>

      {error && <p className="error">{error}</p>}
      {loading && <p className="muted">Cargando…</p>}

      {!loading && tickets.length === 0 && <p className="muted">No hay tickets.</p>}

      <table>
        <thead>
          <tr><th>Asunto</th><th>Tipo</th><th>Prioridad</th><th>Sentimiento</th><th>Estado</th><th>Cliente</th><th>Resumen</th><th>Fecha</th><th></th></tr>
        </thead>
        <tbody>
          {tickets.map(t => (
            <tr key={t.id} className={badgeClass(t)}>
              <td>{t.subject}</td>
              <td>{t.type}</td>
              <td>{t.priority}</td>
              <td>{t.sentiment}</td>
              <td>{t.status}</td>
              <td>{t.customerName} · {t.customerEmail}</td>
              <td className="summary">{t.summary || '—'}</td>
              <td>{new Date(t.createdAtUtc).toLocaleString()}</td>
              <td>
                <select value={t.status} onChange={e => changeStatus(t, e.target.value)}>
                  {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
