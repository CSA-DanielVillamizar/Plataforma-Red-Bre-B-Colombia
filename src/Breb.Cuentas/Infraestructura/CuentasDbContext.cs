using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Dominio;

namespace Breb.Cuentas.Infraestructura;

public class CuentasDbContext : DbContext
{
    public CuentasDbContext(DbContextOptions<CuentasDbContext> options)
        : base(options) { }

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();
    public DbSet<MensajeProcesado> MensajesProcesados => Set<MensajeProcesado>();

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

        base.OnModelCreating(modelBuilder);
    }
}