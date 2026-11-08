namespace Breb.App.Modelos;

public record TransferenciaBancaria(
    Guid TransferenciaId,
    Guid CuentaId,
    decimal MontoUVB,
    DateTime CreadaEn);
