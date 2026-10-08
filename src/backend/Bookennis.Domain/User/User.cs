using System.Diagnostics.CodeAnalysis;
using Bookennis.Domain.Base;
using Bookennis.Global;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Domain.User;

public enum Gender
{
    Male,
    Female,
    Diverse
}

public enum Language
{
    German,
    English
}

public enum Country
{
    Austria,
    Germany,
    Switzerland
}

public class User : IdentityUser<int>, IEntity
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private User() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public User(string userName, string email, string firstName, string lastName, DateOnly birthday, Gender gender, string street = "", string city = "", string zipCode = "", Country country = Country.Austria, Language language = Language.German)
    {
        UserName = userName.Clean();
        Email = email.Clean();
        FirstName = firstName.Clean();
        LastName = lastName.Clean();
        Birthday = birthday;
        Gender = gender;
        Street = street.Trim();
        City = city.Trim();
        ZipCode = zipCode.Trim();
        Country = country;
        Language = language;
        UpdateFullName();
    }

    public User(string firstName, string lastName, DateOnly birthday, Gender gender, int belongsToUserId, string street = "", string city = "", string zipCode = "", Country country = Country.Austria, Language language = Language.German)
    {
        FirstName = firstName.Clean();
        LastName = lastName.Clean();
        Birthday = birthday;
        Gender = gender;
        Language = language;
        Street = street.Trim();
        City = city.Trim();
        ZipCode = zipCode.Trim();
        Country = country;
        BelongsToUserId = belongsToUserId;
        UpdateFullName();
    }

    public EntityMetadata Metadata { get; private set; } = new();
    public List<IDomainEvent> Events { get; } = new();
    public void AddDomainEvent(IDomainEvent domainEvent) => Events.Add(domainEvent);

    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string FullName { get; private set; }
    public DateOnly Birthday { get; private set; }
    public Gender Gender { get; private set; }
    public Language Language { get; private set; }
    public string Street { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string ZipCode { get; private set; } = string.Empty;
    public Country Country { get; private set; }
    public DateTimeOffset? ProfileCompletionBannerDismissedAt { get; private set; }

    /// <summary>When the user confirmed the privacy policy at registration. <c>null</c> for users that registered before it was asked.</summary>
    public DateTimeOffset? PrivacyPolicyAcceptedAt { get; private set; }

    public int? BelongsToUserId { get; private set; }
    public bool IsBelongingToUser => BelongsToUserId is not null;

    public void Update(string firstName, string lastName, DateOnly birthday, Gender gender, Language language, string street, string city, string zipCode, Country country)
    {
        FirstName = firstName.Clean();
        LastName = lastName.Clean();
        Birthday = birthday;
        Gender = gender;
        Language = language;
        Street = street.Trim();
        City = city.Trim();
        ZipCode = zipCode.Trim();
        Country = country;
        UpdateFullName();
    }

    public void DismissProfileCompletionBanner()
        => ProfileCompletionBannerDismissedAt ??= DateTimeOffset.UtcNow;

    public void AcceptPrivacyPolicy()
        => PrivacyPolicyAcceptedAt = DateTimeOffset.UtcNow;

    /// <summary>Hands a user without own login (a child) over to another user, e.g. the other parent when the owner deletes their account.</summary>
    public void TransferTo(int newOwnerUserId)
    {
        if (!IsBelongingToUser)
            throw new InvalidOperationException("Only users that belong to another user can be transferred.");

        if (newOwnerUserId == Id)
            throw new ArgumentException("A user can't belong to itself.", nameof(newOwnerUserId));

        BelongsToUserId = newOwnerUserId;
    }

    [MemberNotNull(nameof(FullName))]
    private void UpdateFullName() => FullName = $"{FirstName} {LastName}";
}