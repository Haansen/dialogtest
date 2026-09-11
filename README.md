# DialogTest

Exempelprojekt som visar en **generisk, spärrad dialog-öppnare för MudBlazor** (`IDialogOpener`).
Målet: en rad på pagen för att öppna en dialog och få ett typat resultat tillbaka.

## Kör

```bash
dotnet run
```

Öppna `http://localhost:5135/customers`.

## Idén

| Metod | Används från | Spärr |
|---|---|---|
| `OpenAsync<TDialog>` / `OpenAsync<TDialog, TResult>` | Pages (toppnivå) | Ja – bara en åt gången, anrop köar |
| `OpenChildAsync<TDialog>` / `OpenChildAsync<TDialog, TResult>` | Inifrån en dialog | Nej – staplas direkt ovanpå |
| `ConfirmAsync(title, message, ...)` | Överallt | Nej |

Spärren är en `SemaphoreSlim` i en scoped service (`Services/DialogOpener.cs`).
Toppnivå-anrop köar bakom en öppen dialog (skydd mot dubbelklick m.m.), medan
barn-dialoger medvetet går förbi spärren så att de kan staplas — MudBlazor stödjer
staplade dialoger nativt.

## Användning

```razor
@inject IDialogOpener DialogOpener

@code {
    // One-liner: öppna dialog, typat resultat, null = användaren avbröt
    private async Task EditCustomer(Customer customer)
    {
        var saved = await DialogOpener.OpenAsync<CustomerDialog, Customer>(
            title: "Redigera kund",
            parameters: new DialogParameters<CustomerDialog> { { x => x.CustomerId, customer.Id } });

        if (saved is null) return;
        // ...
    }

    // Bekräftelse utan egen komponent
    private async Task DeleteCustomer(Customer customer)
    {
        if (await DialogOpener.ConfirmAsync("Ta bort", $"Ta bort {customer.Name}?",
                confirmText: "Ta bort", confirmColor: Color.Error))
        {
            // ...
        }
    }
}
```

Inifrån en dialog:

```razor
@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    private async Task PickAddress()
    {
        var address = await DialogOpener.OpenChildAsync<AddressPickerDialog, Address>(title: "Välj adress");
        if (address is not null)
            _model.Address = address;
    }

    private void Save() => MudDialog.Close(DialogResult.Ok(_model));
}
```

## Kontraktet mellan page och dialog

- **In**: dialogen exponerar `[Parameter]`-properties som sätts via typ-säkra `DialogParameters<TDialog>`.
- **Ut**: dialogen anropar `MudDialog.Close(DialogResult.Ok(data))`; `MudDialog.Cancel()`/Esc/kryss ger `null` tillbaka.

## Struktur

```
Services/DialogOpener.cs              – IDialogOpener + implementation (spärren)
Components/Dialogs/ConfirmDialog.razor – återanvändbar bekräftelsedialog
Components/Dialogs/CustomerDialog.razor – exempel: redigera kund (öppnar barn-dialog)
Components/Dialogs/AddressPickerDialog.razor – exempel på barn-dialog
Components/Pages/Customers.razor       – exempel-page
```
