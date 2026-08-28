import { useState } from 'react'
import { api, setToken } from '../api.js'

export default function Login({ onLogin }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function submit(e) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const res = await api('/auth/login', { method: 'POST', body: { email, password } });
      setToken(res.token);
      onLogin();
    } catch (err) {
      setError(err.message.includes('401') || err.message.includes('inválidas')
        ? 'Credenciales inválidas.'
        : 'No se pudo iniciar sesión. Revisá que el backend esté corriendo.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-wrap">
      <form className="login-card" onSubmit={submit}>
        <h1>Panel de agentes</h1>
        <p className="muted">PQRS · multi-tenant</p>
        <input value={email} onChange={e => setEmail(e.target.value)} placeholder="Email" type="email" required />
        <input value={password} onChange={e => setPassword(e.target.value)} placeholder="Contraseña" type="password" required />
        {error && <p className="error">{error}</p>}
        <button disabled={loading}>{loading ? 'Ingresando…' : 'Iniciar sesión'}</button>
        <p className="muted small">Demo: agente@acme.com / Password123!</p>
      </form>
    </div>
  );
}
