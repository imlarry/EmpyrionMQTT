using System;
using System.Windows.Input;

namespace EDNAClient.Helpers
{
    internal sealed class SimpleCommand : ICommand
    {
        private readonly Action _execute;
        public SimpleCommand(Action execute) { _execute = execute; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
