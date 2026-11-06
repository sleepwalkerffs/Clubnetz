using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Accounting;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Accounting;

/// <summary>The receipts that wait for an entry, newest first.</summary>
public record GetAccountingReceiptInbox(int ClubId) : IQuery<List<AccountingReceiptDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingReceiptInbox, List<AccountingReceiptDto>>
    {
        public async Task<List<AccountingReceiptDto>> Handle(GetAccountingReceiptInbox request, CancellationToken cancellationToken)
        {
            var receipts = await context.AccountingReceipts.AsNoTracking()
                .Where(r => r.ClubId == request.ClubId && r.EntryId == null)
                .OrderByDescending(r => r.UploadedAt).ThenByDescending(r => r.Id)
                .ToListAsync(cancellationToken);
            var names = await context.MemberNames(request.ClubId, receipts.Select(r => r.UploadedByMemberId), cancellationToken);
            return receipts.Select(r => r.ToDto(null, names)).ToList();
        }
    }
}

/// <summary>Uploads a receipt into the inbox (a photo from the phone or a PDF). It is attached to an entry when the entry is saved.</summary>
public record UploadAccountingReceipt(int ClubId, int UserId, IFormFile File, DateTimeOffset Now) : ICommand<AccountingReceiptDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UploadAccountingReceipt, AccountingReceiptDto>
    {
        public async Task<AccountingReceiptDto> Handle(UploadAccountingReceipt request, CancellationToken cancellationToken)
        {
            await context.GetAccountingSettings(request.ClubId, cancellationToken);

            // Checked before reading, so that an oversized upload is not loaded into memory
            if (request.File.Length > AccountingReceipt.MaxSizeInBytes)
                throw new PreconditionException(AccountingErrorCode.AccountingReceiptTooLarge, [(AccountingReceipt.MaxSizeInBytes / (1024 * 1024)).ToString()], "The file is too large.");

            await using var input = request.File.OpenReadStream();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, cancellationToken);

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var receipt = new AccountingReceipt(request.ClubId, request.File.FileName, buffer.ToArray(), memberId, request.Now);
            context.Add(receipt);
            await context.SaveChangesAsync(cancellationToken);

            return receipt.ToDto(null, await context.MemberNames(request.ClubId, [memberId], cancellationToken));
        }
    }
}

/// <summary>Deletes a receipt from the inbox. A receipt of an entry is taken off the entry first (in an open year).</summary>
public record DeleteAccountingReceipt(int ClubId, int ReceiptId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteAccountingReceipt>
    {
        public async Task<Unit> Handle(DeleteAccountingReceipt request, CancellationToken cancellationToken)
        {
            await context.LockAccounting(request.ClubId, cancellationToken);
            var receipt = await context.AccountingReceipts.SingleRequiredAsync(r => r.Id == request.ReceiptId && r.ClubId == request.ClubId, cancellationToken);
            if (receipt.EntryId is { } entryId)
            {
                var number = await context.AccountingEntries.Where(e => e.Id == entryId).Select(e => e.Number).FirstAsync(cancellationToken);
                throw new PreconditionException(AccountingErrorCode.AccountingReceiptAlreadyBooked, [number.ToString(System.Globalization.CultureInfo.InvariantCulture)], "The receipt belongs to an entry.");
            }

            context.AccountingReceipts.Remove(receipt);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

/// <summary>The file of a receipt. Served as a download with the content type of its extension, never the one the browser sent.</summary>
public record GetAccountingReceiptFile(int ClubId, int ReceiptId) : IQuery<AccountingReceiptFile>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAccountingReceiptFile, AccountingReceiptFile>
    {
        public async Task<AccountingReceiptFile> Handle(GetAccountingReceiptFile request, CancellationToken cancellationToken)
        {
            var receipt = await context.AccountingReceipts.AsNoTracking().Include(r => r.Content)
                .SingleRequiredAsync(r => r.Id == request.ReceiptId && r.ClubId == request.ClubId, cancellationToken);
            return new AccountingReceiptFile(receipt.Content.Data, receipt.ContentType, receipt.FileName);
        }
    }
}

public record AccountingReceiptFile(byte[] Data, string ContentType, string FileName);
