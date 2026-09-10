using Station.Desktop.ViewModels;

namespace Station.Desktop.Tests;

public sealed class WindowLoadTests
{
    [Fact]
    public async Task Compiled_window_and_resources_load_on_sta_thread()
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var application = new App();
                application.InitializeComponent();
                var model = new MainWindowViewModel();
                var window = new MainWindow(model);
                foreach (var page in Enum.GetValues<DesktopPage>())
                {
                    model.NavigateCommand.Execute(page);
                    window.Measure(new System.Windows.Size(1040, 680));
                    window.Arrange(new System.Windows.Rect(0, 0, 1040, 680));
                    window.UpdateLayout();
                }
                window.Close();
                application.Shutdown();
                finished.SetResult();
            }
            catch (Exception exception) { finished.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
