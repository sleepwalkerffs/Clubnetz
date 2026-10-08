using System.Linq.Expressions;
using Bookennis.Domain.Base;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data;

public static class NavigationBuilderExtensions
{
    public static void ConfigureTenantRelation<TParent>(this EntityTypeBuilder<TParent> builder)
        where TParent : TenantDomainEntity
        => builder.HasOne<Club>().WithMany().HasForeignKey(i => i.ClubId);

    public static void OwnsDateTimeOffsetInterval<TParent>(this EntityTypeBuilder<TParent> builder, Expression<Func<TParent, DateTimeOffsetInterval?>> navigationExpression)
        where TParent : class
        => builder.OwnsOne(
            navigationExpression,
            validityBuilder =>
            {
                validityBuilder.HasIndex(v => v.From).IsDescending();
                validityBuilder.HasIndex(v => v.To).IsDescending();
            });

    public static void OwnsDateOnlyInterval<TParent>(this EntityTypeBuilder<TParent> builder, Expression<Func<TParent, DateOnlyInterval?>> navigationExpression)
        where TParent : class
        => builder.OwnsOne(
            navigationExpression,
            validityBuilder =>
            {
                validityBuilder.HasIndex(v => v.From).IsDescending();
                validityBuilder.HasIndex(v => v.To).IsDescending();
            });

    public static void OwnsTimeOnlyInterval<TParent>(this EntityTypeBuilder<TParent> builder, Expression<Func<TParent, TimeOnlyInterval?>> navigationExpression)
        where TParent : class
        => builder.OwnsOne(
            navigationExpression,
            validityBuilder =>
            {
                validityBuilder.HasIndex(v => v.From).IsDescending();
                validityBuilder.HasIndex(v => v.To).IsDescending();
            });

    public static void OwnsTimeOnlyInterval<TOwner, TDependent>(this OwnedNavigationBuilder<TOwner, TDependent> builder, Expression<Func<TDependent, TimeOnlyInterval?>> navigationExpression)
        where TOwner : class
        where TDependent : class
        => builder.OwnsOne(
            navigationExpression,
            validityBuilder =>
            {
                validityBuilder.HasIndex(v => v.From).IsDescending();
                validityBuilder.HasIndex(v => v.To).IsDescending();
            });
}