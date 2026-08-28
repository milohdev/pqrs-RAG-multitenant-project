# Plataforma SaaS Multi-tenant de PQRS con IA y Widget Incrustable

Backend en **.NET 10** (Clean Architecture, 4 proyectos), base **PostgreSQL + pgvector**,
**RAG de autoatención** y **triaje automático** con IA (NVIDIA NIM), **tiempo real con SignalR**
para tickets críticos, y un **widget en JS vanilla** embebible. Todo levanta con un solo
`docker compose up`.

---

## Estructura

```
├── backend/                     # API (.NET 10, Clean Architecture)
│   ├── App.sln                  # Solución (App.Domain, App.Application, App.Infrastructure, App.Api)
│   ├── Directory.Packages.props # Versiones de paquetes centralizadas (CPM)
│   └── src/
│       ├── App.Domain/          # Entidades y contratos de dominio (sin dependencias)
│       ├── App.Application/     # Casos de uso y abstracciones (referencia solo Domain)
│       ├── App.Infrastructure/  # EF Core, tenancy, IA (NVIDIA), CORS, SignalR, auth
│       └── App.Api/             # Controllers, Program.cs, wwwroot/pqrs-widget.js, Dockerfile
├── frontend/                    # Panel de agentes (React + Vite)
│   ├── src/                     # App.jsx, api.js, pages/ (Login, Tickets, KbArticles)
│   ├── Dockerfile               # Build de producción + nginx
│   └── nginx.conf               # Sirve estáticos y proxya /api y /hubs al backend
├── docker-compose.yml           # 3 servicios: db, backend, frontend
├── .env.example                 # Variables de entorno de ejemplo
└── README.md
```

La dirección de dependencias es estricta: `Api → Application/Infrastructure → Application → Domain`.

---

## Puesta en marcha

### Requisitos
- Docker + Docker Compose
- Una **API key de NVIDIA** (para embeddings y chat): https://build.nvidia.com

### 1. Configurar variables de entorno

```bash
cp .env.example .env
```

Completar `.env`:

```
DB_USER=appuser
DB_PASSWORD=<contraseña de la base>
DB_NAME=pqrsdb
JWT_SECRET=<secreto largo de al menos 32 caracteres>
NVIDIA_API_KEY=<tu api key de nvidia>
```

> **Para la entrega:** el `.env` debe incluirse junto al proyecto — es de donde
> Docker Compose toma la key de NVIDIA, la contraseña de la DB y el secreto JWT.
> Sin `NVIDIA_API_KEY` configurada (o con el placeholder), el backend registra
> stubs de IA deterministas (ver "Módulo de IA").

> En desarrollo local (sin Docker), la connection string y la API key se leen de
> `backend/src/App.Api/appsettings.json`.

### 2. Levantar todo

```bash
docker compose up -d
```

El primer arranque aplica las migraciones y siembra datos de demostración
(idempotente): dos tenants, un agente por tenant y artículos de base de conocimiento.

Servicios y puertos:

| Servicio | URL | Qué es |
|---|---|---|
| **Panel de agentes** | `http://localhost:8081` | Frontend React (nginx) — login, tickets, KB |
| **Backend / API** | `http://localhost:8080` | API REST (Swagger en `/swagger` solo en Development) |
| **Widget** | `http://localhost:8080/pqrs-widget.js` | Script embebible para el cliente final |

### 3. Datos de demostración sembrados automáticamente

| Tenant | `WidgetApiKey` | Dominios permitidos (CORS) | Agente |
|---|---|---|---|
| Acme S.A. | `acme-widget-key` | `localhost,acme.com` | `agente@acme.com` / `Password123!` |
| Beta Corp | `beta-widget-key` | `localhost,betacorp.com` | `agente@betacorp.com` / `Password123!` |

---

## Endpoints (`/api/v1`)

### Públicos — Widget (identidad del tenant por header `X-Tenant-Id`)
| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/widget/rag-search` | Búsqueda RAG de autoatención. `{"query":"..."}` → `{matched, answer, articleIds}` |
| `POST` | `/widget/rag-search/feedback` | Registra métrica de desviación (`matchedArticleId` opcional) |
| `POST` | `/widget/tickets` | Radica un PQRS: `{customerName, customerEmail, subject, description, escalatedFromRag}` → `{ticketNumber, status}`. El triaje (tipo/prioridad/sentimiento/resumen) lo hace la IA |

### Protegidos — Agentes (JWT Bearer)
| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/auth/login` | `{email, password}` → `{token, tenantId, role}` |
| `GET` | `/kb-articles` | Lista artículos de KB del tenant |
| `POST` | `/kb-articles` | Crea artículo (genera el embedding automáticamente) |
| `PUT` | `/kb-articles/{id}` | Actualiza artículo (regenera el embedding) |
| `DELETE` | `/kb-articles/{id}` | Elimina artículo |
| `GET` | `/tickets?status=&priority=` | Lista tickets del tenant, filtrables |
| `PATCH` | `/tickets/{id}/status` | Cambia estado: `"Pendiente" \| "EnProceso" \| "Resuelto"` |

### Tiempo real
- `GET /hubs/tickets` — hub de SignalR. Los agentes se conectan con su JWT vía
  query string (`access_token`) y reciben `CriticalTicket` cuando un ticket se
  clasifica con `Priority = Alta` o `Sentiment = Negativo`.

---

## Cómo embeber el widget

En cualquier página del dominio permitido del tenant, incluir:

```html
<script src="http://localhost:8080/pqrs-widget.js"
        data-tenant="acme-widget-key"
        data-api-base="http://localhost:8080"></script>
```

