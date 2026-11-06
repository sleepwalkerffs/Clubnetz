using Bookennis.Client.Localization;
using Bookennis.Client.Localization.Pages.Accounting;
using Bookennis.Client.Services.Store.Accounting;
using Bookennis.Client.Utils;
using Bookennis.Shared.Controller.Accounting;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;

namespace Bookennis.Client.Pages.ClubShell.Accounting;

/// <summary>Opens the dialogs of the accounting the same way from every part of the page.</summary>
public static class AccountingDialogs
{
    private static readonly DialogOptions Wide = new() { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
    private static readonly DialogOptions Narrow = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };

    /// <summary>Adds an entry (<paramref name="entry"/> = null) or changes one; <paramref name="receiptIds"/> are attached to a new entry.</summary>
    public static async Task OpenEntry(IDialogService dialogService, IStringLocalizer<AccountingLocale> locale, AccountingEntryDetailDto? entry, List<int> receiptIds)
    {
        var parameters = new DialogParameters<AccountingEntryDialog>
        {
            { d => d.Entry, entry },
            { d => d.ReceiptIds, receiptIds },
        };
        await dialogService.ShowAsync<AccountingEntryDialog>(locale[entry is null ? "Entry_AddTitle" : "Entry_EditTitle"], parameters, Wide);
    }

    /// <summary>A new entry filled in with <paramref name="draft"/>, saved with <paramref name="save"/> (a suggestion or bank line is booked with it).</summary>
    public static async Task<bool> OpenDraft(IDialogService dialogService, IStringLocalizer<AccountingLocale> locale, SaveAccountingEntryModel draft,
        Func<SaveAccountingEntryModel, Task<Bookennis.Client.Services.HttpClients.HttpResult<AccountingEntryDetailDto>>> save)
    {
        var parameters = new DialogParameters<AccountingEntryDialog>
        {
            { d => d.Draft, draft },
            { d => d.SaveFunc, save },
        };
        var dialog = await dialogService.ShowAsync<AccountingEntryDialog>(locale["Entry_AddTitle"], parameters, Wide);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>Writes a new draft invoice (<paramref name="invoice"/> = null) or changes a draft.</summary>
    public static Task OpenInvoice(IDialogService dialogService, IStringLocalizer<AccountingLocale> locale, InvoiceDetailDto? invoice)
        => dialogService.ShowAsync<InvoiceDialog>(locale[invoice is null ? "Invoice_AddTitle" : "Invoice_EditTitle"],
            new DialogParameters<InvoiceDialog> { { d => d.Invoice, invoice } }, Wide);

    public static Task OpenInvoiceDetail(IDialogService dialogService, IStringLocalizer<AccountingLocale> locale, int invoiceId)
        => dialogService.ShowAsync<InvoiceDetailDialog>(locale["Invoice"], new DialogParameters<InvoiceDetailDialog> { { d => d.InvoiceId, invoiceId } }, Wide);

    public static async Task OpenDetail(IDialogService dialogService, IStringLocalizer<AccountingLocale> locale, int entryId)
    {
        var parameters = new DialogParameters<AccountingEntryDetailDialog> { { d => d.EntryId, entryId } };
        await dialogService.ShowAsync<AccountingEntryDetailDialog>(locale["Entry_DetailTitle"], parameters, Wide);
    }

    public static Task Open<TDialog>(IDialogService dialogService, string title, DialogParameters<TDialog> parameters)
        where TDialog : Microsoft.AspNetCore.Components.ComponentBase
        => dialogService.ShowAsync<TDialog>(title, parameters, Narrow);

    /// <summary>Shows a photo of a receipt in a dialog; a PDF is downloaded (the browser opens it with its own viewer).</summary>
    public static async Task ShowReceipt(IAccountingStore store, IDialogService dialogService, IJSRuntime jsRuntime, ISnackbar snackbar,
        IStringLocalizer<BackendErrorCodeLocale> backendLocale, AccountingReceiptDto receipt)
    {
        var result = await store.GetReceiptFile(receipt.Id);
        if (result is not { Success: true, Dto: not null })
        {
            snackbar.ShowErrorDetails(result, backendLocale);
            return;
        }

        if (receipt.ContentType.StartsWith("image/", StringComparison.Ordinal))
        {
            var parameters = new DialogParameters<AccountingReceiptImageDialog>
            {
                { d => d.DataUrl, $"data:{receipt.ContentType};base64,{Convert.ToBase64String(result.Dto.Data)}" },
                { d => d.FileName, receipt.FileName },
            };
            await dialogService.ShowAsync<AccountingReceiptImageDialog>(receipt.FileName, parameters, Wide);
            return;
        }

        using var streamReference = new DotNetStreamReference(new MemoryStream(result.Dto.Data));
        await jsRuntime.InvokeVoidAsync("App.downloadFileFromStream", receipt.FileName, streamReference);
    }
}
