using System.Drawing;
using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Domain.Weather;
using Bookennis.Global;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Clubs;

[Guid("D52FEEDD-4AAC-4F07-A926-AC3BE2894811")]
public class Club : DomainEntity, IAggregateRoot
{
    public const int ConcurrentAllowedBookingsForNewClubs = 15;
    private readonly List<PlayMode> playModes = new();
    private readonly List<Season> seasons = new();

#pragma warning disable 8618
    private Club() { }
#pragma warning restore 8618

    public Club(string name, TimeOnlyInterval openingHours, TimeOnlyInterval primeTimeHours, List<PlayModeDto> playModes, int? bookingGracePeriodInMinutes = null, PrimeTimeSettings? primeTimeSettings = null)
    {
        Name = name.Clean();
        OpeningHours = openingHours;
        PrimeTimeSettings = primeTimeSettings ?? new PrimeTimeSettings(
            isEnabled: true,
            primeTimeHours: primeTimeHours,
            applicableWeekdays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            restrictChildren: true,
            restrictGuests: true,
            childAgeThreshold: 18);
        this.playModes = playModes.ConvertAll(playMode => new PlayMode(this,
            playMode.AllowedRoles,
            playMode.Color,
            playMode.FixedPlayerCount,
            playMode.IsChargingBookingSubscription,
            playMode.Name.Clean(),
            playMode.FixedDuration,
            playMode.CanOverbook,
            playMode.CommentAllowed,
            playMode.MaxBookingsPerSeason));
        BookingGracePeriodInMinutes = bookingGracePeriodInMinutes;
    }

    public string Name { get; private set; }

    /// <summary>
    /// Bookings per week: how many upcoming bookings in charging play modes a member may have at the same time.
    /// Applies to every member without an own value (<see cref="Member.BookingsPerWeek"/>) and is the upper bound of the own values.
    /// </summary>
    public int ConcurrentAllowedBookings { get; private set; } = ConcurrentAllowedBookingsForNewClubs;

    public const int MinBookingsPerWeek = 1;
    public const int MaxBookingsPerWeek = 50;

    public enum BookingsPerWeekErrorCode
    {
        ClubInvalidBookingsPerWeek = 0,
    }

    public void SetBookingsPerWeek(int bookingsPerWeek)
    {
        if (bookingsPerWeek is < MinBookingsPerWeek or > MaxBookingsPerWeek)
            throw new PreconditionException(BookingsPerWeekErrorCode.ClubInvalidBookingsPerWeek, [MinBookingsPerWeek.ToString(), MaxBookingsPerWeek.ToString()],
                $"Bookings per week must be between {MinBookingsPerWeek} and {MaxBookingsPerWeek}.");
        ConcurrentAllowedBookings = bookingsPerWeek;
    }
    public TimeOnlyInterval OpeningHours { get; private set; }
    public IReadOnlyList<PlayMode> PlayModes => playModes.AsReadOnly();
    public IReadOnlyList<Season> Seasons => seasons.AsReadOnly();
    public int? BookingGracePeriodInMinutes { get; private set; }
    public bool IsAtpClub { get; private set; }

    /// <summary>
    /// The club admin changed what the roles may do: the stored <c>RolePermission</c> rows are the matrix, even when there are none left.
    /// <c>false</c>: the defaults of <c>PermissionCatalog</c> apply.
    /// </summary>
    public bool HasCustomPermissions { get; private set; }

    public void SetHasCustomPermissions(bool value) => HasCustomPermissions = value;

    public PrimeTimeSettings PrimeTimeSettings { get; private set; } = PrimeTimeSettings.Default;

    /// <summary>Public website of the club, available in club emails as <c>club.website_url</c>.</summary>
    public string? WebsiteUrl { get; private set; }

    /// <summary>Address members reach when replying to a club email.</summary>
    public string? ReplyToEmail { get; private set; }

