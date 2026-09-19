using System.Windows;

namespace Transcry.Views;

public partial class CompletionWindow : Window
{
  public CompletionWindow()
  {
    InitializeComponent();
  }

  private void Close_Click(object sender, RoutedEventArgs e)
  {
    Close();
  }
}
