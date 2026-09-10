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
| **E1** | **mié 23-sep** | Seguimiento | Recrear el entorno y sustentar un Issue | Squad, "a tres brazos" |
| **E2** | **mié 30-sep** | **Parcial** | Fundamentos: mensajería, sagas, concurrencia, seguridad | **Individual** |
| **E3** | **mié 21-oct** | Seguimiento | App MAUI consumiendo la API con JWT | Squad |
| **E4** | **mié 11-nov** | Seguimiento | App corriendo en un teléfono real, end-to-end | Squad + demo |
| **E5** | **mié 25-nov** | **Final** | Trabajo final: solución completa | Sustentación individual sobre trabajo de squad |

> **Sobre E1:** llevamos seis semanas de trabajo sin nota, pero **nunca se les pidió hacer commits ni Pull Requests** — esa parte la hacía el docente. Calificar evidencia que nadie pidió producir no sería justo. En vez de eso, E1 mide algo que sí depende de ellos y vale más: **clonar el repositorio, recrear el entorno funcionando, y sustentar un Issue demostrando en vivo que está resuelto.**

---

## 4. Cronograma de las 20 sesiones

### Bloque A — Cerrar el backend distribuido (7 sesiones · 9-sep a 30-sep)

| # | Fecha | Clase | Tema |
|---|---|---|---|
| 7 | mié 9-sep | Semana 7 | **Comunicación entre servicios**: REST, gRPC y contratos. Cuándo cada uno |
| 8 | lun 14-sep | Semana 8 | **Seguridad I**: autenticación con JWT en la API Bre-B |
| 9 | mié 16-sep | Semana 8 | **Seguridad II**: autorización, secretos, y por qué el token no basta entre servicios |
| 10 | lun 21-sep | Semana 9 | **Observabilidad**: trazas distribuidas y correlación de logs |
| 11 | **mié 23-sep** | Semana 9 | 🎯 **E1 — Entorno recreado + sustentación de un Issue** (20 %) |
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

### E1 · Recrear el entorno y sustentar un Issue — 23 de septiembre

> **Este evaluable cambió de formato, y conviene entender por qué.** La versión original calificaba commits, Pull Requests y comentarios de cada estudiante. **Eso no era justo:** durante las seis primeras semanas nunca se les pidió hacer commits ni PRs — las actividades se revisaban al final de cada clase, y la revisión cruzada y los Pull Requests los hacía el docente.
>
> No se puede calificar evidencia que nadie pidió producir. El formato nuevo mide algo que **sí depende de ellos** y que además vale más: **que puedan levantar el sistema desde cero y explicarlo.**

**Cada squad:** clona el repositorio, recrea el entorno local funcionando, escoge **un Issue** y lo sustenta demostrando en vivo que está resuelto.

**Sustentación "a tres brazos":**

| Rol | Qué hace |
|---|---|
| **Brazo 1** | Muestra el ambiente recreado: contenedores, migraciones, aplicación arriba |
| **Brazo 2** | Ejecuta las pruebas o la demo del Issue elegido |
| **Brazo 3** | Sustenta el comportamiento: qué se ve, por qué pasa, qué problema resuelve |

**El resto del squad está presente** para resolver dudas o apoyar si aparece un impase. No son público: son parte del equipo que responde.

**Issues que pueden sustentar** — uno cualquiera, y dos squads pueden repetir:

| Issue | Qué demuestran |
|---|---|
| **#13** Semana 2 — Outbox | Detener RabbitMQ, ver los eventos acumularse y salir solos al recuperarlo |
| **#16** Semana 3 — Saga | Los dos caminos: confirmación a tiempo y compensación por timeout |
| **#18** Semana 4 — Deadlocks | Provocar un deadlock y ver a PostgreSQL elegir una víctima |
| **#20** Semana 5 — Escalamiento | Tres instancias con `consumers: 3`, y 1 cuenta contra 20 |
| **#21** El bug de las sagas | Reproducirlo, mostrar el `40001`, y que con el arreglo quedan **0** |
| **#23** Semana 6 — Retención | El rastro de auditoría con `CreadaEn` y `LiberadaEn` |

**Rúbrica:**

| Criterio | Peso |
|---|---|
| El entorno funciona — recreado por ellos, no una captura | 30 % |
| La demostración corre en vivo | 25 % |
| Sustentación del comportamiento: **por qué** pasa, no solo que pasó | 25 % |
| Los tres brazos participan de verdad | 10 % |
| El squad responde dudas y desatasca impases | 10 % |

**Cuatro preguntas publicadas de antemano**, para que las estudien. Cada squad responde la de su Issue más una de las otras tres:

1. ¿Por qué bloqueo pesimista y no concurrencia optimista?
2. ¿Qué resuelve el Outbox que no resuelve publicar el evento directamente?
3. Si la compensación llega dos veces, ¿qué pasa y por qué?
4. ¿Por qué agregar instancias **empeoró** el rendimiento sobre una sola cuenta?

> Si el entorno no levanta el día de la sustentación, que lo digan y muestren el Plan B. Se penaliza menos un fallo de infraestructura reconocido que una demostración que finge funcionar.

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
| **Seis semanas sin nota** | Alto | E1 lo resuelve pidiendo algo que sí depende de ellos: recrear el entorno y sustentarlo |

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
