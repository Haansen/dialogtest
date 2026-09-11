namespace DialogTest.Services;

/// <summary>
/// Namngivna storleks-presets för dialoger. Varje preset mappas mot ett komplett
/// DialogOptions-paket (storlek, css-klass, backdrop-beteende osv) som definieras
/// centralt — se DialogOpenerOptions.Presets.
/// </summary>
public enum DialogSize
{
    Small,
    Medium,
    Large,
    ExtraLarge,
    FullScreen
}
