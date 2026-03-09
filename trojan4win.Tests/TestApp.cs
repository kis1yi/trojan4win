using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(trojan4win.Tests.TestApp))]

namespace trojan4win.Tests;

public class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>();
}