    /// <summary>Where the courts are (see <see cref="GeoLocation"/>); both or none are set. Members see the weather forecast when set.</summary>
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    public GeoLocation? Location => Latitude is { } lat && Longitude is { } lon ? new GeoLocation(lat, lon) : null;

    public void SetLocation(GeoLocation? location)
    {
        Latitude = location?.Latitude;
        Longitude = location?.Longitude;
    }

    public void SetIsAtpClub(bool isAtpClub) => IsAtpClub = isAtpClub;

    /// <summary>IANA id of the club's time zone: opening hours, prime time, club events and "today" are local times of it.</summary>
    public string TimeZoneId { get; private set; } = ClubTimeZone.DefaultId;

    /// <summary>The country of the club: how dates, times and amounts are written (12-hour clock in the United States) and where its Stripe account is.</summary>
    public Country Country { get; private set; } = Country.Austria;

    /// <summary>ISO 4217 code in lower case ("eur", "usd", "chf"): what membership plans, court time and the clubhouse charge, and the currency of the accounting.</summary>
    public string Currency { get; private set; } = DefaultCurrency;

    public const string DefaultCurrency = "eur";
    public static readonly IReadOnlyList<string> SupportedCurrencies = ["eur", "usd", "chf"];

    public TimeZoneInfo TimeZone => ClubTimeZone.Get(TimeZoneId);

    public enum RegionErrorCode
    {
        ClubInvalidTimeZone = 0,
        ClubUnsupportedCurrency = 1,
        ClubUnknownCountry = 2,

        /// <summary>The club has prices, payments or accounting in its currency: changing it would charge old prices in the new currency.</summary>
        ClubCurrencyInUse = 3,
    }

    /// <summary>Where the club is. Prices that were set before keep the currency they were set in.</summary>
    public void SetRegion(Country country, string timeZoneId, string currency)
    {
        currency = currency.Trim().ToLowerInvariant();
        timeZoneId = timeZoneId.Trim();
        // A club keeps a zone it already has; a new one must be one of the supported zones
        if (!ClubTimeZone.IsValid(timeZoneId) || (timeZoneId != TimeZoneId && !ClubTimeZone.Supported.Contains(timeZoneId)))
            throw new PreconditionException(RegionErrorCode.ClubInvalidTimeZone, "Unknown time zone.");
        if (!SupportedCurrencies.Contains(currency))
            throw new PreconditionException(RegionErrorCode.ClubUnsupportedCurrency, "Unsupported currency.");
        if (!Enum.IsDefined(country))
            throw new PreconditionException(RegionErrorCode.ClubUnknownCountry, "Unknown country.");

        Country = country;
        TimeZoneId = timeZoneId;
        Currency = currency;
    }

    public void UpdateWorkingHours(TimeOnlyInterval openingHours, TimeOnlyInterval primeTimeHours)
    {
        if (!openingHours.Contains(primeTimeHours))
            throw new InvalidOperationException("Prime time hours must be withing opening hours");

        OpeningHours = openingHours;
        PrimeTimeSettings = new PrimeTimeSettings(
            PrimeTimeSettings.IsEnabled,
            primeTimeHours,
            PrimeTimeSettings.ApplicableWeekdays,
            PrimeTimeSettings.RestrictChildren,
            PrimeTimeSettings.RestrictGuests,
            PrimeTimeSettings.ChildAgeThreshold);
    }

    public void SetBookingGracePeriod(int? minutes) => BookingGracePeriodInMinutes = minutes;

    public void UpdatePrimeTimeSettings(PrimeTimeSettings settings) => PrimeTimeSettings = settings;

    public void UpdateContactSettings(string? websiteUrl, string? replyToEmail)
    {
        websiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim();
        replyToEmail = string.IsNullOrWhiteSpace(replyToEmail) ? null : replyToEmail.Trim();

        if (websiteUrl is not null && !IsValidWebsiteUrl(websiteUrl))
            throw new ArgumentException("Website url must be an absolute http(s) url.", nameof(websiteUrl));
        if (replyToEmail is not null && !System.Net.Mail.MailAddress.TryCreate(replyToEmail, out _))
            throw new ArgumentException("Reply-to email is not a valid email address.", nameof(replyToEmail));

        WebsiteUrl = websiteUrl;
        ReplyToEmail = replyToEmail;
    }

