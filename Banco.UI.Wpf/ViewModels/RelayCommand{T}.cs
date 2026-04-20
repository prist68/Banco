using System.Windows.Input;

namespace Banco.UI.Wpf.ViewModels;

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(ConvertParameter(parameter)) ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute(ConvertParameter(parameter));
    }

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();

    private static T? ConvertParameter(object? parameter)
    {
        if (parameter is null)
        {
            return default;
        }

        if (parameter is T typedParameter)
        {
            return typedParameter;
        }

        return (T?)parameter;
    }
}
