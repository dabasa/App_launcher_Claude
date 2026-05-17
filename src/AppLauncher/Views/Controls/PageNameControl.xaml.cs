using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class PageNameControl : UserControl
{
    private LauncherViewModel? _vm;
    private bool _isEditing;

    public PageNameControl()
    {
        InitializeComponent();
        DataContextChanged           += OnDataContextChanged;
        TextLabel.MouseLeftButtonUp  += OnTextLabelClicked;
        EditTextBox.KeyDown          += OnEditTextBoxKeyDown;
        EditTextBox.LostFocus        += (_, _) => CommitEdit();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = DataContext as LauncherViewModel;
        if (_vm != null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode))
            HandleModeChange();
        else if (e.PropertyName is nameof(LauncherViewModel.CurrentPageIndex)
                                or nameof(LauncherViewModel.CurrentPage))
            CommitEdit();
    }

    private void HandleModeChange()
    {
        if (_vm?.Mode == AppMode.Edit)
        {
            TextLabel.Cursor = Cursors.IBeam;
        }
        else
        {
            CommitEdit();
            TextLabel.Cursor = Cursors.Arrow;
        }
    }

    private void OnTextLabelClicked(object sender, MouseButtonEventArgs e)
    {
        if (_vm?.Mode != AppMode.Edit) return;
        _isEditing = true;
        EditTextBox.Text       = _vm.CurrentPage.Name;
        TextLabel.Visibility   = Visibility.Collapsed;
        EditTextBox.Visibility = Visibility.Visible;
        EditTextBox.Focus();
        EditTextBox.SelectAll();
    }

    private void OnEditTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitEdit();
    }

    private void CommitEdit()
    {
        if (!_isEditing) return;
        if (_vm != null)
            _vm.CurrentPage.Name = EditTextBox.Text;
        _isEditing             = false;
        EditTextBox.Visibility = Visibility.Collapsed;
        TextLabel.Visibility   = Visibility.Visible;
    }
}
