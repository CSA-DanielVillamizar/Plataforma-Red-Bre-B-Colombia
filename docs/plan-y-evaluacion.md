# Plan de las clases restantes y esquema de evaluación
## Programación Distribuida (PDI74) · 190304014-1 · Red Bre-B Colombia
### Del 9 de septiembre al 25 de noviembre de 2026

---

## 1. La situación, en números

| | |
|---|---|
| Sesiones lunes/miércoles del 7-sep al 29-nov | 24 |
| Menos la del 7-sep (Clase 6, ya dictada) | −1 |
| Menos festivos: **12-oct**, **2-nov**, **16-nov** (los tres, lunes) | −3 |
| **Sesiones efectivas restantes** | **20** |

El 29 de noviembre cae domingo. **La última clase real es el miércoles 25 de noviembre.**

---

## 2. Lo que exige el temario oficial y dónde estamos

El documento PDI74 define siete saberes. Estado real tras seis semanas:

| # | Saber oficial | Estado |
|---|---|---|
| 1 | Programación distribuida por capas · protocolos de comunicación | **Cubierto** — arquitectura por capas, mensajería, contratos |
| 2 | Análisis de requerimientos · técnicas de entrevista | **Cubierto** — SDD, historias de usuario, Event Storming |
| 3 | Integración de componentes de transacción · BD distribuidas | **Cubierto a fondo** — sagas, outbox, concurrencia, particionamiento |
| 4 | Comunicación entre sistemas · **seguridad en redes y sistemas distribuidos** | **Pendiente** — falta JWT y autenticación |
| 5 | **Desarrollo de aplicaciones multiplataforma** · diseño de UI | **Pendiente** |
| 6 | **Alternativas para desarrollo multiplataforma** · frameworks | **Pendiente** |
| 7 | **Aplicaciones móviles como estrategia** | **Pendiente** |

El temario es explícito en tres puntos que hay que cumplir:

> *"Implementación, instalación y puesta en marcha de la solución en un **Smartphone**. Conocimientos sobre el entorno de desarrollo, **SDK (MAUI)**, lenguaje de programación (C#)."*
>
> *"Conocimientos en la implementación de **JWT**, API REST."*
>
> *"Conocimiento en el diseño e implementación de aplicación **Crossplatform** para dispositivos móviles — **trabajo final**."*

**Por qué esto encaja y no es un anexo forzado:** la app móvil es el cliente natural de la plataforma que los estudiantes ya construyeron. Y el problema de una app offline —cola local, reintentos, sincronización, no duplicar la transferencia— **es exactamente el mismo problema distribuido** que vienen resolviendo desde la Semana 2. El móvil no cambia de tema: lo cierra.

---

## 3. Esquema de evaluación

El temario oficial fija la estructura:

| Actividad | % | Según el temario |
|---|---|---|
| Examen Parcial | 20 % | Semana 9 |
| Examen Final | 20 % | Semana 17 |
| Actividades de Seguimiento | 60 % | Las define el profesor, cada una ≤ 20 % |

Se traduce en **cinco eventos del 20 %**: parcial, final y **tres seguimientos**.

| Evento | Fecha | Tipo | Qué evalúa | Modalidad |
|---|---|---|---|---|
| **E1** | **mié 23-sep** | Seguimiento | La plataforma distribuida construida (Semanas 1-6) | Squad + sustentación |
| **E2** | **mié 30-sep** | **Parcial** | Fundamentos: mensajería, sagas, concurrencia, seguridad | **Individual** |
| **E3** | **mié 21-oct** | Seguimiento | App MAUI consumiendo la API con JWT | Squad |
| **E4** | **mié 11-nov** | Seguimiento | App corriendo en un teléfono real, end-to-end | Squad + demo |
| **E5** | **mié 25-nov** | **Final** | Trabajo final: solución completa | Sustentación individual sobre trabajo de squad |

> **Sobre E1:** llevamos seis semanas de trabajo sin nota. E1 no pide nada nuevo — **califica lo que ya existe** en el repositorio: Issues de historias de usuario, Pull Requests, análisis de squad en los comentarios, y las decisiones técnicas documentadas. Es recuperar el trabajo hecho, no agregar carga.

---

## 4. Cronograma de las 20 sesiones

### Bloque A — Cerrar el backend distribuido (7 sesiones · 9-sep a 30-sep)

