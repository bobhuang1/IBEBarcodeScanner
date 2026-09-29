using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IBEBarcode.Scanner.Resources.Strings;
using IBEBarcode.Scanner.Services;

namespace IBEBarcode.Scanner.ViewModels;

/// <summary>
/// The session's scans. Nothing is persisted: closing the app is the delete button, which is what "no
/// database" means in practice.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ScanHistoryService _history;
    private readonly ScanResultActions _actions;

    public HistoryViewModel(ScanHistoryService history, ScanResultActions actions)
    {
        _history = history;
        _actions = actions;

        _history.Changed += OnHistoryChanged;
        Rebuild();
    }

    public ObservableCollection<ScanResultViewModel> Items { get; } = [];

    public string Title => AppResources.HistoryTitle;

    public string ClearButtonText => AppResources.ClearHistoryButton;

    public string EmptyText => AppResources.HistoryEmpty;

    public string NoDatabaseNote => AppResources.NoDatabaseNote;

    public bool IsEmpty => Items.Count == 0;

    public bool HasItems => Items.Count > 0;

    public string CountText => string.Format(AppResources.HistoryCount, Items.Count);

    [RelayCommand]
    private void Clear()
    {
        _history.Clear();
        Rebuild();
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(Rebuild);

    private void Rebuild()
    {
        Items.Clear();

        foreach (var entry in _history.Entries)
        {
            Items.Add(new ScanResultViewModel(entry, _actions));
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CountText));
    }
}
