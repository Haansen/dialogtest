using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;

namespace DialogTest.Services;

/// <summary>
/// Generisk dialog-öppnare för MudBlazor. En one-liner öppnar vilken dialog som helst:
///   var saved = await DialogOpener.OpenAsync&lt;CustomerDialog, Customer&gt;("Redigera", customer);
///
/// • Spärr per dialogtyp — samma dialog kan aldrig vara öppen dubbelt (avvisas med null),
///   men olika dialoger får vara öppna samtidigt. Fungerar även dialog-i-dialog.
/// • Parametrar: null, anonymous object (new { Id = 5 }), en modell-klass (mappas per
///   propertynamn), DialogParameters&lt;T&gt; eller dictionary.
/// • Retur: typad via TResult — vad dialogen än skickar i DialogResult.Ok(data).
/// • Storlek: DialogSize-preset som definieras centralt i DialogOpenerOptions.Presets.
/// </summary>
public interface IDialogOpener
{
    /// <summary>Öppnar en dialog. Null om den avvisas (samma typ redan öppen) eller avbryts.</summary>
    Task<DialogResult?> OpenAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent;

    /// <summary>Öppnar en dialog och returnerar ett typat resultat (default vid avvisning/avbrott).</summary>
    Task<TResult?> OpenAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent;
}

public sealed class DialogOpener : IDialogOpener
{
    private readonly IDialogService _dialogService;
    private readonly DialogOpenerOptions _config;

    // Typerna som är öppna just nu. TryAdd är atomiskt = spärren, helt utan SemaphoreSlim.
    private readonly ConcurrentDictionary<Type, byte> _openDialogs = new ConcurrentDictionary<Type, byte>();

    public DialogOpener(IDialogService dialogService, DialogOpenerOptions config)
    {
        _dialogService = dialogService;
        _config = config;
    }

    public Task<DialogResult?> OpenAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent
    {
        return OpenGuardedAsync<TDialog>(title, parameters, size, options, cancellationToken);
    }

    public async Task<TResult?> OpenAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        DialogSize size = DialogSize.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent
    {
        DialogResult? result = await OpenGuardedAsync<TDialog>(title, parameters, size, options, cancellationToken);

        if (result == null || result.Canceled)
        {
            return default;
        }

        if (result.Data is TResult typedData)
        {
            return typedData;
        }

        return default;
    }

    private async Task<DialogResult?> OpenGuardedAsync<TDialog>(
        string? title,
        object? parameters,
        DialogSize size,
        DialogOptions? options,
        CancellationToken cancellationToken)
        where TDialog : IComponent
    {
        bool wasAdded = _openDialogs.TryAdd(typeof(TDialog), 0);
        if (!wasAdded)
        {
            // Samma dialog är redan öppen — avvisa direkt
            return null;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            DialogParameters<TDialog> dialogParameters = BuildParameters<TDialog>(parameters);
            DialogOptions dialogOptions = options != null ? options : DialogOpenerOptions.Copy(_config.Presets[size]);

            IDialogReference dialog = await _dialogService.ShowAsync<TDialog>(
                title ?? string.Empty,
                dialogParameters,
                dialogOptions);

            return await dialog.Result;
        }
        finally
        {
            _openDialogs.TryRemove(typeof(TDialog), out _);
        }
    }

    private static DialogParameters<TDialog> BuildParameters<TDialog>(object? parameters)
        where TDialog : IComponent
    {
        if (parameters == null)
        {
            return new DialogParameters<TDialog>();
        }

        if (parameters is DialogParameters<TDialog> typedParameters)
        {
            return typedParameters;
        }

        if (parameters is IReadOnlyDictionary<string, object?> dictionary)
        {
            return FromDictionary<TDialog>(dictionary);
        }

        return MapByName<TDialog>(parameters);
    }

    private static DialogParameters<TDialog> FromDictionary<TDialog>(IReadOnlyDictionary<string, object?> dictionary)
        where TDialog : IComponent
    {
        DialogParameters<TDialog> result = new DialogParameters<TDialog>();

        foreach (KeyValuePair<string, object?> entry in dictionary)
        {
            result.Add(entry.Key, entry.Value);
        }

        return result;
    }

    // Mappar propertynamn på källobjektet mot dialogens [Parameter]-properties.
    // Properties som inte matchar ignoreras, så en hel modell-klass kan skickas in.
    private static DialogParameters<TDialog> MapByName<TDialog>(object source)
        where TDialog : IComponent
    {
        Dictionary<string, PropertyInfo> dialogParameters = typeof(TDialog)
            .GetProperties()
            .Where(p => p.CanWrite && p.GetCustomAttribute<ParameterAttribute>() != null)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        DialogParameters<TDialog> result = new DialogParameters<TDialog>();

        foreach (PropertyInfo property in source.GetType().GetProperties())
        {
            if (!property.CanRead)
            {
                continue;
            }

            if (dialogParameters.TryGetValue(property.Name, out PropertyInfo? target))
            {
                result.Add(target.Name, property.GetValue(source));
            }
        }

        return result;
    }
}

/// <summary>
/// Namngivna storleks-presets. Vad varje storlek faktiskt innebär (MaxWidth, css-klass,
/// backdrop-beteende osv) definieras centralt i DialogOpenerOptions.Presets.
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
/// Central konfiguration för DialogOpener — vad varje DialogSize innebär.
/// Ändra på ett ställe, gäller alla dialoger i appen:
///   builder.Services.AddDialogOpener(o => o.Presets[DialogSize.Medium].BackdropClick = false);
/// </summary>
public sealed class DialogOpenerOptions
{
    public Dictionary<DialogSize, DialogOptions> Presets { get; } = new Dictionary<DialogSize, DialogOptions>
    {
        [DialogSize.Small] = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = true,
            BackgroundClass = "dialog-backdrop"
        },
        [DialogSize.Medium] = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = true,
            BackgroundClass = "dialog-backdrop dialog-medium"
        },
        [DialogSize.Large] = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = false,
            BackgroundClass = "dialog-backdrop dialog-large"
        },
        [DialogSize.ExtraLarge] = new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraLarge,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = false,
            BackgroundClass = "dialog-backdrop dialog-xl"
        },
        [DialogSize.FullScreen] = new DialogOptions
        {
            FullScreen = true,
            CloseButton = true,
            CloseOnEscapeKey = false,
            BackdropClick = false,
            BackgroundClass = "dialog-backdrop"
        }
    };

    /// <summary>Kopierar en preset så att den delade instansen aldrig muteras per anrop.</summary>
    internal static DialogOptions Copy(DialogOptions source)
    {
        return new DialogOptions
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
}

public static class DialogOpenerServiceCollectionExtensions
{
    /// <summary>
    /// Registrerar IDialogOpener med centrala storleks-presets:
    ///   builder.Services.AddDialogOpener(o => o.Presets[DialogSize.Medium].BackdropClick = false);
    /// Presets delas som singleton; själva servicen (och spärrarna) är scoped per användare/krets.
    /// </summary>
    public static IServiceCollection AddDialogOpener(
        this IServiceCollection services,
        Action<DialogOpenerOptions>? configure = null)
    {
        DialogOpenerOptions options = new DialogOpenerOptions();
        configure?.Invoke(options);

        services.AddMudServices();
        services.AddSingleton(options);
        services.AddScoped<IDialogOpener, DialogOpener>();
        return services;
    }
}
