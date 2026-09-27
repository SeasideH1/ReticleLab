using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Reticle.Widget;
sealed partial class App : Application
{
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<int, WeakReference<OverlayPage>> Views = new();
    public App() {
        Store.Startup("Application constructor entered");
        UnhandledException += (_, args) => Store.Log(args.Exception);
        Suspending += async (_, args) => {
            var deferral = args.SuspendingOperation.GetDeferral();
            try {
                foreach (var pair in Views.ToArray()) if (pair.Value.TryGetTarget(out var page)) {
                    try { await page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, page.Suspend); } catch (Exception e) { Store.Log(e); }
                }
            } finally { deferral.Complete(); }
        };
        Resuming += async (_, _) => {
            foreach(var pair in Views.ToArray()) if(pair.Value.TryGetTarget(out var page)) {
                try { await page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,page.Resume); } catch(Exception e) {Store.Log(e);}
            }
        };
    }
    // UWP Application.Start must be called from MTA; it creates the XAML UI thread.
    [MTAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => {
            if (args.ExceptionObject is Exception error) Store.Log(error);
        };
        try {
            Store.Startup("Main; apartment=" + Thread.CurrentThread.GetApartmentState());
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(InitializeApplication);
        } catch (Exception error) { Store.Log(error); throw; }
    }
    // No CoreWindow/Window/dispatcher access here: a view is not available yet.
    // Install the per-view context in OnLaunched/OnActivated and async handlers.
    static void InitializeApplication(ApplicationInitializationCallbackParams args)
    {
        try {
            Store.Startup("XAML initialization callback");
            _ = new App();
            Store.Startup("Application created");
        } catch (Exception error) { Store.Log(error); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        UiThread.Attach(Window.Current.Dispatcher);
        Store.Startup("Settings launch");
        Window.Current.Content ??= new SettingsPage();
        Window.Current.Activate();
        Store.Startup("Settings activated");
    }
    protected override void OnActivated(IActivatedEventArgs args)
    {
        UiThread.Attach(Window.Current.Dispatcher);
        Store.Startup("Activation: " + args.Kind);
        if (args is not XboxGameBarWidgetActivatedEventArgs activation) return;
        if (!activation.IsLaunchActivation) { Window.Current.Activate(); return; }
        var window = Window.Current;
        var frame = new Frame(); window.Content = frame;
        var widget = new XboxGameBarWidget(activation, window.CoreWindow, frame);
        if (activation.AppExtensionId == "Settings") frame.Content = new SettingsPage(widget);
        else frame.Content = new OverlayPage(widget);
        window.Activate();
        Store.Startup("Widget activated: " + activation.AppExtensionId);
    }
}
