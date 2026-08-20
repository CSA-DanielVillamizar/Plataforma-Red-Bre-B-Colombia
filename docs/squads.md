# Squads — Plataforma Red Bre-B Colombia (190304014-1)

Roster oficial de escuadrones para el proyecto transversal del curso. Este archivo es la fuente de verdad para asignar revisores en Pull Requests y para saber a quién preguntarle qué durante las clases.

**Estado del roster:** 42 de 43 estudiantes confirmados. Los pendientes se añaden a su squad tan pronto compartan su usuario de GitHub — ver la sección [Pendientes](#pendientes) al final.

---

## Squad Fintech & Core (Sagas y MassTransit)

Responsables del Servicio de Orquestación de Pagos (MassTransit + Automatonymous Sagas) y de la lógica de compensación.

| Nombre | GitHub |
|---|---|
| Sara Bermudez | [@SaraBermudez4](https://github.com/SaraBermudez4) |
| Monica Puentes | [@monicapuentes-pd](https://github.com/monicapuentes-pd) |
| Samuel Osorio | [@samuelosorio-dev](https://github.com/samuelosorio-dev) |
| Estiben Montoya | [@EstibenMT](https://github.com/EstibenMT) |
| Sergio Álvarez | [@sergioter23](https://github.com/sergioter23) |
| Leonel Antonio Martinez Silgado | [@Leoces95](https://github.com/Leoces95) |
| Jean Carlos Gonzalez Goyeneche | [@JeanGonzalez10](https://github.com/JeanGonzalez10) |
| Juliana Arroyave Arango | [@Juli2609](https://github.com/Juli2609) |
| Kevin Santiago Martinez Molina | [@Kevinmartinez07](https://github.com/Kevinmartinez07) |
| Paula Andrea Calderón Quintero | [@Paucq](https://github.com/Paucq) |

## Squad DevOps, Infra y Cloud

Responsables de Docker Compose, despliegues, observabilidad de infraestructura y resiliencia (RabbitMQ, Redis).

| Nombre | GitHub |
|---|---|
| Stefany Builes | [@Stefany023](https://github.com/Stefany023) |
| Jose Miguel Buritica | [@BuritiCrack](https://github.com/BuritiCrack) |
| David Ramirez | [@davidramirez-beep](https://github.com/davidramirez-beep) |
| Duban Guerra Castro | [@duguerrac](https://github.com/duguerrac) |
| Edwin Ramirez Gonzalez | [@edwinramirezgon](https://github.com/edwinramirezgon) |
| Jorge Elias Builes Chavarría | [@JorgeBuiles](https://github.com/JorgeBuiles) |
| Juan Diego Quintero Ortiz | [@JuanDiego24](https://github.com/JuanDiego24) |
| Felipe Ramirez Loaiza | [@FelipeRamirezLoaiza](https://github.com/FelipeRamirezLoaiza) |
| Juan David Velasquez Murillo | [@Juandavm12](https://github.com/Juandavm12) |
| Evelyn Muñetones Álvarez | [@Eve1254](https://github.com/Eve1254) |

## Squad Arquitectura, UI y QA

Responsables del diseño de contratos (SDD), Event Storming, revisión arquitectónica y calidad.

| Nombre | GitHub |
|---|---|
| Juan Daniel Duque | [@DanielDuque2](https://github.com/DanielDuque2) |
| Leidy Mora | [@daihanamora](https://github.com/daihanamora) |
| Valentina Alvarez | [@ValenAlvarez16](https://github.com/ValenAlvarez16) |
| Juan Carlos Herazo | [@DunKeL626](https://github.com/DunKeL626) |
| Yenifer Gonzalez Quirama | [@YeniferGonzalezQ](https://github.com/YeniferGonzalezQ) |
| Liliana Arias Rivera | [@Lili1823](https://github.com/Lili1823) |
| Salome Ruiz Gallego | [@SalomeRG97](https://github.com/SalomeRG97) |
| Luis Guillermo Gonzalez Ayala | [@memouk](https://github.com/memouk) |
| David Stiven Diaz Duarte | [@EndyG34](https://github.com/EndyG34) |
| Johan Sneider Garzon Salazar | [@JohanGarzon9905](https://github.com/JohanGarzon9905) |

## Squad Datos, Tuning y Observabilidad

Responsables de PostgreSQL, tuning de queries/bloqueos, OpenTelemetry y Serilog.

| Nombre | GitHub |
|---|---|
| Jalvi Humberto Villegas Taborda | [@JVillegasT](https://github.com/JVillegasT) |
| Diego Valencia | [@D13G04L3X](https://github.com/D13G04L3X) |
| Yulieth Urrego | [@YuliethUrrego](https://github.com/YuliethUrrego) |
| Juan Sebastian Cardona | *(usuario pendiente)* |
| Sara Melissa Marroquin Vega | [@saram05](https://github.com/saram05) |
| Roison Garcia Sepulveda | [@RoisonGarcia](https://github.com/RoisonGarcia) |
| Danny Mateo Hernández Sánchez | [@dannymateo](https://github.com/dannymateo) |
| Juan Esteban Quintero Acosta | [@Juanes-Quintero](https://github.com/Juanes-Quintero) |
| Miguel Angel Giraldo Florez | [@miguel-Angel-G](https://github.com/miguel-Angel-G) |
| Juan Manuel Valencia Giraldo | [@Juanchos2905](https://github.com/Juanchos2905) |
| Juan David Alvarez Garcia | [@Juan-AG](https://github.com/Juan-AG) |

---

## Cómo usar este roster para asignar revisores

Al abrir un Pull Request con `gh pr create`, asigna como `--reviewer` a un miembro del squad dueño del bounded context que tocaste:

```bash
gh pr create \
  --title "feat(orquestacion): implementa compensacion de saga" \
  --body "Closes #12" \
  --reviewer "DanielDuque2"
```

## Pendientes

- **Juan Sebastian Cardona** (Squad Datos, Tuning y Observabilidad) — usuario de GitHub por confirmar.

Es el único estudiante del curso (43 en total) que aún no ha compartido su usuario.

Cuando se confirmen, actualízalos aquí vía Pull Request siguiendo el mismo flujo descrito en el [README](../README.md#flujo-de-trabajo-gitops--sdd).
