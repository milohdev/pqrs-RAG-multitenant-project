import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import Login from './pages/Login.jsx'
import Tickets from './pages/Tickets.jsx'
import KbArticles from './pages/KbArticles.jsx'
import { getToken, clearToken } from './api.js'

// El backend envía los enums como número por SignalR (0/1/2); se mapean a nombre.
const PRIORITY_NAMES = ['Baja', 'Media', 'Alta'];
const SENTIMENT_NAMES = ['Positivo', 'Neutro', 'Negativo'];
const numName = (v, names) => (typeof v === 'number' ? names[v] ?? v : v);

// Router casero con hash (#/): suficiente para el panel, sin dependencias extra.
function useHashRoute() {
  const [hash, setHash] = useState(window.location.hash || '#/login');
  useEffect(() => {
    const onChange = () => setHash(window.location.hash || '#/login');
    window.addEventListener('hashchange', onChange);
    return () => window.removeEventListener('hashchange', onChange);
  }, []);
  return hash;
}

export default function App() {
  const hash = useHashRoute();
  const [token, setTokenState] = useState(() => getToken());
  const [alerts, setAlerts] = useState([]);
  const authed = Boolean(token);

  // Sin sesión, todo lleva al login.
  useEffect(() => {
    if (!authed && hash !== '#/login') window.location.hash = '#/login';
  }, [authed, hash]);

  // SignalR a nivel de App: la conexión persiste mientras haya sesión, así el
  // agente recibe los tickets críticos en cualquier pantalla del panel.
  useEffect(() => {
    if (!authed) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/tickets', { accessTokenFactory: () => getToken() })
      .withAutomaticReconnect()
      .build();

    conn.on('CriticalTicket', (t) => {
      setAlerts(prev => [{ ...t, at: new Date().toLocaleTimeString() }, ...prev]);
    });

    conn.start().catch(() => { /* sin tiempo real si no conecta: no bloquea el panel */ });
    return () => { conn.stop(); };
  }, [authed]);

  const handleLogout = () => {
    clearToken();
    setTokenState(null);
    window.location.hash = '#/login';
  };

  if (!authed || hash === '#/login') {
    return <Login onLogin={() => { setTokenState(getToken()); window.location.hash = '#/tickets'; }} />;
  }

  let page;
  if (hash.startsWith('#/kb-articles')) page = <KbArticles />;
  else page = <Tickets />;

  return (
    <div className="app">
      <nav className="topbar">
        <a href="#/tickets" className={hash.startsWith('#/tickets') ? 'active' : ''}>Tickets</a>
        <a href="#/kb-articles" className={hash.startsWith('#/kb-articles') ? 'active' : ''}>Base de conocimiento</a>
        <button className="logout" onClick={handleLogout}>Cerrar sesión</button>
      </nav>
      <main className="content">
        {alerts.length > 0 && (
          <div className="alerts">
            {alerts.map((a, i) => (
              <div key={i} className="alert">
                🚨 <b>Ticket crítico:</b> {a.subject} · {numName(a.priority, PRIORITY_NAMES)} · {numName(a.sentiment, SENTIMENT_NAMES)} · {a.at}
              </div>
            ))}
          </div>
        )}
        {page}
      </main>
    </div>
  );
}
