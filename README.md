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
**Plataforma de Orquestación Transaccional para la Red Bre-B** es una solución distribuida de alta disponibilidad y misión crítica diseñada para integrar comercios corporativos al sistema de pagos inmediatos de Colombia (Bre-B). El sistema opera bajo estrictos Acuerdos de Nivel de Servicio (SLA de < 20 segundos por transacción y límites de 1,000 UVB), asegurando una coreografía financiera impecable: resolución de llaves (DICE), retención preventiva de fondos en el core local, liquidación central (MOL) y compensación o reverso automático en caso de fallos.

Desarrollado como el proyecto central de ingeniería para la asignatura **Programación Distribuida (190304014-1)** en la *Institución Universitaria ITM*, bajo la supervisión arquitectónica del **Prof. M.Sc.IoT Daniel Andrey Villamizar Araque**.

---

### 🏛️ Arquitectura y Componentes Clave
La plataforma implementa un diseño orientado a microservicios reactivos y transacciones distribuidas:
* **`API Gateway / BFF`**: Interfaz unificada de entrada, validación de firmas y delegación de solicitudes de comercios afiliados.
* **`Servicio de Resolución de Llaves`**: Comunicación síncrona con el Banco de la República (DICE) optimizada mediante **Caché Distribuida en Redis**.
* **`Servicio de Orquestación de Pagos`**: Máquinas de estado persistidas basadas en **MassTransit y Automatonymous Sagas**, garantizando consistencia eventual y rollbacks compensatorios.
* **`Servicio de Cuentas`**: Persistencia en **PostgreSQL** aplicando estrictamente el **Patrón Outbox/Inbox** para evitar dobles escrituras y asegurar atomicidad entre la base de datos y el bus de eventos (**RabbitMQ**).

---

### 🛠️ Stack Tecnológico
* **Backend Framework:** .NET 8 / 9 (ASP.NET Core WebAPIs, gRPC, MassTransit)
* **Datos y ORM:** Entity Framework Core con PostgreSQL (Patrones Outbox/Inbox y Concurrencia Optimista)
* **Mensajería y Resiliencia:** RabbitMQ, Automatonymous Sagas, Redis Cache
* **Infraestructura y DevOps:** Docker Compose, OpenTelemetry, Serilog (Logging Centralizado), GitHub Projects & CLI (`gh`) para GitOps.

---

### 👥 Escuadrón de Ingeniería (ITM Grupo Remoto)
* **Squad Fintech & Core (Sagas y MassTransit):** Sara Bermúdez, Mónica Puentes, Samuel Osorio, Estiben Montoya, Sergio Álvarez
* **Squad DevOps, Infra y Cloud:** Stefany Builes, José Miguel Buriticá, David Ramírez, Duban Guerra
* **Squad Arquitectura, UI y QA:** Juan Daniel Duque, Leidy Mora, Valentina Alvarez, Juan Carlos Herazo
* **Squad Datos, Tuning y Observabilidad:** Jalvi Villegas, Diego Valencia, Yulieth Urrego, Juan Sebastián Cardona
* **Backend, APIs y Servicios:** Resto del equipo de ingeniería (más de 30 desarrolladores y especialistas senior distribuidos).

---

### 🚦 Primeros Pasos
Consulte nuestro [Tablero de Proyectos (Projects Board)](https://github.com/) para revisar los Issues activos, Historias de Usuario (SDD) y el progreso de los Sprints bajo nuestro flujo de trabajo GitOps.

---
---

<a name="english"></a>
## 🇬🇧 English

### 🏗️ Overview
**Bre-B Transaction Orchestration Platform** is a high-availability, mission-critical distributed solution designed to integrate corporate merchants into Colombia's instant payment system (Bre-B). The platform operates under strict Service Level Agreements (sub-20 second SLA per transaction), orchestrating a precise financial choreography: key resolution (DICE), preliminary balance holding in the local core, central settlement (MOL), and automated compensating rollbacks upon failure.

Developed as the core enterprise project for the **Distributed Programming (PDI74)** course at *Institución Universitaria ITM*, under the architectural supervision of **Prof. Daniel Andrey Villamizar Araque**.

---

### 🏛️ Architecture & Key Components
The platform implements a reactive microservices design built for distributed transactions:
* **`API Gateway / BFF`**: Unified ingress, signature validation, and request delegation for partner merchants.
* **`Key Resolution Service`**: Synchronous communication with the Central Bank (DICE) optimized via **Distributed Redis Caching**.
* **`Payment Orchestration Service`**: Persistent state machines powered by **MassTransit and Automatonymous Sagas**, ensuring eventual consistency and compensating rollbacks.
* **`Accounts Service`**: **PostgreSQL** persistence strictly enforcing the **Outbox/Inbox Pattern** to prevent dual-write anomalies and guarantee atomicity between DB state changes and messaging (**RabbitMQ**).

---

### 🛠️ Technology Stack
* **Backend Framework:** .NET 8 / 9 (ASP.NET Core WebAPIs, gRPC, MassTransit)
* **Data & ORM:** Entity Framework Core with PostgreSQL (Outbox/Inbox Patterns, Optimistic Concurrency)
* **Messaging & Resilience:** RabbitMQ, Automatonymous Sagas, Redis Cache
* **Infrastructure & DevOps:** Docker Compose, OpenTelemetry, Serilog (Centralized Logging), GitHub Projects & CLI (`gh`) for GitOps workflow.

---

### 👥 Engineering Squad (ITM Remote Group)
* **Fintech & Core Squad (Sagas & MassTransit):** Sara Bermúdez, Mónica Puentes, Samuel Osorio, Estiben Montoya, Sergio Álvarez
* **DevOps, Infra & Cloud Squad:** Stefany Builes, José Miguel Buriticá, David Ramírez, Duban Guerra
* **Architecture, UI & QA Squad:** Juan Daniel Duque, Leidy Mora, Valentina Alvarez, Juan Carlos Herazo
* **Data, Tuning & Observability Squad:** Jalvi Villegas, Diego Valencia, Yulieth Urrego, Juan Sebastián Cardona
* **Backend & Services:** Full engineering roster (30+ developers and senior specialists).

---

### 🚦 Getting Started
Please refer to our [Projects Board](https://github.com/) to check active Issues, User Stories (SDD), and Sprint milestones following our GitOps workflow.
