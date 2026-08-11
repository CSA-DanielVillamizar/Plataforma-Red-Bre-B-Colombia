# Caso de Negocio — Plataforma de Orquestación Transaccional Red Bre-B

> Documento fuente de verdad del negocio, redactado antes de cualquier decisión de implementación. Ninguna de las cinco secciones siguientes menciona una tecnología concreta — eso pertenece al Stack Tecnológico del [README](../../README.md), no a este documento.

Cierra el criterio de aceptación *"Documento `docs/business-case/red-bre-b.md` describe Problema, Oportunidad, Actores, Métrica de éxito, Alcance"* del [Issue #2](https://github.com/CSA-DanielVillamizar/Plataforma-Red-Bre-B-Colombia/issues/2).

---

## 1. Problema

Hoy, transferir dinero entre dos entidades financieras distintas en Colombia puede tardar horas o hasta el siguiente día hábil. La Red Bre-B exige que cualquier pago inmediato se resuelva en **menos de 20 segundos**, con montos de hasta **1,000 UVB**.

Ese SLA convierte cualquier falla de coordinación entre sistemas en algo más que un bug: si el liquidador central (MOL) retiene el saldo de un usuario pero el core bancario destino nunca confirma el abono, el resultado es **dinero atrapado** — un estado en el que el sistema no sabe si el pago se completó o no. Eso implica, en orden creciente de gravedad:

- Incertidumbre para el usuario sobre el estado real de su dinero.
- Reclamos de soporte que hoy no tienen un procedimiento automático de resolución.
- Un incidente reportable ante la Superintendencia Financiera si el reintegro no ocurre dentro de los tiempos regulados.
- Pérdida de confianza del ecosistema Bre-B como alternativa real a las transferencias tradicionales.

## 2. Oportunidad

Ser la plataforma de orquestación de referencia para la interoperabilidad de Bre-B: el componente que las entidades financieras y comercios afiliados confían para coordinar, sin intervención manual, el ciclo completo de una transferencia inmediata — incluida su compensación automática cuando algo falla.

Resolver esto bien no es solo evitar el problema descrito arriba: es la base habilitante para que, en fases posteriores del proyecto, se construyan encima productos de valor agregado (conciliación en tiempo real, analítica de fraude, líneas de crédito instantáneo) que hoy son inviables sobre una capa de orquestación poco confiable.

## 3. Actores

| Actor | Rol en el sistema |
|---|---|
| **Usuario Pagador** | Inicia la transferencia; espera confirmación o reversión dentro del SLA. |
| **Usuario Beneficiario** | Recibe el abono; su llave (celular/correo/documento) debe resolverse contra el DICE. |
| **DICE** (Directorio) | Sistema externo que resuelve una llave a una cuenta bancaria destino. No mueve dinero. |
| **MOL** (Módulo/Motor de Liquidación) | Sistema externo que mueve el dinero real entre entidades financieras. |
| **Core Bancario** (origen y destino) | Sistema transaccional interno de cada banco o billetera — el que debita/acredita al usuario real. |
| **Regulador** (Superintendencia Financiera / Banco de la República) | Define el SLA, los topes de monto (UVB) y los requisitos de trazabilidad y reintegro. |
| **Equipo de Ingeniería (squads del curso)** | Diseña, construye y opera la plataforma de orquestación. |

## 4. Métrica de Éxito

Todas las métricas deben ser verificables — ninguna se acepta como "que funcione bien":

- **≥ 99% de las transferencias** se resuelven (éxito o reversión notificada) en **menos de 20 segundos**.
- **0 casos de dinero atrapado sin reintegro** — toda retención de fondos que no se confirme dentro de la ventana permitida dispara una compensación automática verificable en el historial del usuario.
- **≥ 99.5% de las resoluciones de llave** contra el DICE responden en menos de 3 segundos (ver [HU-01](../hu/historias-usuario.md#hu-01--resolución-de-llave-del-beneficiario)).
- **100% de los movimientos** aparecen exactamente una vez en el historial del usuario, incluso ante reintentos internos del sistema (ver [HU-04](../hu/historias-usuario.md#hu-04--consistencia-del-historial-de-movimientos-sin-duplicados)).

## 5. Alcance

**Dentro de alcance** — cuatro bounded contexts, cada uno con un squad dueño primario:

| Bounded Context | Responsabilidad | Squad dueño |
|---|---|---|
| **API Gateway / BFF** | Punto de entrada único, validación de firmas, delegación de solicitudes. | Arquitectura, UI y QA |
| **Resolución de Llaves** | Consulta al DICE y traducción de llave a cuenta destino. | Fintech & Core |
| **Orquestación (Saga)** | Coordina Core Bancario, DICE y MOL; dispara compensaciones. | Fintech & Core |
| **Cuentas** | Ledger interno del usuario; consistencia entre base de datos y bus de eventos. | Datos, Tuning y Observabilidad |

El **Squad DevOps, Infra y Cloud** no es dueño de un bounded context de negocio — opera transversalmente la infraestructura (despliegues, mensajería, caché, observabilidad) que sostiene a los cuatro anteriores.

**Fuera de alcance (para esta fase):**
- Aplicación móvil o interfaz de usuario final para el pagador/beneficiario.
- Integración real con el DICE o el MOL de producción (se simulan/mockean durante el curso).
- Funcionalidades de valor agregado mencionadas en la Oportunidad (conciliación, analítica de fraude, crédito instantáneo).

---

## Referencias

- Roster y ownership de squads: [`docs/squads.md`](../squads.md)
- Historias de Usuario derivadas de este caso de negocio: [`docs/hu/historias-usuario.md`](../hu/historias-usuario.md)
- Flujo de trabajo GitOps/SDD para implementar cualquier ítem de este alcance: [README](../../README.md#flujo-de-trabajo-gitops--sdd)
