-- Cuentas base del laboratorio
--
--   11111111-...  la cuenta de todas las demos (Semanas 2 a 9)
--   22222222-...  la segunda cuenta, para provocar el deadlock (Semana 4, Issue #18)
--
-- Es idempotente: si las cuentas ya existen, no hace nada.
--
-- bash:
--   docker exec -i breb-postgres psql -U postgres -d brebcuentas < scripts/cuentas-base.sql
-- PowerShell:
--   Get-Content scripts\cuentas-base.sql | docker exec -i breb-postgres psql -U postgres -d brebcuentas

INSERT INTO "Cuentas" ("Id", "SaldoDisponible", "SaldoRetenido") VALUES
    ('11111111-1111-1111-1111-111111111111', 5000, 0),
    ('22222222-2222-2222-2222-222222222222', 5000, 0)
ON CONFLICT ("Id") DO NOTHING;

SELECT "Id", "SaldoDisponible", "SaldoRetenido" FROM "Cuentas"
WHERE "Id" IN ('11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222');
