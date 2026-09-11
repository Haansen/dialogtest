using System.Reflection;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DialogTest.Services;

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
/// </summary>
public interface IDialogOpener
{
    /// <summary>Öppnar en toppnivå-dialog. Köar bakom en eventuellt öppen dialog.</summary>
    Task<DialogResult?> OpenAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent;

    /// <summary>Öppnar en toppnivå-dialog och returnerar ett typat resultat (default vid avbrott).</summary>
    Task<TResult?> OpenAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent;

    /// <summary>Öppnar en barn-dialog inifrån en dialog. Staplas direkt, ingen spärr.</summary>
    Task<DialogResult?> OpenChildAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        DialogOptions? options = null)
        where TDialog : IComponent;

    /// <summary>Öppnar en barn-dialog inifrån en dialog och returnerar ett typat resultat.</summary>
    Task<TResult?> OpenChildAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
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
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DialogOpener(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task<DialogResult?> OpenAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ShowCoreAsync<TDialog>(title, parameters, maxWidth, options);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TResult?> OpenAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TDialog : IComponent
    {
        var result = await OpenAsync<TDialog>(title, parameters, maxWidth, options, cancellationToken);
        return Unwrap<TResult>(result);
    }

    public Task<DialogResult?> OpenChildAsync<TDialog>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        DialogOptions? options = null)
        where TDialog : IComponent
        => ShowCoreAsync<TDialog>(title, parameters, maxWidth, options);

    public async Task<TResult?> OpenChildAsync<TDialog, TResult>(
        string? title = null,
        object? parameters = null,
        MaxWidth maxWidth = MaxWidth.Medium,
        DialogOptions? options = null)
        where TDialog : IComponent
    {
        var result = await ShowCoreAsync<TDialog>(title, parameters, maxWidth, options);
        return Unwrap<TResult>(result);
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Ja",
        string cancelText = "Avbryt",
        Color confirmColor = Color.Primary)
        => OpenChildAsync<Components.Dialogs.ConfirmDialog, bool>(title,
            new { Message = message, ConfirmText = confirmText, CancelText = cancelText, ConfirmColor = confirmColor });

    private async Task<DialogResult?> ShowCoreAsync<TDialog>(
        string? title,
        object? parameters,
        MaxWidth maxWidth,
        DialogOptions? options)
        where TDialog : IComponent
    {
        var dialog = await _dialogService.ShowAsync<TDialog>(
            title ?? string.Empty,
            BuildParameters<TDialog>(parameters),
            options ?? DefaultOptions(maxWidth));
        return await dialog.Result;
    }

    private static DialogOptions DefaultOptions(MaxWidth maxWidth) => new()
    {
        MaxWidth = maxWidth,
        FullWidth = true,
        CloseButton = true,
        CloseOnEscapeKey = true
    };

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
