using CasinoTracker.Services;
using CasinoTracker.ViewModels;
using CasinoTracker.Views;
using Microsoft.Maui.Handlers;

namespace CasinoTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        RegisterServices(builder.Services);
        RegisterViewModelsAndPages(builder.Services);
        ConfigureHandlers();

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton(Preferences.Default);

        services.AddSingleton<IDatabaseService>(_ =>
            new DatabaseService(Path.Combine(FileSystem.AppDataDirectory, DatabaseService.FileName)));
        services.AddSingleton<IBackupService, BackupService>();
#if ANDROID
        services.AddSingleton<IFileTransferService, FileTransferService>();
#endif
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<ICasinoService, CasinoService>();
        services.AddSingleton<IMenuitemService, MenuitemService>();
        services.AddSingleton<IGameService, GameService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
    }

    private static void RegisterViewModelsAndPages(IServiceCollection services)
    {
        // Tab pages (Shell keeps one instance per tab)
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainPage>();
        services.AddTransient<SessionsViewModel>();
        services.AddTransient<SessionsPage>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<StatisticsPage>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();

        // Detail / edit pages (new instance per navigation)
        services.AddTransient<SessionDetailViewModel>();
        services.AddTransient<SessionDetailPage>();
        services.AddTransient<CasinoEditViewModel>();
        services.AddTransient<CasinoEditPage>();
        services.AddTransient<MenuitemEditViewModel>();
        services.AddTransient<MenuitemEditPage>();
        services.AddTransient<GameEditViewModel>();
        services.AddTransient<GameEditPage>();
    }

    /// <summary>
    /// Android specifics: removes the default underline of text inputs so they fit into the card
    /// design, and lets numeric entries accept both "." and "," as decimal separator.
    /// </summary>
    private static void ConfigureHandlers()
    {
#if ANDROID
        EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));

        // The stock numeric key listener only accepts the decimal separator of the device locale and
        // silently drops the other one ("6,50" becomes "650" on an English device). Every property
        // below re-applies the input type, so the tolerant listener is attached after each of them.
        foreach (var property in new[]
                 {
                     nameof(IEntry.Keyboard), nameof(IEntry.IsPassword),
                     nameof(IEntry.IsTextPredictionEnabled), nameof(IEntry.IsSpellCheckEnabled),
                 })
        {
            EntryHandler.Mapper.AppendToMapping(property, (handler, entry) =>
            {
                if (entry.Keyboard != Keyboard.Numeric) return;
                handler.PlatformView.KeyListener = Android.Text.Method.DigitsKeyListener.GetInstance("0123456789.,-");
                handler.PlatformView.SetRawInputType(
                    Android.Text.InputTypes.ClassNumber |
                    Android.Text.InputTypes.NumberFlagDecimal |
                    Android.Text.InputTypes.NumberFlagSigned);
            });
        }

        EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));

        PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif
    }
}
