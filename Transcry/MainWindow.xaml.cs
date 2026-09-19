using System.Windows;
using System.Windows.Threading;
using Transcry.Services;
using Transcry.ViewModels;
using Transcry.Views;

namespace Transcry;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();

    var settingsService = new SettingsService();
    var transcriptionService = new WhisperTranscriptionService();
    var viewModel = new MainViewModel(settingsService, transcriptionService);
    viewModel.TranscriptionCompleted += OnTranscriptionCompleted;
    DataContext = viewModel;
  }

  private void OnTranscriptionCompleted(object? sender, EventArgs e)
  {
    if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
    {
      return;
    }

    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
    {
      if (IsVisible)
      {
        new CompletionWindow { Owner = this }.ShowDialog();
      }
    }));
  }
}
