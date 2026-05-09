using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using Sietch_Console.ViewModels;
using SietchConsole.Core.Models;

namespace Sietch_Console.Views;

public partial class LogsView : UserControl
{
    private LogsViewModel? _vm;
    private ObservableCollection<LogEntry>? _entries;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as LogsViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            SubscribeToEntries(_vm.FilteredEntries);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LogsViewModel.FilteredEntries) || _vm is null) return;

        SubscribeToEntries(_vm.FilteredEntries);

        if (_vm.AutoScroll && LogListBox.Items.Count > 0)
            LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
    }

    private void SubscribeToEntries(ObservableCollection<LogEntry> entries)
    {
        if (_entries is not null)
            _entries.CollectionChanged -= OnEntriesChanged;

        _entries = entries;
        _entries.CollectionChanged += OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm?.AutoScroll != true) return;
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (LogListBox.Items.Count > 0)
            LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
    }
}
