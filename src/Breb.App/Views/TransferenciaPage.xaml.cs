using Breb.App.ViewModels;

namespace Breb.App.Views;

public partial class TransferenciaPage : ContentPage, IQueryAttributable
{
    public TransferenciaPage(TransferenciaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryAttributable vm)
            vm.ApplyQueryAttributes(query);
    }
}
