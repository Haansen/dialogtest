using MudBlazor.Services;

namespace DialogTest.Services;

public static class DialogOpenerServiceCollectionExtensions
{
    /// <summary>
    /// Registrerar IDialogOpener med centrala storleks-presets.
    /// Anpassa presets här om appens standarder skiljer sig från default:
    ///   builder.Services.AddDialogOpener(o => o.Presets[DialogSize.Medium].BackdropClick = false);
    /// </summary>
    public static IServiceCollection AddDialogOpener(
        this IServiceCollection services,
        Action<DialogOpenerOptions>? configure = null)
    {
        var options = new DialogOpenerOptions();
        configure?.Invoke(options);

        services.AddMudServices();
        services.AddSingleton(options);
        services.AddScoped<IDialogOpener, DialogOpener>();
        return services;
    }
}
