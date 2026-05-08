using System;
using System.Windows;

namespace EDNAClient.Helpers
{
    internal static class UI
    {
        public static void Invoke(Action action)
            => Application.Current.Dispatcher.Invoke(action);

        public static void InvokeAsync(Action action)
            => Application.Current.Dispatcher.InvokeAsync(action);
    }
}
