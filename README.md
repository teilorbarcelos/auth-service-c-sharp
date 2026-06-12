# Auth Service (C#/.NET) 🔐

Microsserviço de autenticação dedicado, extraído do `backend-c-sharp`.
Plug-and-play com o monólito: setar `AUTH_MODE=remote` no monólito e subir este serviço na porta `8001`.

---

## 🚀 Tecnologias

- **Runtime & SDK:** .NET 10.0
- **Framework Web:** ASP.NET Core Web API
- **ORM:** Entity Framework Core 10 (SQL Server)
- **Cache/Sessão:** Redis (StackExchange.Redis)
- **Autenticação:** JWT (HS256) + BCrypt
- **Validação:** FluentValidation
- **CQRS:** MediatR
- **Documentação:** Swagger / OpenAPI 3.0 (`/v1/docs`)

---

## 🔌 Plug-and-Play: Monolito → Microsserviço

O `auth-service-csharp` substitui o módulo de autenticação do `backend-c-sharp` sem alterar o middleware JWT, o RBAC ou a sessão Redis.

### Como funciona

```
FRONTEND                   AUTH SERVICE (8001)         MONOLITH (8888)
   │                            │                          │
   ├─ POST /login ────────────→│                          │
   │                            ├─ SELECT User+Auth+Role  │
   │                            ├─ BCrypt verify          │
   │                            ├─ Redis: create session  │
   │                            ├─ JWT (HS256)            │
   │←── { token, refresh } ────│                          │
   │                                                      │
   ├─ GET /users (JWT) ────────────────────────────────→│
   │                          ├─ valida JWT local        │
   │                          ├─ checa Redis session     │
   │                          ├─ RBAC check (permissions)│
   │←─────────────────────────────────────────────────────│
```

### Passo a passo

```bash
# 1. Configure o auth-service
cd auth-service-csharp
cp .env.example .env
# Edite .env: mesma DATABASE_URL, JWT_SECRET e REDIS_HOST do monólito

# 2. Suba o auth-service (porta 8001)
make dev

# 3. No monólito, ative o modo remoto
# backend-csharp/.env → AUTH_MODE=remote

# 4. Frontend passa a chamar:
#   - POST /v1/auth/login        → auth-service (8001)
#   - POST /v1/auth/refresh      → auth-service (8001)
#   - POST /v1/auth/logout       → auth-service (8001)
#   - Demais endpoints           → monólito (8888)

# 5. Pronto! O JWT emitido pelo auth-service é aceito pelo monólito.
```

### O que muda no monólito

| Componente | Antes (monolito) | Depois (auth-service) |
|---|---|---|
| `/v1/auth/*` endpoints | Handler local | ❌ Remove (404 via middleware) |
| Middleware JWT | `JwtAuthenticationMiddleware` | ✅ **Igual** |
| Middleware RBAC | `TokenSessionValidationMiddleware` | ✅ **Igual** |
| Session version (Redis) | `session:user:{id}:*` | ✅ **Igual** |
| Tabela `Auth` | EF Core `ApplicationDbContext` | ✅ **Igual** (compartilhada) |

---

## 🏁 Começando

```bash
# 1. Suba infraestrutura (ou use a do monólito)
make infra-up

# 2. Configure o ambiente
cp .env.example .env

# 3. Inicie o servidor
make dev
```

### Variáveis de ambiente

```bash
PORT=8001
DATABASE_URL="Server=localhost,1433;Database=backend_c_sharp;..."
JWT_SECRET=86941813-8b97-4cad-b0b2-f97734a947d7
REDIS_HOST="redis://localhost:6379"
```

---

## 📡 Endpoints

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/v1/auth/login` | ❌ | Login (email + password) |
| POST | `/v1/auth/refresh` | ❌ | Renova par de tokens |
| POST | `/v1/auth/logout` | ✅ | Revoga sessão |
| GET | `/v1/auth/me` | ✅ | Dados do usuário logado |
| POST | `/v1/auth/password/request` | ❌ | Solicita reset de senha |
| POST | `/v1/auth/password/validate` | ❌ | Valida token de reset |
| POST | `/v1/auth/password/change` | ❌ | Altera senha |
| GET | `/v1/auth/.well-known/jwks.json` | ❌ | JWKS (placeholder RS256) |
| GET | `/health` | ❌ | Health check |
| GET | `/liveness` | ❌ | Liveness probe |
| GET | `/ready` | ❌ | Readiness probe |

---

## 🧪 Testes

```bash
# Testes unitários e de integração
make test

# Cobertura
make coverage
```

### Compliance (E2E com monólito)

```bash
cd ../mage-backend-compliance

# Modo monolítico
cp .env.csharp .env
make test-csharp

# Modo microsserviço
cp .env.auth.csharp .env
make test-auth-csharp
```
