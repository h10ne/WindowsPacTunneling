using System.Windows.Input;
using System.Windows.Threading;

namespace WPT.Wpf.ViewModels;

public sealed class RelayCommand : ICommand
{
    private static readonly List<WeakReference<RelayCommand>> Commands = [];

    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
        lock (Commands)
        {
            Commands.Add(new WeakReference<RelayCommand>(this));
        }
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => RunOnUiThread(RaiseCanExecuteChangedCore);

    public static void RaiseAllCanExecuteChanged() => RunOnUiThread(RaiseAllCanExecuteChangedCore);

    private void RaiseCanExecuteChangedCore() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static void RaiseAllCanExecuteChangedCore()
    {
        lock (Commands)
        {
            for (var i = Commands.Count - 1; i >= 0; i--)
            {
                if (!Commands[i].TryGetTarget(out var command))
                {
                    Commands.RemoveAt(i);
                    continue;
                }

                command.RaiseCanExecuteChangedCore();
            }
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action, DispatcherPriority.DataBind);
    }

}
