using Breb.App.Vistas;

namespace Breb.App;

public partial class AppShell : Shell
{
    public AppShell(LoginPage login, SaldoPage saldo, TransferenciaPage transferencia)
    {
        InitializeComponent();
        ContenidoLogin.Content = login;
        ContenidoSaldo.Content = saldo;
        ContenidoTransferencia.Content = transferencia;
    }
}
