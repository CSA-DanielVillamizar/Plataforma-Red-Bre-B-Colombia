namespace Breb.App;

public partial class BancoPage : ContentPage
{
    public BancoPage(ViewModels.BancoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.BancoViewModel viewModel)
            viewModel.AparecerCommand.Execute(null);
    }
}
