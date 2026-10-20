namespace Breb.App.Pantallas;

// Vista de solo presentación: recibe el texto ya armado por el ViewModel.
public partial class PanelTrazaView : ContentView
{
	public static readonly BindableProperty TextoProperty =
		BindableProperty.Create(nameof(Texto), typeof(string), typeof(PanelTrazaView), string.Empty);

	public string Texto
	{
		get => (string)GetValue(TextoProperty);
		set => SetValue(TextoProperty, value);
	}

	public PanelTrazaView()
	{
		InitializeComponent();
	}
}
