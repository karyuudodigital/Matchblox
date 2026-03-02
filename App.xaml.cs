using System;
using System.IO;
using System.Windows;

namespace DiamondSword
{
    public partial class App : System.Windows.Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogError(ex);
            }
            else
            {
                LogRaw(e.ExceptionObject?.ToString() ?? "Unknown exception object");
            }
        }

        private void App_DispatcherUnhandledException(
            object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            e.Handled = true; // prevents immediate crash (optional)
        }

        private void LogError(Exception ex)
        {
            try
            {
                string basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DiamondSword");

                Directory.CreateDirectory(basePath);

                string logPath = Path.Combine(basePath, "crashlog.txt");

                File.AppendAllText(logPath,
                    DateTime.Now + Environment.NewLine +
                    ex.ToString() + Environment.NewLine +
                    "----------------------------------" + Environment.NewLine);
            }
            catch
            {
                // Never crash while logging
            }
        }

        private void LogRaw(string message)
        {
            try
            {
                string basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DiamondSword");

                Directory.CreateDirectory(basePath);

                string logPath = Path.Combine(basePath, "crashlog.txt");

                File.AppendAllText(logPath,
                    DateTime.Now + Environment.NewLine +
                    message + Environment.NewLine +
                    "----------------------------------" + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}