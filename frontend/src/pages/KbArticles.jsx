import { useEffect, useState } from 'react'
import { api } from '../api.js'

const empty = { question: '', answer: '' };

export default function KbArticles() {
  const [articles, setArticles] = useState([]);
  const [form, setForm] = useState(empty);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [info, setInfo] = useState('');

  async function load() {
    setLoading(true);
    try {
      setArticles(await api('/kb-articles'));
      setError('');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  function reset() { setForm(empty); setEditingId(null); }

  async function save(e) {
    e.preventDefault();
    setSaving(true);
    setError(''); setInfo('');
    try {
      if (editingId) {
        await api(`/kb-articles/${editingId}`, { method: 'PUT', body: form });
        setInfo('Artículo actualizado. El embedding se regeneró.');
      } else {
        await api('/kb-articles', { method: 'POST', body: form });
        setInfo('Artículo creado. El embedding se generó con la IA.');
      }
      reset();
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  function edit(a) {
    setEditingId(a.id);
    setForm({ question: a.question, answer: a.answer });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  async function remove(a) {
    if (!confirm(`¿Eliminar el artículo "${a.question}"?`)) return;
    try {
      await api(`/kb-articles/${a.id}`, { method: 'DELETE' });
      await load();
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <div>
      <h1>Base de conocimiento</h1>
      <p className="muted">Estos artículos son los que responde el chat RAG del widget de tu empresa.</p>

      <form className="card" onSubmit={save}>
        <h2>{editingId ? 'Editar artículo' : 'Nuevo artículo'}</h2>
        <input value={form.question} onChange={e => setForm({ ...form, question: e.target.value })}
          placeholder="Pregunta (ej: ¿Cómo solicito un reembolso?)" required />
        <textarea value={form.answer} onChange={e => setForm({ ...form, answer: e.target.value })}
          placeholder="Respuesta: escribí la respuesta completa, en el tono en que un cliente preguntaría."
          rows="4" required />
        <div className="row">
          <button disabled={saving}>{saving ? 'Guardando…' : editingId ? 'Guardar cambios' : 'Crear artículo'}</button>
          {editingId && <button type="button" className="ghost" onClick={reset}>Cancelar</button>}
        </div>
        {error && <p className="error">{error}</p>}
        {info && <p className="info">{info}</p>}
      </form>

      {loading && <p className="muted">Cargando…</p>}
      {!loading && articles.length === 0 && <p className="muted">Todavía no hay artículos para tu empresa.</p>}

      <ul className="articles">
        {articles.map(a => (
          <li key={a.id} className="card">
            <h3>{a.question}</h3>
            <p>{a.answer}</p>
            <div className="row">
              <button className="ghost" onClick={() => edit(a)}>Editar</button>
              <button className="ghost danger" onClick={() => remove(a)}>Borrar</button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
