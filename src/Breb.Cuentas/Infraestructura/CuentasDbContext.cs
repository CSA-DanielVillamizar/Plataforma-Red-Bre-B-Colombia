using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Dominio;
using Breb.Cuentas.Sagas;

namespace Breb.Cuentas.Infraestructura;

public class CuentasDbContext : DbContext
{
    public CuentasDbContext(DbContextOptions<CuentasDbContext> options)
        : base(options) { }

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();
    public DbSet<MensajeProcesado> MensajesProcesados => Set<MensajeProcesado>();
    public DbSet<TransferenciaSagaState> TransferenciaSagas => Set<TransferenciaSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ⚠️ ESTAS TRES LÍNEAS SON OBLIGATORIAS.
        // Crean las tablas internas que MassTransit necesita para el Outbox.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Cuenta>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.SaldoDisponible).HasPrecision(18, 2);
            e.Property(c => c.SaldoRetenido).HasPrecision(18, 2);

            // ── Concurrencia optimista sobre el dinero (Semana 5) ──────────
            // Sin esto, dos retenciones simultáneas sobre la MISMA cuenta se
            // pisan: ambas leen el saldo viejo, ambas escriben, y una de las
            // dos se pierde (lost update). El evento sale igual y la saga nace
            // igual, así que quince segundos después intenta devolver una plata
            // que nunca se retuvo — y queda atrapada en Compensando.
            //
            // xmin es la columna de sistema de PostgreSQL que cambia en cada
            // UPDATE de la fila. Usarla como token hace que EF incluya
            // "WHERE xmin = <leido>" y detecte el conflicto en vez de pisarlo.
            //
            // La saga YA estaba protegida así desde la Semana 3 (Version).
            // Lo que faltaba era proteger la cuenta bancaria.
            // (UseXminAsConcurrencyToken() fue retirado en Npgsql 8; esta es
            //  la forma explícita equivalente y no requiere migración, porque
            //  xmin ya existe como columna de sistema en toda tabla.)
            e.Property<uint>("xmin")
             .HasColumnName("xmin")
             .HasColumnType("xid")
             .ValueGeneratedOnAddOrUpdate()
             .IsConcurrencyToken();
        });

        modelBuilder.Entity<MensajeProcesado>(e =>
        {
            e.HasKey(m => m.MessageId);
            e.HasIndex(m => m.ProcesadoEn);   // para poder purgar por fecha
        });

        modelBuilder.Entity<TransferenciaSagaState>(e =>
        {
            e.HasKey(s => s.CorrelationId);
            e.Property(s => s.CurrentState).HasMaxLength(64);
            e.Property(s => s.MontoUVB).HasPrecision(18, 2);
            e.Property(s => s.Version).IsConcurrencyToken();   // concurrencia optimista
            e.HasIndex(s => s.CurrentState);                   // consultas por estado
        });

        base.OnModelCreating(modelBuilder);
    }
}