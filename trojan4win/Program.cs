using System;
using System.Threading;
using Avalonia;

namespace trojan4win
{
    internal class Program
    {
        // Named mutex used by the Inno Setup installer (AppMutex directive)
        // to detect whether the application is running during install/uninstall.
        private static Mutex? _appMutex;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            _appMutex = new Mutex(false, "trojan4win_app_mutex");
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
