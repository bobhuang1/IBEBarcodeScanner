using BarcodeScanning;
using IBEBarcode.Scanner.ViewModels;

namespace IBEBarcode.Scanner.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _viewModel;

    public ScanPage(ScanViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;

        // The captured-frame event is subscribed rather than bound, because the matching command is handed a
        // bare PlatformImage that a command typed for OnImageCapturedEventArg refuses. Handing the frame to
        // the view model is a view concern; decoding it is not.
        Camera.OnImageCaptured += OnImageCaptured;
    }

    private void OnImageCaptured(object? sender, OnImageCapturedEventArg e)
        => _viewModel.OnFrameCaptured(e);

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!await _viewModel.EnsureCameraPermissionAsync())
            return;

        _viewModel.CameraEnabled = true;
    }

    protected override void OnDisappearing()
    {
        // Stop the scan timer and release the camera so another tab can use neither.
        _viewModel.Suspend();
        base.OnDisappearing();
    }
}
