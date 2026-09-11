using System.Reflection;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;

namespace DialogTest.Services;

/// <summary>
/// Namngivna storleks-presets för dialoger. Varje preset mappas mot ett komplett
/// DialogOptions-paket (storlek, css-klass, backdrop-beteende osv) i DialogOpenerOptions.Presets.
/// </summary>
public enum DialogSize
{
    Small,
    Medium,
    Large,
    ExtraLarge,
    FullScreen
}

/// <summary>
/// Central konfiguration för DialogOpener. Här definieras vad varje DialogSize
/// faktiskt innebär — ändra på ett ställe, gäller alla dialoger i appen.
/// Anpassa i Program.cs:
///   builder.Services.AddDialogOpener(o => o.Presets[DialogSize.Medium].BackdropClick = false);
/// </summary>
public sealed class DialogOpenerOptions
{
    public Dictionary<DialogSize, DialogOptions> Presets { get; } = new()
    {
        [DialogSize.Small] = new()
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = true,
            BackgroundClass = "dialog-backdrop"
        },
        [DialogSize.Medium] = new()
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = true,
            BackgroundClass = "dialog-backdrop dialog-medium"
        },
        [DialogSize.Large] = new()
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = false,
            BackgroundClass = "dialog-backdrop dialog-large"
        },
        [DialogSize.ExtraLarge] = new()
        {
            MaxWidth = MaxWidth.ExtraLarge,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = false,
            BackgroundClass = "dialog-backdrop dialog-xl"
        },
        [DialogSize.FullScreen] = new()
        {
            FullScreen = true,
            CloseButton = true,
            CloseOnEscapeKey = false,
            BackdropClick = false,
            BackgroundClass = "dialog-backdrop"
        }
    };

    /// <summary>Kopierar en preset så att delade instanser aldrig muteras per anrop.</summary>
    internal static DialogOptions Copy(DialogOptions source) => new()
    {
        MaxWidth = source.MaxWidth,
        FullWidth = source.FullWidth,
        FullScreen = source.FullScreen,
        CloseButton = source.CloseButton,
        CloseOnEscapeKey = source.CloseOnEscapeKey,
        BackdropClick = source.BackdropClick,
        Position = source.Position,
        NoHeader = source.NoHeader,
        BackgroundClass = source.BackgroundClass
    };
}

/// <summary>
/// Generisk dialog-öppnare för MudBlazor.
/// Toppnivå-dialoger (OpenAsync) spärras så att bara en kan vara öppen åt gången.
/// Dialoger som öppnas inifrån en annan dialog (OpenChildAsync) staplas utan spärr.
///
/// Parametrar kan skickas som:
///   null                        – inga parametrar
///   anonymous object            – new { ModelId = 5 } mappas per namn mot dialogens [Parameter]
///   valfri modell-klass         – matchande propertynamn mappas, övriga ignoreras
///   DialogParameters&lt;TDialog&gt;   – typ-säkert, används som det är
///   IReadOnlyDictionary         – nyckel/värde direkt
///
/// Storlek styrs via DialogSize-preset (centralt i DialogOpenerOptions.Presets).
/// </summary>
public interface IDialogOpener
{
    /// <summary>Öppnar en toppnivå-dialog. Köar bakom en eventuellt öppen dialog.</summary>
    Task<DialogResult?> OpenAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent;

    /// <summary>Öppnar en toppnivå-dialog och returnerar ett typat resultat (default vid avbrott).</summary>
    Task<TResult?> OpenAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent;

    /// <summary>Öppnar en barn-dialog inifrån en dialog. Staplas direkt, ingen spärr.</summary>
    Task<DialogResult?> OpenChildAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null)
        where TDialog : IComponent;

    /// <summary>Öppnar en barn-dialog inifrån en dialog och returnerar ett typat resultat.</summary>
    Task<TResult?> OpenChildAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null)
        where TDialog : IComponent;

    /// <summary>Enkel bekräftelsedialog. Fungerar både från pages och inifrån dialoger.</summary>
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Ja",
        string cancelText = "Avbryt",
        Color confirmColor = Color.Primary);
}

public sealed class DialogOpener : IDialogOpener
{
    private readonly IDialogService _dialogService;
    private readonly DialogOpenerOptions _config;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DialogOpener(IDialogService dialogService, DialogOpenerOptions config)
    {
        _dialogService = dialogService;
        _config = config;
    }

    public async Task<DialogResult?> OpenAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ShowCoreAsync<TDialog>(title, parameters, size, options);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TResult?> OpenAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent
    {
        var result = await OpenAsync<TDialog>(title, parameters, size, options, cancellationToken);
        return Unwrap<TResult>(result);
    }

    public Task<DialogResult?> OpenChildAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null)
        where TDialog : IComponent
        => ShowCoreAsync<TDialog>(title, parameters, size, options);

    public async Task<TResult?> OpenChildAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null)
        where TDialog : IComponent
    {
        var result = await ShowCoreAsync<TDialog>(title, parameters, size, options);
        return Unwrap<TResult>(result);
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Ja",
        string cancelText = "Avbryt",
        Color confirmColor = Color.Primary)
        => OpenChildAsync<Components.Dialogs.ConfirmDialog, bool>(title,
            new { Message = message, ConfirmText = confirmText, CancelText = cancelText, ConfirmColor = confirmColor },
            DialogSize.Small);

    private async Task<DialogResult?> ShowCoreAsync<TDialog>(
        string? title,
        object? parameters,
        DialogSize size,
        DialogOptions? options)
        where TDialog : IComponent
    {
        var dialog = await _dialogService.ShowAsync<TDialog>(
            title ?? string.Empty,
            BuildParameters<TDialog>(parameters),
            ResolveOptions(size, options));
        return await dialog.Result;
    }

    // Explicit options vinner alltid; annars en färsk kopia av den centrala preseten.
    private DialogOptions ResolveOptions(DialogSize size, DialogOptions? options)
        => options ?? DialogOpenerOptions.Copy(_config.Presets[size]);

    private static DialogParameters<TDialog> BuildParameters<TDialog>(object? parameters)
        where TDialog : IComponent
    {
        switch (parameters)
        {
            case null:
                return new DialogParameters<TDialog>();
            case DialogParameters<TDialog> typed:
                return typed;
            case IReadOnlyDictionary<string, object?> dictionary:
                var fromDictionary = new DialogParameters<TDialog>();
                foreach (var (key, value) in dictionary)
                    fromDictionary.Add(key, value);
                return fromDictionary;
            default:
                return MapByName<TDialog>(parameters);
        }
    }

    // Mappar propertynamn på källobjektet mot dialogens [Parameter]-properties.
    // Properties som inte matchar ignoreras, så en hel modell-klass kan skickas in.
    private static DialogParameters<TDialog> MapByName<TDialog>(object source)
        where TDialog : IComponent
    {
        var dialogParameters = typeof(TDialog).GetProperties()
            .Where(p => p.CanWrite && p.GetCustomAttribute<ParameterAttribute>() is not null)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var mapped = new DialogParameters<TDialog>();
        foreach (var property in source.GetType().GetProperties())
        {
            if (property.CanRead && dialogParameters.TryGetValue(property.Name, out var target))
                mapped.Add(target.Name, property.GetValue(source));
        }
        return mapped;
    }

    private static TResult? Unwrap<TResult>(DialogResult? result)
    {
        if (result is null || result.Canceled)
            return default;
        return result.Data is TResult data ? data : default;
    }
}

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
