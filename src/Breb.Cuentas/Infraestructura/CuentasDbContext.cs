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