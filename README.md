# DialogTest

Exempelprojekt som visar en **generisk, spärrad dialog-öppnare för MudBlazor** (`IDialogOpener`).
Målet: **en rad på pagen** för att öppna vilken dialog som helst — med eller utan parametrar —
och få tillbaka ett typat resultat.

## Kör

```bash
dotnet run
```

Öppna `http://localhost:5135/customers`.

## One-linern — alla varianter

```csharp
@inject IDialogOpener DialogOpener

// 1. Utan parametrar, utan returvärde (medium storlek är default)
await DialogOpener.OpenAsync<ApaDialog>("Apa");

// 2. Parametrar som anonymous object – mappas per namn mot dialogens [Parameter]
var id = await DialogOpener.OpenAsync<ApaDialog, int>("Apa", new { ModelId = model.Id });

// 3. En hel modell-klass – matchande propertynamn mappas, övriga ignoreras
var saved = await DialogOpener.OpenAsync<ApaDialog, Customer>("Apa", customer);

// 4. Annan storlek
await DialogOpener.OpenAsync<ApaDialog>("Apa", maxWidth: MaxWidth.Large);

// 5. Full kontroll när det behövs (typ-säkert alternativ)
var r = await DialogOpener.OpenAsync<ApaDialog, Customer>("Apa",
    parameters: new DialogParameters<ApaDialog> { { x => x.ModelId, model.Id } },
    options: new DialogOptions { BackdropClick = false });

// 6. Returtypen är vad dialogen än returnerar – int, bool, modell, record ...
int number      = await DialogOpener.OpenAsync<NumberDialog, int>("Välj tal");
Customer cust   = await DialogOpener.OpenAsync<CustomerDialog, Customer>("Redigera", customer);
bool confirmed  = await DialogOpener.ConfirmAsync("Ta bort", "Säker?");
```

`null`/`default` tillbaka betyder alltid att användaren avbröt (Esc, krysset, Avbryt).

## Parametermappning

`parameters` accepterar:

| Typ | Beteende |
|---|---|
| `null` | Inga parametrar |
| anonymous object | `new { ModelId = 5 }` → dialogens `[Parameter] ModelId` |
| modell-klass | Propertynamn matchas (case-insensitivt) mot dialogens `[Parameter]`; resten ignoreras |
| `DialogParameters<TDialog>` | Typ-säkert, används som det är |
| `IReadOnlyDictionary<string, object?>` | Nyckel/värde direkt |

## Spärren

| Metod | Används från | Spärr |
|---|---|---|
| `OpenAsync` | Pages (toppnivå) | Ja – bara en åt gången, anrop köar |
| `OpenChildAsync` | Inifrån en dialog | Nej – staplas direkt ovanpå |
| `ConfirmAsync` | Överallt | Nej |

Spärren är en `SemaphoreSlim` i den scoped servicen `Services/DialogOpener.cs`.
Toppnivå-anrop köar bakom en öppen dialog (skydd mot dubbelklick m.m.), medan
barn-dialoger medvetet går förbi spärren — MudBlazor stödjer staplade dialoger nativt.

## Dialog-kontraktet

```razor
@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    // In: [Parameter]-properties (sätts av opencern)
    [Parameter] public int Id { get; set; }

    // Ut: DialogResult.Ok(data) – typen bestämmer TResult på pagen
    private void Save() => MudDialog.Close(DialogResult.Ok(_model));
    private void Cancel() => MudDialog.Cancel();
}
```

## Struktur

```
Services/DialogOpener.cs               – IDialogOpener + implementation (spärr + mappning)
Components/Dialogs/ConfirmDialog.razor – återanvändbar bekräftelsedialog
Components/Dialogs/CustomerDialog.razor – exempel: redigera kund (öppnar barn-dialog)
Components/Dialogs/AddressPickerDialog.razor – exempel på barn-dialog
Components/Pages/Customers.razor       – exempel-page
```