    public static bool IsValidWebsiteUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public PlayMode AddPlayMode(PlayModeDto playMode)
    {
        var added = new PlayMode(
            this,
            playMode.AllowedRoles,
            playMode.Color,
            playMode.FixedPlayerCount,
            playMode.IsChargingBookingSubscription,
            playMode.Name.Clean(),
            playMode.FixedDuration,
            playMode.CanOverbook,
            playMode.CommentAllowed,
            playMode.MaxBookingsPerSeason,
            playMode.AllowRecurring);
        playModes.Add(added);
        return added;
    }

    public void UpdatePlayMode(int playModeId, PlayModeDto dto)
    {
        var playMode = playModes.SingleOrDefault(p => p.Id == playModeId)
            ?? throw new InvalidOperationException($"PlayMode with id {playModeId} not found.");
        playMode.Update(dto.AllowedRoles, dto.Color, dto.FixedPlayerCount, dto.IsChargingBookingSubscription, dto.Name.Clean(), dto.FixedDuration, dto.CanOverbook, dto.CommentAllowed, dto.MaxBookingsPerSeason, dto.AllowRecurring);
    }

    public void RemovePlayMode(int playModeId)
    {
        var playMode = playModes.SingleOrDefault(p => p.Id == playModeId)
            ?? throw new InvalidOperationException($"PlayMode with id {playModeId} not found.");
        playModes.Remove(playMode);
    }

    public Season AddSeason(DateOnlyInterval period)
    {
        if (seasons.Any(s => s.Period.Intersects(period)))
            throw new InvalidOperationException("Seasons cannot overlap.");
        var season = new Season(Id, period);
        seasons.Add(season);
        return season;
    }

    public void UpdateSeason(int seasonId, DateOnlyInterval period)
    {
        var season = seasons.SingleOrDefault(s => s.Id == seasonId)
            ?? throw new InvalidOperationException($"Season with id {seasonId} not found.");
        if (seasons.Any(s => s.Id != seasonId && s.Period.Intersects(period)))
            throw new InvalidOperationException("Seasons cannot overlap.");
        season.Update(period);
    }

    public void RemoveSeason(int seasonId)
    {
        var season = seasons.SingleOrDefault(s => s.Id == seasonId)
            ?? throw new InvalidOperationException($"Season with id {seasonId} not found.");
        seasons.Remove(season);
    }

    public void Update(string name, TimeOnlyInterval openingHours, TimeOnlyInterval primeTimeHours, int? bookingGracePeriodInMinutes, bool isAtpClub, PrimeTimeSettings primeTimeSettings)
    {
        Name = name.Clean();
        if (!openingHours.Contains(primeTimeHours))
            throw new InvalidOperationException("Prime time hours must be within opening hours");
        OpeningHours = openingHours;
        BookingGracePeriodInMinutes = bookingGracePeriodInMinutes;
        IsAtpClub = isAtpClub;
        PrimeTimeSettings = new PrimeTimeSettings(
            primeTimeSettings.IsEnabled,
            primeTimeHours,
            primeTimeSettings.ApplicableWeekdays,
            primeTimeSettings.RestrictChildren,
            primeTimeSettings.RestrictGuests,
            primeTimeSettings.ChildAgeThreshold);
    }

    public record PlayModeDto(
        MemberRole[] AllowedRoles,
        Color Color,
        int? FixedPlayerCount,
        bool IsChargingBookingSubscription,
        string Name,
        TimeSpan? FixedDuration,
        bool CanOverbook,
        bool CommentAllowed,
        int? MaxBookingsPerSeason = null,
        bool AllowRecurring = false);
}