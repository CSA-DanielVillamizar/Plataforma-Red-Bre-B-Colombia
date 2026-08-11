# Historias de Usuario (SDD) — Clase 0

Especificaciones iniciales para el proyecto Red Bre-B, redactadas en formato **Specification-Driven Development**: Como/Quiero/Para + criterios de aceptación en Gherkin (Dado/Cuando/Entonces). Ninguna historia menciona una tecnología concreta — eso se decide al implementar, no al especificar (ver la sección "Anti-Patrón" de la Clase 0).

Cierra el criterio de aceptación *"Al menos 3 Historias de Usuario redactadas en formato SDD"* del [Issue #2](https://github.com/CSA-DanielVillamizar/Plataforma-Red-Bre-B-Colombia/issues/2).

---

## HU-01 — Resolución de llave del beneficiario

**Bounded context:** Resolución de Llaves
**Squad sugerido:** Fintech & Core (consumidor directo del servicio en el flujo de pago)

**Como** usuario pagador de la Red Bre-B,
**quiero** que el sistema resuelva automáticamente el celular o correo del beneficiario a su cuenta bancaria destino,
**para** no tener que conocer ni digitar manualmente el número de cuenta de la otra persona.

### Criterios de Aceptación

```gherkin
Escenario: Llave registrada y válida
  Dado que el beneficiario tiene su celular registrado en el DICE
  Cuando el usuario pagador ingresa ese celular como destino de la transferencia
  Entonces el sistema muestra el nombre de la entidad financiera y el nombre enmascarado del beneficiario en menos de 3 segundos

Escenario: Llave no registrada
  Dado que el celular ingresado no está registrado en el DICE
  Cuando el usuario pagador intenta iniciar la transferencia
  Entonces el sistema le informa que la llave no existe y no permite continuar con el pago
```

---

## HU-02 — Confirmación oportuna de la transferencia

**Bounded context:** API Gateway / Orquestación
**Squad sugerido:** Arquitectura, UI y QA (dueño del contrato de cara al usuario) + Fintech & Core (dueño de la Saga)

**Como** usuario pagador,
**quiero** saber en menos de 20 segundos si mi transferencia fue exitosa o revertida,
**para** no quedarme en incertidumbre sobre si mi dinero llegó a su destino.

### Criterios de Aceptación

```gherkin
Escenario: Transferencia exitosa dentro del SLA
  Dado que inicié una transferencia de menos de 1000 UVB
  Cuando todos los pasos de la orquestación (resolución de llave, retención de fondos, liquidación, confirmación) se completan correctamente
  Entonces recibo una confirmación de éxito en menos de 20 segundos desde que presioné "Enviar"

Escenario: La transferencia no se puede completar dentro del SLA
  Dado que inicié una transferencia válida
  Cuando algún paso de la orquestación no responde dentro de la ventana de tiempo permitida
  Entonces recibo una notificación de reversión antes de que se cumplan los 20 segundos, no un mensaje de "cargando" indefinido
```

---

## HU-03 — Reintegro automático ante fallo de liquidación (compensación)

**Bounded context:** Orquestación (Saga) + Cuentas
**Squad sugerido:** Fintech & Core (lógica de compensación) + Datos, Tuning y Observabilidad (consistencia del ledger)

**Como** usuario pagador de la Red Bre-B,
**quiero** que mi saldo se reintegre automáticamente si la transferencia no puede completarse,
**para** no perder mi dinero por una falla del sistema que no controlo.

### Criterios de Aceptación

```gherkin
Escenario: El MOL retiene fondos pero el banco destino no confirma
  Dado que el MOL retuvo mis fondos para la transferencia
  Cuando el core bancario destino no confirma el abono en menos de 15 segundos
  Entonces el sistema reintegra mi saldo completo y me notifica la reversión con el motivo

Escenario: La compensación en sí falla
  Dado que la Saga intentó compensar una transferencia fallida
  Cuando el reintegro no puede procesarse en el primer intento
  Entonces el sistema reintenta la compensación automáticamente y escala a un caso de soporte manual si falla 3 veces consecutivas, en vez de dejar el saldo del usuario en un estado indefinido
```

---

## HU-04 — Consistencia del historial de movimientos (sin duplicados)

**Bounded context:** Cuentas
**Squad sugerido:** Datos, Tuning y Observabilidad

**Como** usuario pagador,
**quiero** que cada transferencia aparezca exactamente una vez en mi historial de movimientos, incluso si el sistema tuvo que reintentar algún paso internamente,
**para** poder confiar en mi saldo y mi historial sin tener que auditar manualmente cada mes.

### Criterios de Aceptación

```gherkin
Escenario: Reintento interno transparente al usuario
  Dado que un paso interno de la orquestación se reintentó automáticamente por una falla de red
  Cuando la transferencia finalmente se completa
  Entonces el historial del usuario muestra un único movimiento, no uno por cada reintento

Escenario: Transferencia revertida
  Dado que una transferencia fue compensada (revertida)
  Cuando el usuario consulta su historial
  Entonces ve el movimiento original marcado como "Revertido", junto al reintegro correspondiente, sin que el saldo quede en un estado ambiguo
```

---

## HU-05 — Confiabilidad del pago ante fallos internos

**Bounded context:** Orquestación (Saga)
**Squad sugerido:** Fintech & Core

**Como** cliente bancario beneficiario de un pago,
**quiero** que mi pago se complete de forma confiable aunque alguno de los pasos internos del proceso falle,
**para** tener la certeza de que el dinero llega correctamente o, si algo sale mal, se revierte sin dejarme en un estado inconsistente (dinero descontado sin acreditar, por ejemplo).

### Criterios de Aceptación

```gherkin
Escenario: Pago exitoso de principio a fin
  Dado que el pagador confirmó una operación válida
  Cuando todos los pasos internos del proceso se completan correctamente
  Entonces el cliente bancario beneficiario ve el dinero acreditado en menos de 5 segundos desde la confirmación

Escenario: Falla interna con reversión
  Dado que un paso interno falló después de que los fondos ya fueron retenidos
  Cuando el sistema detecta la falla
  Entonces revierte automáticamente los pasos anteriores en menos de 10 segundos desde la detección de la falla, y ninguno de los clientes bancarios involucrados queda con saldo incorrecto

Escenario: Notificación al pagador tras una reversión
  Dado que una transferencia fue revertida
  Cuando el pagador consulta el estado de su operación
  Entonces recibe un mensaje en lenguaje no técnico explicando qué pasó, no un código de error
```

**Nota de procedencia:** propuesta originalmente por un equipo del curso como una sola historia con dos actores mezclados (beneficiario y pagador) y criterios en prosa. Se dividió el tercer escenario para separar al pagador como actor propio, y se convirtió a Gherkin en la revisión cruzada de la Clase 0. Métrica de éxito asociada: % de pagos que terminan en estado consistente + tiempo promedio de reversión ante fallas.

---

## Trazabilidad

| HU | Bounded Context | Squad sugerido | Estado |
|---|---|---|---|
| HU-01 | Resolución de Llaves | Fintech & Core | Especificada |
| HU-02 | API Gateway / Orquestación | Arquitectura, UI y QA + Fintech & Core | Especificada |
| HU-03 | Orquestación (Saga) + Cuentas | Fintech & Core + Datos, Tuning y Obs. | Especificada |
| HU-04 | Cuentas | Datos, Tuning y Observabilidad | Especificada |
| HU-05 | Orquestación (Saga) | Fintech & Core | Especificada |

Cada HU se convierte en su propio Issue (`gh issue create`) cuando el squad dueño la tome para implementar, siguiendo el flujo descrito en el [README](../../README.md#flujo-de-trabajo-gitops--sdd).
