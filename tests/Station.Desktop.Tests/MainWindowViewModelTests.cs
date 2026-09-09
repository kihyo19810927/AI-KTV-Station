using Station.Desktop.ViewModels;

namespace Station.Desktop.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Defines_all_v1_navigation_destinations()
    {
        var viewModel = new MainWindowViewModel();
        Assert.Equal(Enum.GetValues<DesktopPage>(), viewModel.NavigationItems.Select(item => item.Page));
        Assert.Equal(DesktopPage.Dashboard, viewModel.CurrentPage);
        Assert.Equal("仪表盘", viewModel.CurrentTitle);
    }

    [Fact]
    public void Navigation_command_updates_current_page_and_display_properties()
    {
        var viewModel = new MainWindowViewModel();
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.NavigateCommand.Execute(DesktopPage.Room);

        Assert.Equal(DesktopPage.Room, viewModel.CurrentPage);
        Assert.Equal("房间与二维码", viewModel.CurrentTitle);
        Assert.Contains(nameof(MainWindowViewModel.CurrentPage), changes);
        Assert.Contains(nameof(MainWindowViewModel.CurrentTitle), changes);
        Assert.Contains(nameof(MainWindowViewModel.CurrentDescription), changes);
    }
}
