# Plataforma-Red-Bre-B-Colombia
Enterprise-grade distributed transaction orchestrator for the Colombian Bre-B instant payments network. Built with .NET 8/9, MassTransit Sagas, and Outbox Pattern for financial consistency. Developed for ITM Distributed Systems.

# Plataforma Red Bre-B Colombia 🇨🇴💳⚡

[![.NET Version](https://img.shields.io/badge/.NET-8.0%2F9.0-blue.svg)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-MassTransit%20Sagas%20%2F%20Outbox-orange.svg)]()
[![Methodology](https://img.shields.io/badge/SDD-Specification%20Driven-green.svg)]()
[![Institution](https://img.shields.io/badge/ITM-1903040141%202026-purple.svg)](https://www.itm.edu.co/)

> **Idiomas / Languages:** [Español](#español) | [English](#english)

---

<a name="español"></a>
## 🇪🇸 Español

### 🏗️ Descripción General

**Plataforma de Orquestación Transaccional para la Red Bre-B** es una solución distribuida de alta disponibilidad y misión crítica diseñada para integrar comercios corporativos al sistema de pagos inmediatos de Colombia (Bre-B).

**El problema de negocio:** hoy, transferir dinero entre bancos distintos en Colombia puede tardar horas o hasta el siguiente día hábil. Las normativas de pagos inmediatos exigen que Bre-B resuelva cualquier transferencia en **menos de 20 segundos**, con montos de hasta **1,000 UVB**. Si esa promesa se rompe en el peor momento —cuando el liquidador central (MOL) ya retuvo el saldo de un usuario pero el core bancario destino nunca confirmó el abono— el resultado no es un bug: es dinero atrapado, un incidente regulatorio, y una pérdida de confianza del usuario.

**La solución:** esta plataforma orquesta, mediante el **patrón Saga**, una coreografía financiera impecable entre tres sistemas externos:
- **DICE** (Directorio): resuelve una llave (celular, correo, documento) a una cuenta bancaria destino.
- **MOL** (Módulo/Motor de Liquidación): mueve el dinero "de verdad" entre entidades financieras.
- **Core Bancario**: el sistema transaccional interno de cada banco o billetera.

Si algo falla a mitad de camino, la Saga dispara una **compensación automática** que reintegra el saldo del usuario — el dinero nunca queda atrapado.

Desarrollado como el proyecto central de ingeniería para la asignatura **Programación Distribuida (190304014-1)** en la *Institución Universitaria ITM*, bajo la supervisión arquitectónica del **Prof. M.Sc.IoT Daniel Andrey Villamizar Araque**.

---

### 🏛️ Arquitectura y Componentes Clave

La plataforma implementa un diseño orientado a microservicios reactivos y transacciones distribuidas, organizados en 4 *bounded contexts*:

* **`API Gateway / BFF`**: Interfaz unificada de entrada, validación de firmas y delegación de solicitudes de comercios afiliados.
* **`Servicio de Resolución de Llaves`**: Comunicación síncrona con el Banco de la República (DICE) optimizada mediante **Caché Distribuida en Redis**.
* **`Servicio de Orquestación de Pagos`**: Máquinas de estado persistidas basadas en **MassTransit y Automatonymous Sagas**, garantizando consistencia eventual y rollbacks compensatorios.
* **`Servicio de Cuentas`**: Persistencia en **PostgreSQL** aplicando estrictamente el **Patrón Outbox/Inbox** para evitar dobles escrituras y asegurar atomicidad entre la base de datos y el bus de eventos (**RabbitMQ**).

```
[API Gateway] → [Resolución de Llaves] → [Orquestación (Saga)] → [Cuentas]
                     ↓ (Redis Cache)          ↓ (RabbitMQ)          ↓ (Outbox/EF Core)
                   DICE (gRPC)               MOL (gRPC)          PostgreSQL
```

---

### 🛠️ Stack Tecnológico

* **Backend Framework:** .NET 8 / 9 (ASP.NET Core WebAPIs, gRPC, MassTransit)
* **Datos y ORM:** Entity Framework Core con PostgreSQL (Patrones Outbox/Inbox y Concurrencia Optimista)
* **Mensajería y Resiliencia:** RabbitMQ, Automatonymous Sagas, Redis Cache
* **Infraestructura y DevOps:** Docker Compose, OpenTelemetry, Serilog (Logging Centralizado), GitHub Projects & CLI (`gh`) para GitOps.

---

### 👥 Escuadrón de Ingeniería (ITM Grupo Remoto)

* **Squad Fintech & Core (Sagas y MassTransit):** Sara Bermúdez, Mónica Puentes, Samuel Osorio, Estiben Montoya, Sergio Álvarez + 6 integrantes adicionales
* **Squad DevOps, Infra y Cloud:** Stefany Builes, José Miguel Buriticá, David Ramírez, Duban Guerra + 6 integrantes adicionales
* **Squad Arquitectura, UI y QA:** Juan Daniel Duque, Leidy Mora, Valentina Alvarez, Juan Carlos Herazo + 6 integrantes adicionales
* **Squad Datos, Tuning y Observabilidad:** Jalvi Villegas, Diego Valencia, Yulieth Urrego, Juan Sebastián Cardona + 6 integrantes adicionales

📋 **Roster completo con usuarios de GitHub:** [`docs/squads.md`](docs/squads.md) — 42 de 43 estudiantes confirmados. Úsalo para saber a quién asignar como revisor de tu Pull Request.

---

### 🔀 Flujo de Trabajo (GitOps + SDD)

Este repositorio sigue **Specification-Driven Development (SDD)**: la especificación (Historia de Usuario) se escribe y aprueba en un Issue *antes* de escribir código, y el código se valida contra esos criterios de aceptación.

**1. Toda tarea nace como Issue**, con formato Historia de Usuario (Como/Quiero/Para) y criterios de aceptación en Gherkin (Dado/Cuando/Entonces):

```bash
gh issue create \
  --title "[SDD] Implementar compensación de saga en Orquestación" \
  --label "sdd,orquestacion" \
  --body "Como usuario pagador, quiero que mi saldo se reintegre si la transferencia falla, para no perder dinero por una falla del sistema."
```

**2. Se crea una rama exclusiva** — nunca se trabaja directo sobre `main`. Convención de nombres: `<tipo>/<bounded-context>-<descripcion-corta>` (ej. `feat/orquestacion-saga-compensacion`, `docs/squads-roster`, `fix/cuentas-outbox-deadlock`).

```bash
git checkout -b feat/orquestacion-saga-compensacion
```

**3. Se abre un Pull Request con `Closes #N`** apuntando al Issue — esto cierra el Issue automáticamente cuando el PR se fusiona, dejando el código enlazado para siempre con la especificación que le dio origen. Asigna como revisor a alguien del squad dueño del bounded context (ver [`docs/squads.md`](docs/squads.md)):

```bash
gh pr create \
  --title "feat(orquestacion): implementa compensación de saga" \
  --body "Closes #12" \
  --reviewer "DanielDuque2" \
  --base main
```

**4. Solo se hace merge si los criterios de aceptación del Issue se cumplen.** El revisor valida el PR contra el Gherkin del Issue original, no solo contra el estilo del código.

**Reglas del repositorio:**
- `main` está protegida — todo cambio entra vía Pull Request revisado, nunca con push directo.
- Cada PR debe referenciar su Issue de origen (`Closes #N`).
- El revisor asignado debe ser del squad dueño del bounded context que se está modificando.
- Los labels (`sdd`, `saga`, `outbox`, `infra`, `qa`, etc.) ayudan a filtrar el backlog por squad.

---

### 🚦 Primeros Pasos

1. Revisa la pestaña [Issues](../../issues) para ver las Historias de Usuario activas y el backlog priorizado.
2. Consulta [`docs/squads.md`](docs/squads.md) para saber qué squad es dueño de cada bounded context y a quién asignar como revisor.
3. Sigue el flujo GitOps descrito arriba: Issue → Branch → Pull Request → Merge.
4. Antes de tu primer PR, lee los criterios de aceptación (Gherkin) del Issue que vas a resolver — un PR que no los cumple no se fusiona.

---

### 💻 Levantar el proyecto localmente

**Prerrequisitos:** .NET 8 SDK · Docker Desktop · `dotnet tool install --global dotnet-ef`

**1. Levantar la infraestructura** (RabbitMQ + PostgreSQL):

```bash
docker compose up -d
```

Espera a que ambos digan `(healthy)` — RabbitMQ tarda ~40 segundos:

```bash
docker compose ps
```

**2. Crear las tablas:**

```bash
cd src/Breb.Cuentas && dotnet ef database update
```

**3. Insertar una cuenta de prueba:**

```bash
docker exec -it breb-postgres psql -U postgres -d brebcuentas -c "INSERT INTO \"Cuentas\" (\"Id\", \"SaldoDisponible\", \"SaldoRetenido\") VALUES ('11111111-1111-1111-1111-111111111111', 5000, 0);"
```

**4. Ejecutar la aplicación:**

```bash
cd src/Breb.Cuentas && dotnet run
```

Swagger queda en `http://localhost:5051/swagger` · Panel de RabbitMQ en `http://localhost:15672` (`guest`/`guest`)

#### ⚠️ Notas importantes

| Tema | Detalle |
|---|---|
| **MassTransit** | Fijado en **8.5.2**. La v9 requiere licencia comercial y la app no arranca sin ella. **No actualices** estos paquetes. |
| **RabbitMQ** | Fijado en **3.12-management**. La 3.13 falla en Docker Desktop/Windows con `.erlang.cookie: eacces`. |
| **PostgreSQL** | Expuesto en el puerto **5433** (no 5432) para no chocar con instalaciones locales. |
| **Healthcheck** | No usa `rabbitmq-diagnostics`: corre como root y corrompe los permisos de la cookie de Erlang, matando el contenedor. |
| **Credenciales** | `dev_only_password` y `guest/guest` son de **desarrollo local únicamente**. Nunca uses estos valores fuera de tu máquina. |

#### Verificar que el Outbox funciona

```bash
# 1. Apaga la mensajería
docker compose stop rabbitmq

# 2. Dispara una transferencia desde Swagger → responde HTTP 200 igual

# 3. El evento quedó guardado, esperando:
docker exec -it breb-postgres psql -U postgres -d brebcuentas -c "SELECT COUNT(*) FROM \"OutboxMessage\";"

# 4. Revive la mensajería → el mensaje sale solo
docker compose start rabbitmq
```

> Si `OutboxMessage` da **0** después de una transferencia exitosa, **es lo correcto**: MassTransit borra la fila una vez entregado el mensaje. Para verla con contenido hay que apagar RabbitMQ primero, como en el paso 1.

---
---

<a name="english"></a>
## 🇬🇧 English

### 🏗️ Overview

**Bre-B Transaction Orchestration Platform** is a high-availability, mission-critical distributed solution designed to integrate corporate merchants into Colombia's instant payment system (Bre-B).

**The business problem:** today, transferring money between different banks in Colombia can take hours or until the next business day. Instant payment regulations require Bre-B to settle any transfer in **under 20 seconds**, up to **1,000 UVB**. If that promise breaks at the worst possible moment — when the central settlement engine (MOL) has already held a user's funds but the destination bank's core never confirmed the credit — the result isn't a bug: it's trapped money, a regulatory incident, and lost user trust.

**The solution:** this platform orchestrates, via the **Saga pattern**, a precise financial choreography across three external systems:
- **DICE** (Directory): resolves a key (phone, email, ID) to a destination bank account.
- **MOL** (Settlement Engine): moves the actual money between financial institutions.
- **Core Banking**: each bank or wallet's internal transactional system.

If anything fails mid-flight, the Saga triggers an **automatic compensation** that refunds the user's balance — money is never left trapped.

Developed as the core enterprise project for the **Distributed Programming (190304014-1)** course at *Institución Universitaria ITM*, under the architectural supervision of **Prof. M.Sc.IoT Daniel Andrey Villamizar Araque**.

---

### 🏛️ Architecture & Key Components

The platform implements a reactive microservices design built for distributed transactions, organized into 4 bounded contexts:

* **`API Gateway / BFF`**: Unified ingress, signature validation, and request delegation for partner merchants.
* **`Key Resolution Service`**: Synchronous communication with the Central Bank (DICE) optimized via **Distributed Redis Caching**.
* **`Payment Orchestration Service`**: Persistent state machines powered by **MassTransit and Automatonymous Sagas**, ensuring eventual consistency and compensating rollbacks.
* **`Accounts Service`**: **PostgreSQL** persistence strictly enforcing the **Outbox/Inbox Pattern** to prevent dual-write anomalies and guarantee atomicity between DB state changes and messaging (**RabbitMQ**).

```
[API Gateway] → [Key Resolution] → [Orchestration (Saga)] → [Accounts]
                    ↓ (Redis Cache)      ↓ (RabbitMQ)           ↓ (Outbox/EF Core)
                  DICE (gRPC)          MOL (gRPC)             PostgreSQL
```

---

### 🛠️ Technology Stack

* **Backend Framework:** .NET 8 / 9 (ASP.NET Core WebAPIs, gRPC, MassTransit)
* **Data & ORM:** Entity Framework Core with PostgreSQL (Outbox/Inbox Patterns, Optimistic Concurrency)
* **Messaging & Resilience:** RabbitMQ, Automatonymous Sagas, Redis Cache
* **Infrastructure & DevOps:** Docker Compose, OpenTelemetry, Serilog (Centralized Logging), GitHub Projects & CLI (`gh`) for GitOps workflow.

---

### 👥 Engineering Squad (ITM Remote Group)

* **Fintech & Core Squad (Sagas & MassTransit):** Sara Bermúdez, Mónica Puentes, Samuel Osorio, Estiben Montoya, Sergio Álvarez + 6 additional members
* **DevOps, Infra & Cloud Squad:** Stefany Builes, José Miguel Buriticá, David Ramírez, Duban Guerra + 6 additional members
* **Architecture, UI & QA Squad:** Juan Daniel Duque, Leidy Mora, Valentina Alvarez, Juan Carlos Herazo + 6 additional members
* **Data, Tuning & Observability Squad:** Jalvi Villegas, Diego Valencia, Yulieth Urrego, Juan Sebastián Cardona + 6 additional members

📋 **Full roster with GitHub handles:** [`docs/squads.md`](docs/squads.md) — 42 of 43 students confirmed. Use it to know who to assign as reviewer on your Pull Request.

---

### 🔀 Workflow (GitOps + SDD)

This repository follows **Specification-Driven Development (SDD)**: the specification (User Story) is written and approved in an Issue *before* any code is written, and the code is validated against those acceptance criteria.

**1. Every task starts as an Issue**, in User Story format (As/I want/So that) with Gherkin acceptance criteria (Given/When/Then):

```bash
gh issue create \
  --title "[SDD] Implement saga compensation in Orchestration" \
  --label "sdd,orchestration" \
  --body "As a paying user, I want my balance refunded if the transfer fails, so that I don't lose money to a system failure I don't control."
```

**2. Create a dedicated branch** — never work directly on `main`. Naming convention: `<type>/<bounded-context>-<short-description>` (e.g. `feat/orchestration-saga-compensation`, `docs/squads-roster`, `fix/accounts-outbox-deadlock`).

```bash
git checkout -b feat/orchestration-saga-compensation
```

**3. Open a Pull Request with `Closes #N`** pointing to the Issue — this automatically closes the Issue when the PR merges, permanently linking the code to the specification that originated it. Assign a reviewer from the squad that owns the affected bounded context (see [`docs/squads.md`](docs/squads.md)):

```bash
gh pr create \
  --title "feat(orchestration): implement saga compensation" \
  --body "Closes #12" \
  --reviewer "DanielDuque2" \
  --base main
```

**4. Merge only happens once the Issue's acceptance criteria are met.** The reviewer validates the PR against the original Issue's Gherkin, not just code style.

**Repository rules:**
- `main` is protected — every change goes through a reviewed Pull Request, never a direct push.
- Every PR must reference its originating Issue (`Closes #N`).
- The assigned reviewer must belong to the squad that owns the bounded context being modified.
- Labels (`sdd`, `saga`, `outbox`, `infra`, `qa`, etc.) help filter the backlog by squad.

---

### 🚦 Getting Started

1. Check the [Issues](../../issues) tab for active User Stories and the prioritized backlog.
2. Check [`docs/squads.md`](docs/squads.md) to know which squad owns each bounded context and who to assign as reviewer.
3. Follow the GitOps workflow described above: Issue → Branch → Pull Request → Merge.
4. Before your first PR, read the Gherkin acceptance criteria of the Issue you're resolving — a PR that doesn't meet them won't be merged.
