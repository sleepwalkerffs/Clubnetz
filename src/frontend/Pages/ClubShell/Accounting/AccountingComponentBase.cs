using Bookennis.Client.Localization.Pages.Accounting;
using Bookennis.Client.Services.Store.Accounting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Bookennis.Client.Pages.ClubShell.Accounting;

/// <summary>
/// The parts of the finances page show what the store holds: they render again whenever it changes, and
/// <see cref="OnYearChangedAsync"/> lets them load the data of another fiscal year.
/// </summary>
public abstract class AccountingComponentBase : ComponentBase, IDisposable
{
    private int? yearId;

    [Inject] protected IAccountingStore Store { get; set; } = null!;
    [Inject] protected IStringLocalizer<AccountingLocale> L { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        Store.OnAccountingChanged += OnStoreChanged;
        yearId = Store.SelectedYear?.Id;
        await OnYearChangedAsync();
    }

    /// <summary>Loads what the part shows for <see cref="IAccountingStore.SelectedYear"/>.</summary>
    protected virtual Task OnYearChangedAsync() => Task.CompletedTask;

    private void OnStoreChanged() => InvokeAsync(async () =>
    {
        if (Store.SelectedYear?.Id != yearId)
        {
            yearId = Store.SelectedYear?.Id;
            await OnYearChangedAsync();
        }

        StateHasChanged();
    });

    public virtual void Dispose()
    {
        Store.OnAccountingChanged -= OnStoreChanged;
        GC.SuppressFinalize(this);
    }
}
