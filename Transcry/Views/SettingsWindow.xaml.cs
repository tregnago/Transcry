using System.Windows;
using Transcry.Services;
using Transcry.ViewModels;

namespace Transcry.Views;

public partial class SettingsWindow : Window
{
  private readonly SettingsViewModel _viewModel;

  public SettingsWindow(ISettingsService settingsService)
  {
    InitializeComponent();
    _viewModel = new SettingsViewModel(settingsService);
    DataContext = _viewModel;
    ApiKeyBox.Password = _viewModel.ApiKey;
  }

  private void Save_Click(object sender, RoutedEventArgs e)
  {
    _viewModel.ApiKey = ApiKeyBox.Password;

    if (_viewModel.TrySave())
    {
      DialogResult = true;
      Close();
    }
  }

  private void Cancel_Click(object sender, RoutedEventArgs e)
  {
    DialogResult = false;
    Close();
  }
}
