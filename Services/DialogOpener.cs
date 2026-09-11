using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DialogTest.Services;

/// <summary>
/// Generisk dialog-öppnare för MudBlazor.
/// Toppnivå-dialoger (OpenAsync) spärras så att bara en kan vara öppen åt gången.
/// Dialoger som öppnas inifrån en annan dialog (OpenChildAsync) staplas utan spärr.
/// </summary>
public interface IDialogOpener
{
    /// <summary>Öppnar en toppnivå-dialog. Köar bakom en eventuellt öppen dialog.</summary>
    Task<DialogResult?> OpenAsync<TComponent>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent;

    /// <summary>Öppnar en toppnivå-dialog och returnerar ett typat resultat (default vid avbrott).</summary>
    Task<TResult?> OpenAsync<TComponent, TResult>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent;

    /// <summary>Öppnar en barn-dialog inifrån en dialog. Staplas direkt, ingen spärr.</summary>
    Task<DialogResult?> OpenChildAsync<TComponent>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null)
        where TComponent : IComponent;

    /// <summary>Öppnar en barn-dialog inifrån en dialog och returnerar ett typat resultat.</summary>
    Task<TResult?> OpenChildAsync<TComponent, TResult>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null)
        where TComponent : IComponent;

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

    public async Task<DialogResult?> OpenAsync<TComponent>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ShowCoreAsync<TComponent>(title, parameters, options);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TResult?> OpenAsync<TComponent, TResult>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        var result = await OpenAsync<TComponent>(title, parameters, options, cancellationToken);
        return Unwrap<TResult>(result);
    }

    public Task<DialogResult?> OpenChildAsync<TComponent>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null)
        where TComponent : IComponent
        => ShowCoreAsync<TComponent>(title, parameters, options);

    public async Task<TResult?> OpenChildAsync<TComponent, TResult>(
        string? title = null,
        DialogParameters<TComponent>? parameters = null,
        DialogOptions? options = null)
        where TComponent : IComponent
    {
        var result = await ShowCoreAsync<TComponent>(title, parameters, options);
        return Unwrap<TResult>(result);
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Ja",
        string cancelText = "Avbryt",
        Color confirmColor = Color.Primary)
    {
        var parameters = new DialogParameters<Components.Dialogs.ConfirmDialog>
        {
            { x => x.Message, message },
            { x => x.ConfirmText, confirmText },
            { x => x.CancelText, cancelText },
            { x => x.ConfirmColor, confirmColor }
        };

        return OpenChildAsync<Components.Dialogs.ConfirmDialog, bool>(title, parameters);
    }

    private async Task<DialogResult?> ShowCoreAsync<TComponent>(
        string? title,
        DialogParameters<TComponent>? parameters,
        DialogOptions? options)
        where TComponent : IComponent
    {
        var dialog = await _dialogService.ShowAsync<TComponent>(
            title ?? string.Empty, parameters ?? new DialogParameters<TComponent>(), options);
        return await dialog.Result;
    }

    private static TResult? Unwrap<TResult>(DialogResult? result)
    {
        if (result is null || result.Canceled)
            return default;
        return result.Data is TResult data ? data : default;
    }
}
