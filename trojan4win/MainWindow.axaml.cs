using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using trojan4win.ViewModels;

namespace trojan4win;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = 36;

        // Enforce minimum size after title bar extension
        MinWidth = 580;
        MinHeight = 420;

        // Commit edits and clear focus from DataGrid cells when user clicks outside the grid
        AddHandler(InputElement.PointerPressedEvent, OnGlobalPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var grid = this.FindControl<DataGrid>("RouterRulesGrid");
        if (grid == null) return;
        if (e.Source is not Visual src) return;
        // If click happened inside the grid, do nothing
        if (src == grid || src.FindAncestorOfType<DataGrid>() == grid) return;
        // If click happened inside a popup (e.g. ComboBox dropdown opened from a cell editor), do nothing
        if (src.FindAncestorOfType<Avalonia.Controls.Primitives.PopupRoot>() != null) return;
        // Click is outside the grid: commit any pending edit and drop focus from cell editors
        try { grid.CommitEdit(); } catch { /* no active edit */ }
        if (FocusManager?.GetFocusedElement() is Visual focused &&
            (focused == grid || focused.FindAncestorOfType<DataGrid>() == grid))
        {
            Focus();
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _vm = DataContext as MainViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            await _vm.AutoConnectIfNeededAsync();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.TrojanLog))
        {
            Dispatcher.UIThread.Post(() =>
            {
                var tb = this.FindControl<TextBox>("TrojanLogBox");
                if (tb != null) tb.CaretIndex = int.MaxValue;
            }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(MainViewModel.ProxifyreLog))
        {
            Dispatcher.UIThread.Post(() =>
            {
                var tb = this.FindControl<TextBox>("ProxifyreLogBox");
                if (tb != null) tb.CaretIndex = int.MaxValue;
            }, DispatcherPriority.Background);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty &&
            change.NewValue is WindowState state &&
            state == WindowState.Minimized &&
            _vm is { MinimizeToTray: true })
        {
            Hide();
            WindowState = WindowState.Normal;
        }

        // Enforce minimum size — workaround for ExtendClientAreaToDecorationsHint
        // not always respecting MinWidth/MinHeight at non-100% DPI scaling
        if (change.Property == ClientSizeProperty && change.NewValue is Size size)
        {
            bool snap = false;
            double w = size.Width, h = size.Height;
            if (w > 0 && w < MinWidth) { w = MinWidth; snap = true; }
            if (h > 0 && h < MinHeight) { h = MinHeight; snap = true; }
            if (snap)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Width = w;
                    Height = h;
                });
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm?.Dispose();
        base.OnClosing(e);
    }
}