| # | Fecha | Clase | Tema |
|---|---|---|---|
| 7 | mié 9-sep | Semana 7 | **Comunicación entre servicios**: REST, gRPC y contratos. Cuándo cada uno |
| 8 | lun 14-sep | Semana 8 | **Seguridad I**: autenticación con JWT en la API Bre-B |
| 9 | mié 16-sep | Semana 8 | **Seguridad II**: autorización, secretos, y por qué el token no basta entre servicios |
| 10 | lun 21-sep | Semana 9 | **Observabilidad**: trazas distribuidas y correlación de logs |
| 11 | **mié 23-sep** | Semana 9 | 🎯 **E1 — Sustentación de squads** (20 %) |
| 12 | lun 28-sep | Semana 10 | Taller integrador y preparación del parcial |
| 13 | **mié 30-sep** | Semana 10 | 🎯 **E2 — PARCIAL** (20 %) |

### Bloque B — Multiplataforma con .NET MAUI (7 sesiones · 5-oct a 28-oct)

| # | Fecha | Clase | Tema |
|---|---|---|---|
| 14 | lun 5-oct | Semana 11 | **MAUI I**: qué es, cómo compila a varias plataformas, primer proyecto |
| 15 | mié 7-oct | Semana 11 | **MAUI II**: MVVM y binding — la arquitectura de la app |
| — | ~~lun 12-oct~~ | — | *Festivo — Día de la Raza* |
| 16 | mié 14-oct | Semana 12 | **Consumir la API Bre-B** desde el móvil, autenticando con JWT |
| 17 | lun 19-oct | Semana 13 | **La app offline**: cola local y sincronización. El mismo problema distribuido |
| 18 | **mié 21-oct** | Semana 13 | 🎯 **E3 — App consumiendo la API** (20 %) |
| 19 | lun 26-oct | Semana 14 | **Idempotencia desde el cliente**: reintentar sin duplicar la transferencia |
| 20 | mié 28-oct | Semana 14 | **UI adaptativa** y experiencia en pantalla de teléfono |

### Bloque C — Integración, despliegue y cierre (6 sesiones · 4-nov a 25-nov)

| # | Fecha | Clase | Tema |
|---|---|---|---|
| — | ~~lun 2-nov~~ | — | *Festivo — Todos los Santos* |
| 21 | mié 4-nov | Semana 15 | **Puesta en marcha en un teléfono real** *(requisito explícito del temario)* |
| 22 | lun 9-nov | Semana 16 | **Pruebas**: de la app y de contrato contra la API |
| 23 | **mié 11-nov** | Semana 16 | 🎯 **E4 — App en teléfono, end-to-end** (20 %) |
| — | ~~lun 16-nov~~ | — | *Festivo — Independencia de Cartagena* |
| 24 | mié 18-nov | Semana 17 | **Despliegue** de la plataforma completa · retrospectiva técnica del curso |
| 25 | lun 23-nov | Semana 17 | Preparación de la sustentación final |
| 26 | **mié 25-nov** | Semana 17 | 🎯 **E5 — SUSTENTACIÓN FINAL** (20 %) |

---

## 5. Qué se evalúa en cada evento

### E1 · Sustentación de la plataforma distribuida — 23 de septiembre

**No requiere trabajo nuevo.** Se califica la evidencia que ya está en el repositorio.

| Criterio | Peso | Evidencia |
|---|---|---|
| Historias de usuario en formato SDD | 20 % | Issues con criterios Gherkin |
| Calidad de los Pull Requests | 20 % | Descripción, justificación técnica, revisión cruzada |
| Análisis de squad documentado | 20 % | Comentarios en los Issues de cada semana |
| Comprensión de las decisiones técnicas | 30 % | Sustentación oral de 5 minutos por squad |
| Trabajo en equipo | 10 % | Distribución real de contribuciones en el repositorio |

**Pregunta obligatoria en la sustentación:** *"¿Por qué su sistema usa bloqueo pesimista y no optimista?"* — quien no pueda responderla no participó del trabajo.

### E2 · Parcial — 30 de septiembre

**Individual.** Mitad conceptual, mitad práctica sobre el código del proyecto.

| Sección | Peso | Contenido |
|---|---|---|
| Conceptual | 40 % | Sagas y compensación, outbox, idempotencia, consistencia eventual, JWT |
| Lectura de código | 30 % | Se les da un fragmento con un defecto de concurrencia; deben encontrarlo y explicarlo |
| Diseño | 30 % | Un escenario nuevo: qué eventos, qué saga, qué compensación |

