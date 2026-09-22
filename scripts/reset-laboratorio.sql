-- Reinicio CONSISTENTE del laboratorio
--
-- Deja la base como recién creada: sin sagas, sin retenciones, sin mensajes
-- procesados, y con los saldos iniciales.
--
-- POR QUÉ NO BASTA CON UN UPDATE A LOS SALDOS: desde la Semana 6 el retenido
-- existe dos veces —el total en "Cuentas" y el detalle en "Retenciones"—. Si
-- solo se pone "SaldoRetenido" = 0, las retenciones vivas siguen ahí y la
-- consulta de cuadre marca una diferencia que no es un error de la aplicación.
-- Y una transferencia COMPLETADA (camino feliz) deja su retención sin liberar
-- para siempre, porque el dinero salió: sin este reinicio aparece como
-- "huérfana" en la consulta del Issue #23.
--
-- ⚠️ Correrlo SOLO con 0 sagas en vuelo:
--      SELECT COUNT(*) FROM "TransferenciaSagas";
--
-- bash:
--   docker exec -i breb-postgres psql -U postgres -d brebcuentas < scripts/reset-laboratorio.sql
-- PowerShell:
--   Get-Content scripts\reset-laboratorio.sql | docker exec -i breb-postgres psql -U postgres -d brebcuentas

TRUNCATE "TransferenciaSagas";
TRUNCATE "MensajesProcesados";
DELETE FROM "Retenciones";

UPDATE "Cuentas" SET
    "SaldoRetenido"   = 0,
    "SaldoDisponible" = CASE WHEN "Id"::text LIKE 'aaaaaaaa%' THEN 100000000 ELSE 5000 END;

SELECT (SELECT COUNT(*) FROM "Cuentas")            AS cuentas,
       (SELECT COUNT(*) FROM "Retenciones")        AS retenciones,
       (SELECT COUNT(*) FROM "TransferenciaSagas") AS sagas;
