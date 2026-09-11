using MudBlazor;

namespace DialogTest.Services;

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