El widget (JS vanilla, sin dependencias) inyecta un botón flotante en un **Shadow DOM**
(aisla estilos del sitio anfitrión) y ejecuta un flujo conversacional de 2 fases:

1. **Chat RAG** — el usuario pregunta; si la base de conocimiento matchea, responde y
   pregunta si resolvió la inquietud. Si dijo "Sí", se registra la desviación; si "No",
   se desvía al formulario.
2. **Formulario** — el usuario completa sus datos y radica el PQRS, recibiendo su número
   de radicado en pantalla.

El origen de la página que embeble el widget debe estar en `Tenants.AllowedDomains`
(CSV) del tenant correspondiente — el CORS se evalúa dinámicamente por request.

**Página de prueba rápida** (guardala en cualquier carpeta y servila con `python3 -m http.server 8081`):

```html
<!DOCTYPE html>
<html lang="es"><body>
  <h1>Sitio que embeber el widget</h1>
  <script src="http://localhost:8080/pqrs-widget.js"
          data-tenant="acme-widget-key"
          data-api-base="http://localhost:8080"></script>
</body></html>
```

> No la abras como `file://` (doble clic): el navegador manda `Origin: null` y el
> CORS dinámico del backend no puede resolver ese origen. Servila siempre por HTTP.

---

## Panel de agentes (frontend React)

Frontend opcional (fuera del alcance original del enunciado, agregado como
herramienta de demo) para que los agentes gestionen tickets y la base de
conocimiento de su empresa. **Un solo frontend** — el tenant se resuelve por el
JWT del login, así que cada agente ve solo lo de su empresa.

```
frontend/
├── vite.config.js      # proxy de /api y /hubs (incl. WebSocket) al backend :8080
└── src/
    ├── App.jsx         # ruteo por hash, conexión SignalR global, alertas críticas
    ├── api.js          # cliente fetch con JWT
    └── pages/
        ├── Login.jsx       # login de agente
        ├── Tickets.jsx     # bandeja + filtros + cambio de estado
        └── KbArticles.jsx  # CRUD de base de conocimiento
```

### Correrlo

Está incluido en el `docker compose up` (servicio `frontend`, nginx) en
**http://localhost:8081**. nginx sirve el build y proxya `/api` y `/hubs`
(SignalR/WebSocket) al backend, así que el panel y la API son same-origin.

Para desarrollo con hot-reload (alternativa):

```bash
cd frontend
npm install
npm run dev
# → http://localhost:5173   (Vite proxya al backend en localhost:8080)
```

El panel muestra los **tickets críticos en vivo** (SignalR): al radicarse un PQRS
clasificado `Alta`/`Negativo`, aparece una alerta en cualquier pantalla.

---

## Módulo de IA

- **Embeddings**: `nvidia/llama-nemotron-embed-vl-1b-v2` (2048 dims, truncadas a 2000 por el
  límite de índice de pgvector).
- **Chat/triaje**: `nvidia/nemotron-3-nano-30b-a3b`.
- El RAG busca por **similitud coseno** con umbral **0.45** (calibrado con datos reales en
  `RagSearchService`) y el LLM responde únicamente en base al contexto recuperado.
- El triaje pide al LLM un JSON estricto; si no llega JSON válido, el ticket se guarda con
  valores por defecto (el agente lo reclasifica) — nunca se pierde el ticket.

> **Stubs de IA**: si `NVIDIA_API_KEY` no está configurada (vacía o con el placeholder),
> se registran implementaciones stub deterministas (`App.Infrastructure/Ai/StubAiServices.cs`)
> para que el flujo y el aislamiento multi-tenant se puedan probar sin el servicio externo.
> Con una key configurada se usan los servicios NVIDIA reales.

---

## Aislamiento multi-tenant

- Columna `TenantId` en todas las tablas de tenant + **Global Query Filters** de EF Core.
- El widget resuelve el tenant por `WidgetApiKey` (token público, nunca el GUID interno)
  vía middleware; los agentes por el claim `tenant_id` del JWT.
- El `TenantId` se sella automáticamente en cada `INSERT`.
- La única excepción a los filtros es el login por email único global (`AuthService`).

---

## Verificación rápida

```bash
# Login de agente
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"agente@acme.com","password":"Password123!"}'

# RAG como widget (tenant Acme)
curl -X POST http://localhost:8080/api/v1/widget/rag-search \
  -H "X-Tenant-Id: acme-widget-key" -H "Content-Type: application/json" \
  -d '{"query":"¿A qué hora abren?"}'

# Radicar ticket (el triaje lo hace la IA)
curl -X POST http://localhost:8080/api/v1/widget/tickets \
  -H "X-Tenant-Id: acme-widget-key" -H "Content-Type: application/json" \
  -d '{"customerName":"Juan","customerEmail":"juan@test.com","subject":"Cobro duplicado","description":"Me cobraron dos veces el mismo mes.","escalatedFromRag":false}'
```

---

## Desarrollo local (sin Docker para la API)

```bash
cd backend
dotnet restore
# la DB debe estar corriendo (ej: docker compose up -d db)
dotnet run --project src/App.Api --urls http://localhost:8080
```

> Si el contenedor `backend` del compose está corriendo, el puerto 8080 estará
> ocupado: `docker compose stop backend` antes de levantar la API local (y
> `docker compose start backend` para volver).

Las migraciones se aplican solas al arrancar (`MigrateAsync`). Para regenerarlas:
`dotnet ef migrations add <Nombre> -p src/App.Infrastructure -s src/App.Api`.
