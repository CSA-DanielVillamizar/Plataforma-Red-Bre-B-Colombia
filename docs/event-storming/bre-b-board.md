# Big Picture Event Storming - Transferencia Bre-B

> Documento de trabajo para la Semana 1. El flujo se expresa en lenguaje de negocio y se ordena cronologicamente; los nombres tecnicos aparecen solo cuando delimitan una frontera o un contrato.

## Objetivo y alcance

Modelar una transferencia interoperable desde la confirmacion del pagador hasta la acreditacion al beneficiario o la compensacion completa. El modelo cubre el camino feliz y los fallos descritos en HU-03 (#7) y HU-05 (#9).

## Linea de tiempo del dominio

```text
Pagador
  |
  +-- IniciarTransferencia -------------------------------> TransferenciaSolicitada
                                                               |
                                                               v
DICE       <---------------- ResolverLlave ------------------ LlaveResuelta
                                                               |
                                                               v
MOL        <---------------- RetenerFondos ------------------ FondosRetenidos
                                                               |
                                                               +--[politica: confirmacion antes del limite]--> AbonoConfirmado
                                                               |                                                   |
                                                               |                                                   v
                                                               |                                          TransferenciaConfirmada
                                                               |
                                                               +--[politica: sin confirmacion en 15 s]-------> TransferenciaCompensada
                                                                                                                   |
                                                                                                                   v
                                                                                                         PagoRevertido
                                                                                                                   |
                                                                                                                   v
                                                                                                           PagadorNotificado

Fallo despues de FondosRetenidos
  |
  +-- DetectarFalla --------------------------------------> FallaDeTransferenciaDetectada
                                                               |
                                                               v
                                                        CompensarTransferencia
                                                               |
                         +-----------------------------+-------+-----------------------------+
                         |                             |                                     |
                         v                             v                                     v
                 TransferenciaCompensada      CompensacionReintentada             SoporteManualRequerido
                 (reintegro exitoso)          (intento 2 o 3)                    (3 fallos consecutivos)
```

## Eventos de dominio (naranja)

| Evento | Significado | Evidencia / consecuencia |
|---|---|---|
| `TransferenciaSolicitada` | El pagador inicio una transferencia valida. | HU-02/HU-05: inicia la orquestacion. |
| `LlaveResuelta` | DICE devolvio la cuenta destino y la entidad financiera. | HU-01: permite continuar al destino correcto. |
| `FondosRetenidos` | El MOL retuvo el monto de la transferencia. | HU-03/HU-05: activa el limite de confirmacion y la posibilidad de compensar. |
| `AbonoConfirmado` | El core destino confirmo la acreditacion. | Camino feliz de HU-05. |
| `TransferenciaConfirmada` | La transferencia termino correctamente. | El beneficiario ve el dinero acreditado. |
| `FallaDeTransferenciaDetectada` | La Saga detecto un fallo despues de retener fondos. | HU-05: inicia la reversión de pasos anteriores. |
| `TransferenciaCompensada` | El saldo del pagador fue reintegrado. | HU-03: el dinero no queda atrapado. |
| `CompensacionReintentada` | El reintegro se intento nuevamente. | HU-03: resiliencia ante fallo del primer intento. |
| `SoporteManualRequerido` | Tres intentos de compensacion fallaron. | HU-03: evita un estado indefinido y escala el caso. |
| `PagoRevertido` | La transferencia fue revertida y tiene un motivo legible. | HU-05: el pagador puede entender el resultado. |
| `PagadorNotificado` | El pagador recibio el estado y el motivo en lenguaje no tecnico. | HU-03/HU-05: cierre observable para el usuario. |

## Comandos (azul)

- `IniciarTransferencia`
- `ResolverLlave`
- `RetenerFondos`
- `ConfirmarAbono`
- `DetectarFalla`
- `CompensarTransferencia`
- `ReintentarCompensacion`
- `NotificarPagador`

## Actores (amarillo)

- **Usuario Pagador:** inicia la transferencia y recibe la notificacion de resultado.
- **Usuario Beneficiario:** recibe el abono o queda protegido de un pago inconsistente.
- **Squad de Fintech & Core:** implementa la orquestacion y la compensacion.
- **Squad de Datos, Tuning y Observabilidad:** garantiza consistencia del ledger y evidencia operativa.

## Politicas (lila)

1. **Limite de confirmacion:** cuando ocurre `FondosRetenidos` y no llega `AbonoConfirmado` en 15 segundos, emitir `CompensarTransferencia`.
2. **Compensacion con reintentos:** cuando falla la compensacion, reintentar automaticamente hasta tres intentos, sin duplicar el efecto del reintegro.
3. **Escalamiento:** cuando fallan tres compensaciones consecutivas, emitir `SoporteManualRequerido` y conservar el motivo operativo.
4. **Reversion rapida:** cuando se detecta una falla despues de retener fondos, revertir los pasos anteriores en menos de 10 segundos desde la deteccion.
5. **Notificacion legible:** cuando ocurre `PagoRevertido`, notificar al pagador con un motivo en lenguaje no tecnico, nunca solo con un codigo interno.

## Sistemas externos (rosa)

| Sistema | Interaccion | Riesgo de integracion |
|---|---|---|
| **DICE** | Resuelve la llave del beneficiario a una cuenta destino. | Llave inexistente o respuesta no disponible. |
| **MOL** | Retiene y liquida fondos. | Fondos retenidos sin confirmacion del core destino. |
| **Core Bancario** | Debita al pagador y acredita al beneficiario. | Confirmacion tardia, perdida o fallo interno. |
| **GitHub / Spectral** | Publica y valida los contratos acordados. | Contrato desactualizado o no validado en PR. |

## Bounded contexts validados

| Bounded context | Eventos que produce | Eventos/comandos que consume | Responsabilidad |
|---|---|---|---|
| **API Gateway** | `TransferenciaSolicitada` | `IniciarTransferencia`, `TransferenciaConfirmada`, `PagoRevertido` | Entrada autenticada, correlacion y respuesta al cliente. |
| **Resolucion de Llaves** | `LlaveResuelta` | `ResolverLlave` | Consultar DICE y entregar el destino normalizado. |
| **Orquestacion (Saga)** | `FondosRetenidos`, `TransferenciaConfirmada`, `FallaDeTransferenciaDetectada`, `TransferenciaCompensada`, `CompensacionReintentada`, `SoporteManualRequerido` | `RetenerFondos`, `ConfirmarAbono`, `DetectarFalla`, `CompensarTransferencia`, `ReintentarCompensacion` | Coordinar pasos, limites, compensaciones e idempotencia. |
| **Cuentas** | `PagoRevertido`, `PagadorNotificado` | `TransferenciaCompensada`, `SoporteManualRequerido` | Mantener el saldo y el historial sin duplicados; presentar el resultado al usuario. |

## Decisiones y preguntas abiertas

- Los eventos se nombran en pasado; los comandos expresan intenciones.
- `transferenciaId` es el identificador de correlacion comun a todos los eventos.
- `eventId` permite deduplicar mensajes; procesar dos veces el mismo evento no debe duplicar un debito, abono o reintegro.
- `montoUVB` es la unidad de negocio del contrato; no se sustituye silenciosamente por `montoEnCentavos`.
- Arquitectura debe confirmar despues de la actividad si Notificaciones merece un bounded context independiente. Por ahora se mantiene dentro de Cuentas/API Gateway.

## Trazabilidad de aceptación

- HU-03 #7: `FondosRetenidos` -> `TransferenciaCompensada`; fallos -> `CompensacionReintentada` -> `SoporteManualRequerido`.
- HU-05 #9: `FondosRetenidos` -> `AbonoConfirmado` -> `TransferenciaConfirmada`, o `FallaDeTransferenciaDetectada` -> `PagoRevertido` -> `PagadorNotificado`.
- Contratos: OpenAPI para Resolucion de Llaves y AsyncAPI para eventos de Orquestacion/Cuentas.
