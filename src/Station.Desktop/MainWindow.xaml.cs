namespace Station.Desktop;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow(ViewModels.MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
