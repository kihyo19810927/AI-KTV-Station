using System.Windows.Input;

namespace Station.Desktop.ViewModels;

public sealed class AsyncRelayCommand<T>(Func<T, Task> execute, Predicate<T>? canExecute = null) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke((T)parameter!) ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute((T)parameter!); } finally { running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
