using IBEBarcode.Scanner.ViewModels;

namespace IBEBarcode.Scanner.Views;

public partial class NfcPage : ContentPage
{
    public NfcPage(NfcViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