> El de "lectura de código" es el que más enseña. Use un caso real del curso: el `LiberarRetencion(decimal monto)` de la Semana 5, sin decirles qué buscar.

### E3 · App MAUI consumiendo la API — 21 de octubre

| Criterio | Peso |
|---|---|
| La app compila y corre (emulador o dispositivo) | 20 % |
| Autenticación con JWT funcionando contra la API | 25 % |
| Consulta de saldo y disparo de transferencia | 25 % |
| Arquitectura MVVM aplicada, no todo en el code-behind | 20 % |
| Manejo de errores de red visible al usuario | 10 % |

### E4 · App en teléfono real, end-to-end — 11 de noviembre

| Criterio | Peso |
|---|---|
| Instalada y corriendo en un **teléfono físico** | 25 % |
| Flujo completo: login → saldo → transferencia → confirmación | 25 % |
| Comportamiento offline: encola y sincroniza al recuperar red | 30 % |
| No duplica la transferencia al reintentar | 20 % |

> El criterio de no-duplicación es el corazón del curso entero. Que lo demuestren **provocando** el fallo: activar modo avión a mitad de la transferencia.

### E5 · Sustentación final — 25 de noviembre

**Sustentación individual sobre el trabajo del squad.** Cada estudiante responde por el sistema completo, no solo por su parte.

| Criterio | Peso |
|---|---|
| La solución completa funciona: backend + móvil | 30 % |
| Sustentación de decisiones de arquitectura | 25 % |
| Respuesta a preguntas sobre partes que **no** programó | 25 % |
| Documentación y calidad del repositorio | 10 % |
| Identificación honesta de limitaciones del sistema | 10 % |

> Ese último criterio no es de relleno. Un estudiante que diga *"nuestro sistema pierde 2 de cada 300 transferencias bajo carga y así se detecta"* entendió más que uno que diga *"funciona perfecto"*.

---

## 6. Riesgos y cómo se manejan

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **7 sesiones para MAUI son justas** | Alto | La app se limita a 3 pantallas: login, saldo, transferencia. No es un curso de UI |
| **Estudiantes sin teléfono Android** | Medio | El emulador cuenta para E3; para E4 basta **un** teléfono por squad |
| **MAUI en Windows exige carga de trabajo** | Medio | Verificar el SDK instalado **antes** del 5 de octubre, no ese día |
| **Los tres festivos son lunes** | Medio | Ya está en el cronograma: ningún evento cae en lunes |
| **Seis semanas sin nota** | Alto | E1 lo resuelve calificando lo ya construido |

---

## 7. Lo que hay que preparar antes de cada bloque

**Antes del 9 de septiembre (Bloque A):**
- Nada nuevo. El laboratorio actual sirve.

**Antes del 5 de octubre (Bloque B) — esto sí necesita anticipación:**
- Instalar la carga de trabajo de **.NET MAUI** en Visual Studio y verificar que compila un proyecto vacío
- Configurar al menos un **emulador Android** funcionando
- Avisar a los estudiantes con dos semanas: quién tiene teléfono Android, quién no
- Confirmar que la API Bre-B es alcanzable desde el emulador (no es `localhost` — es `10.0.2.2`)

**Antes del 4 de noviembre (Bloque C):**
- Habilitar depuración USB en al menos un teléfono por squad

---

## 8. El arco narrativo del curso

| Semanas | Pregunta que responde |
|---|---|
| 1-3 | ¿Cómo se modela y se coordina una transacción distribuida? |
| 4-6 | ¿Qué pasa cuando eso se somete a carga real? |
| 7-10 | ¿Cómo se comunica y se protege entre sistemas? |
| 11-14 | ¿Cómo llega esto a las manos de un usuario? |
| 15-17 | ¿Cómo se pone en producción y qué límites tiene? |

> El curso no cambia de tema en octubre. La app móvil es el mismo problema distribuido visto desde el otro lado del cable: una red que se cae, una operación que hay que reintentar sin duplicar, y un estado que hay que reconciliar.

---

*Documento de planeación. Fechas de E1 a E5 definidas por el profesor, dentro de la estructura oficial del temario PDI74 (parcial 20 % · final 20 % · seguimiento 60 %).*
