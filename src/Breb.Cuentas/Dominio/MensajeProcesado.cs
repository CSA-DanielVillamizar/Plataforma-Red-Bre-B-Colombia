namespace Breb.Cuentas.Dominio;

public class MensajeProcesado
{
    public Guid MessageId { get; private set; }
    public DateTime ProcesadoEn { get; private set; }

    private MensajeProcesado() { }

    public MensajeProcesado(Guid messageId, DateTime procesadoEn)
    {
        MessageId = messageId;
        ProcesadoEn = procesadoEn;
    }
}