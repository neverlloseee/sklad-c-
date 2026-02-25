using System.Collections;
using System.Windows;
using System.Windows.Threading;

namespace WarehouseTimesheetApp;

public partial class App : Application
{
    public App()
    {
        Startup += OnStartup;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    private static void OnStartup(object sender, StartupEventArgs e)
    {
        ErrorLogger.LogMessage(
            "App.Startup",
            $"Приложение запущено. Версия .NET: {Environment.Version}; OS: {Environment.OSVersion}");
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLogger.LogException("App.DispatcherUnhandledException", e.Exception);

        MessageBox.Show(
            $"Произошла ошибка. Подробности сохранены в лог:\n{ErrorLogger.LogFilePath}",
            "Ошибка приложения",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var context = new Hashtable
        {
            ["IsTerminating"] = e.IsTerminating
        };

        if (e.ExceptionObject is Exception exception)
        {
            ErrorLogger.LogException("AppDomain.CurrentDomain.UnhandledException", exception, context);
            return;
        }

        ErrorLogger.LogMessage(
            "AppDomain.CurrentDomain.UnhandledException",
            $"Неизвестный объект исключения: {e.ExceptionObject}");
    }

    private static void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ErrorLogger.LogException("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}
