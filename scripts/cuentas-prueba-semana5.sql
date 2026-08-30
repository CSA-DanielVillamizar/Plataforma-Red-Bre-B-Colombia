-- Cuentas de prueba para el experimento de particionamiento (Semana 5)
--
-- Crea 20 cuentas con identificadores predecibles para que el generador de
-- carga pueda repartir las transferencias entre ellas:
--     aaaaaaaa-0000-0000-0000-000000000001 .. 000000000020
--
-- Por que saldos de 100 000 000: una corrida de 300 transferencias por 1 UVB
-- retiene 300. El saldo alto evita que la demo se caiga por fondos
-- insuficientes a mitad de clase, que es un fallo tonto y muy visible.
--
-- Es idempotente: se puede correr las veces que haga falta, y ademas sirve
-- para restablecer los saldos entre corridas.
--
-- Uso:
--   docker exec -i breb-postgres psql -U postgres -d brebcuentas \
--     < scripts/cuentas-prueba-semana5.sql

INSERT INTO "Cuentas" ("Id", "SaldoDisponible", "SaldoRetenido")
SELECT
    ('aaaaaaaa-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
    100000000,
    0
FROM generate_series(1, 20) AS i
ON CONFLICT ("Id") DO UPDATE
    SET "SaldoDisponible" = 100000000,
        "SaldoRetenido"   = 0;

-- Verificacion: deben quedar 22 (las 2 originales + las 20 de prueba).
SELECT COUNT(*) AS cuentas_totales FROM "Cuentas";
