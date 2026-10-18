using Bookennis.Api.Data;
using Bookennis.Domain.Clubhouse;
using Bookennis.Shared.Controller.Clubhouse;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ClubhouseItemCategory = Bookennis.Domain.Clubhouse.ClubhouseItemCategory;

namespace Bookennis.Api.Business.Clubhouse;

/// <summary>The menu of the clubhouse in menu order, with the hidden items (the host shows or hides them).</summary>
public record GetClubhouseItems(int ClubId) : IQuery<List<ClubhouseItemDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubhouseItems, List<ClubhouseItemDto>>
    {
        public async Task<List<ClubhouseItemDto>> Handle(GetClubhouseItems request, CancellationToken cancellationToken)
        {
            var items = await context.ClubhouseItems.AsNoTracking()
                .Where(i => i.ClubId == request.ClubId)
                .InMenuOrder()
                .ToListAsync(cancellationToken);

            return items.Select(i => i.ToDto()).ToList();
        }
    }
}

public record CreateClubhouseItem(int ClubId, SaveClubhouseItemModel Model) : ICommand<ClubhouseItemDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateClubhouseItem, ClubhouseItemDto>
    {
        public async Task<ClubhouseItemDto> Handle(CreateClubhouseItem request, CancellationToken cancellationToken)
        {
            var item = new ClubhouseItem(request.ClubId, request.Model.ToData());
            context.ClubhouseItems.Add(item);
            await context.SaveChangesAsync(cancellationToken);
            return item.ToDto();
        }
    }
}

/// <summary>Changes an item. What is already on a tab keeps the old name and price.</summary>
public record UpdateClubhouseItem(int ClubId, int ItemId, SaveClubhouseItemModel Model) : ICommand<ClubhouseItemDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateClubhouseItem, ClubhouseItemDto>
    {
        public async Task<ClubhouseItemDto> Handle(UpdateClubhouseItem request, CancellationToken cancellationToken)
        {
            var item = await context.ClubhouseItems.SingleRequiredAsync(i => i.Id == request.ItemId && i.ClubId == request.ClubId, cancellationToken);
            item.Update(request.Model.ToData());
            await context.SaveChangesAsync(cancellationToken);
            return item.ToDto();
        }
    }
}

/// <summary>Deletes an item. The tabs keep its name and price.</summary>
public record DeleteClubhouseItem(int ClubId, int ItemId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteClubhouseItem>
    {
        public async Task<Unit> Handle(DeleteClubhouseItem request, CancellationToken cancellationToken)
        {
            var item = await context.ClubhouseItems.SingleRequiredAsync(i => i.Id == request.ItemId && i.ClubId == request.ClubId, cancellationToken);
            context.ClubhouseItems.Remove(item);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}

internal static class ClubhouseItemModels
{
    public static ClubhouseItem.ItemData ToData(this SaveClubhouseItemModel model)
        => new(model.Name, (ClubhouseItemCategory)model.Category, model.PriceCents, model.IsAvailable, model.SortOrder);
}
