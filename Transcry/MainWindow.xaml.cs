using System.Windows;
using Transcry.Services;
using Transcry.ViewModels;

namespace Transcry;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();

    var settingsService = new SettingsService();
    var transcriptionService = new WhisperTranscriptionService();
    DataContext = new MainViewModel(settingsService, transcriptionService);
  }
}
