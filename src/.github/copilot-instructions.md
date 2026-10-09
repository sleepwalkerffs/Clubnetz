# Copilot Instructions

## Project Guidelines

- All MudBlazor input components (MudTextField, MudNumericField, MudTimePicker, MudDatePicker, MudSelect, MudAutocomplete) should use Variant="Variant.Outlined".
- Always follow the Medaiator pattern for implementing controllers and command/query handlers.
- Use accessibility modifiers for interfaces
- FE calls to the BE should always be made via the store implementations in the FE. The stores should use the HttpClient to call the BE endpoints. The stores should be injected into the components via dependency injection. The components should not call the BE endpoints directly.
- For stores use the RunInLoadingContext for read methods and RunInSavingContext for write methods.
- For all FE implementations consider mobile first design and responsiveness.
- For all FE implementations it has to look good on mobile and desktop
- For every command/query handler in the backend make sure its unit tested
- Always respect DDD principles and best practices when implementing features in the backend
- Always localize all strings in the FE
- Always localize all string in emails in the BE
- For localization respect English and German
- For authorization in the BE use resource based authorization with policies and requirements
- Validation and BE errors that result from invalid user input should be returned as ErrorCodeResponse via the ExceptionFilter. The errors should the be displayed in the FE using the SnackbarExtensions.ShowErrorDetails. The user should stay on the same page
- This is a multi tenant application. Always consider the tenant context when implementing features in the BE. The tenant context can be accessed via the ITenantService service. The tenant context contains the TenantId and other tenant specific information. The Tenant is represented as the Club with the Club.Id beeing the TeanentId
- Every backend request handler needs to be tested so that the ef core query can be translated.
- Also do not commit changes. I will commit the changes after i have reviewed them.

## Guest System & Dual Membership

### Session Types

- **Normal session**: User logs in with email/password. They are treated as a `ClubMember`.
- **Guest session**: User logs in via a guest link/code. They are treated as a `GuestMember`.
- The session type is determined by the `guest_session` claim (set to `"true"` during `LoginGuest`). The `guest_member_id` claim stores the specific GuestMember ID.

### Dual Membership

- A single user can have BOTH a `ClubMember` and a `GuestMember` record for the same club (e.g., a club member who also purchased a guest card).
- **CRITICAL**: Any query on `context.Set<Member>()` filtered by `UserId` (and optionally `ClubId`) may return multiple records. Never use `SingleAsync`, `SingleOrDefaultAsync`, or `SingleRequiredAsync` on such queries. Use `FirstOrDefaultAsync` with an `OrderBy` or filter by `MemberType`.
- When filtering by session type: use `MemberType.ClubMember` for normal sessions, `MemberType.GuestMember` for guest sessions.
- The `AuthorizationHandlerService.GetMember()` accepts a `ClaimsPrincipal` parameter to determine which member type to return.

### Member Hierarchy

- `Member` (abstract) → `ClubMember` and `GuestMember`
- `ClubMember.CanParticipate` is always `false` — ClubMembers can only book if enrolled in an active season (`MemberSeasons`).
- `GuestMember.CanParticipate` returns `IsAllowedToBook` (set via `AllowBooking()` in `CreateGuestCard`).
- `GuestMember` always has `MemberRole.Guest`; `ClubMember` typically has `MemberRole.User`.

### Booking Eligibility (`GetBookingOptions`)

- **Guest session**: Allowed to book if `GuestMember.IsAllowedToBook == true` AND `sum(GuestCards.PurchasedBookings) > count(BookingPlayers)`.
- **Normal session (ClubMember)**: Allowed to book if enrolled in the current active season.

### Guest Restrictions

- Guests cannot access Statistics or Leaderboards (denied by `AuthorizationPolicies.ClubMember` policy, hidden in NavMenu).
- Guests cannot book during prime time if `PrimeTimeSettings.RestrictGuests` is enabled.
- Guests can only have 1 upcoming booking at a time.
- Guests cannot be mixed with ClubMembers in the same booking.

### Query Filter Note

- The tenant query filter IS applied to `Member` (the root type). `ClubMember` and `GuestMember` inherit it automatically (EF Core TPH inheritance). They are listed in the exclusion array of `AddQueryFilterToAllEntitiesAssignableFrom` only because EF Core does not allow applying filters directly to derived types — but the filter still works via the root `Member` entity.
- When `tenantService.GetTenantId()` is null (e.g., admin routes without `{clubId}`), the filter passes all records through.
- Existing `IgnoreQueryFilters()` on `Set<Member>()` in handlers like `GetBookings`, `GetLeaderboard`, `GetMemberBookingHistory`, etc. is used to show players from other clubs (ATP cross-club feature).
- The dual membership issue (both `ClubMember` and `GuestMember` for the same user+club) is NOT a tenant filter problem — both records have the same `ClubId`. It's a cardinality issue requiring `MemberType` filtering or `FirstOrDefault` instead of `Single`.

## Recurring Bookings

### Overview

Recurring bookings allow members to reserve a court on a repeating weekly schedule. A recurring booking creates a `RecurringBookingSeries` entity that acts as the template, and individual `Booking` entities for each occurrence.

### Domain Model

- `RecurringBookingSeries` stores the series template: `ClubId`, `CourtId`, `PlayModeId`, `DayOfWeek`, `StartTime`, `EndTime`, `TimeZoneInfoId`, `RecurrenceIntervalWeeks`, `StartDate`, `EndDate`, `Comment`, and `SeriesPlayers`.
- Each individual `Booking` has a nullable `RecurringBookingSeriesId` FK linking it to its series.
- `Booking.IsExcludedFromSeries` marks bookings that were individually edited and should not be touched by series-wide operations.

### Creating a Recurring Booking (`BookRecurringCourt`)

- Requires: `CourtId`, `Interval` (first occurrence), `TimeZoneInfoId`, `PlayModeId`, `Players`, `RecurrenceIntervalWeeks` (≥ 1).
- Optional `EndDate`; defaults to the active season's end date (capped to season end).
- The `PlayMode.AllowRecurring` flag must be `true`.
- Only the **first occurrence** is fully validated via `BookingDomainService.BookCourt` (participants, opening hours, prime time, conflicts, season enrollment, etc.).
- Subsequent occurrences only check for court conflicts (unless `PlayMode.CanOverbook` is true). Conflicting occurrences are **skipped**, not rejected.
- All players must be enrolled in the current active season (`MemberSeasons`).
- Guests cannot create recurring bookings (only `ClubMember` with `MemberRole.User`).

### Editing a Recurring Booking (`EditRecurringBooking`)

- Uses `RecurringEditScope` enum:
    - **`Single`**: Edits only the selected occurrence. The booking is validated via `BookingDomainService.EditBooking`, then marked as `ExcludeFromSeries()` so future series edits don't overwrite it.
    - **`ThisAndFollowing`**: Deletes all non-excluded future bookings from the edit date onward, updates the series template (`Update()`), and regenerates future occurrences. Individually edited (excluded) bookings on those dates are preserved/skipped.
- When regenerating, court conflicts are checked per occurrence (skipped if conflicting, unless `CanOverbook`).

### Deleting a Recurring Booking (`DeleteRecurringBooking`)

- Uses `RecurringDeleteScope` enum:
    - **`Single`**: Removes only the selected booking.
    - **`ThisAndFollowing`**: Removes the selected booking and all future bookings in the same series. If no bookings remain, the `RecurringBookingSeries` entity is also deleted. Otherwise, the series `EndDate` is updated to the day before the deleted booking.
- The club's `BookingGracePeriodInMinutes` is respected — cannot delete bookings that have already started (minus grace period).

### Key Considerations

- **Time zones**: All date/time calculations use the provided `TimeZoneInfoId` to correctly determine local dates, day-of-week, and handle DST transitions.
- **Season boundary**: Series end date is always capped to the active season's end. No occurrences are created beyond the season.
- **Conflict handling**: Non-first occurrences that conflict with existing bookings are silently skipped (not an error).
- **Excluded bookings**: When doing series-wide edits, bookings with `IsExcludedFromSeries == true` are never deleted or regenerated — they represent user-customized single occurrences.
- **Authorization**: Same as regular bookings — resource-based authorization. The user must own the booking or be a family member of a participant.
- **No guest access**: Recurring bookings are restricted to `ClubMember` participants enrolled in the active season.

## Architecture & Project Structure

### Solution Layout

- `Bookennis.Api` — Backend ASP.NET Core Web API (controllers, mediator handlers, EF Core `AppDbContext`, infrastructure).
- `Bookennis.Domain` — Domain layer (entities, domain services, value objects, exceptions). No dependencies on infrastructure.
- `Bookennis.Client` — Frontend Blazor WebAssembly project (pages, components, stores).
- `Bookennis.Shared` — Shared DTOs/models referenced by both FE and BE (controller models, enums).
- `Bookennis.Global` — Cross-cutting utilities (e.g., `DateTimeOffsetInterval`).
- `Bookennis.Api.Tests` — Backend integration/unit tests.
- `Bookennis.Domain.Tests` — Domain logic unit tests.

### Dependency Injection

- The backend uses **SimpleInjector** as the DI container, integrated with ASP.NET Core via `AddSimpleInjector` / `UseSimpleInjector`.
- Registration is split into configuration classes under `Infrastructure/Configuration/ContainerRegistrations/`.
- The mediator is registered via `container.RegisterMediator(services, [...assemblies])` using `Fusonic.Extensions.Mediator`.

### Database

- **PostgreSQL** via Npgsql and EF Core.
- `AppDbContext` is the single DbContext.
- Entities inherit from `DomainEntity` (has `Id`, `EntityMetadata` with Created/Modified timestamps) or `TenantDomainEntity` (adds `ClubId` for multi-tenancy).
- Domain events are collected on entities (`Events` list) and dispatched via `IDomainEventDispatcher` after `SaveChangesAsync`.

### Controller Conventions

- `ControllerBase` — base class for non-tenant routes. Route: `api/[controller]`.
- `ClubControllerBase` — base class for tenant-scoped routes. Route: `api/Clubs/{clubId:int}/[controller]`. The `{clubId}` route parameter is picked up by `MultiTenantServiceMiddleware` to set the tenant context.
- Controllers use primary constructors, inject `IMediator`, and delegate to command/query handlers.

### Exception & Error Handling

- `ErrorDetailException` is the base for domain validation errors. It carries an optional `ErrorCode` enum and `ErrorDetails` string array.
- `PreconditionException` (HTTP 412) — thrown for business rule violations / invalid user input.
- The `ExceptionFilter` maps these to `ErrorCodeResponse` JSON responses.
- In the FE, `HttpResult` / `HttpResult<T>` wraps responses; use `SnackbarExtensions.ShowErrorDetails` to display errors.

### Frontend Store Pattern

- Every store interface extends `ISemaphoreStore` and is implemented by a class extending `SemaphoreStore`.
- `SemaphoreStore` provides `RunInLoadingContextAsync` (for reads) and `RunInSavingContextAsync` (for writes) which manage `IsLoading` / `IsSaving` state and cancellation.
- Stores expose `event Action OnXxxChanged` for components to subscribe to state changes.
- Stores use typed HTTP clients (`HttpClient`) to call backend endpoints and return `HttpResult` / `HttpResult<T>`.

### Testing Conventions

- Backend tests inherit from `TestBase` (which extends `DatabaseUnitTest<AppDbContext, TestFixture>`).
- `TestFixture` sets up SimpleInjector container, registers NSubstitute mocks for external services (`IBookingDomainService`, `IBackgroundJobClient`, etc.), and provides a real PostgreSQL test database.
- Use `SendAsync(new Command(...))` to dispatch commands/queries through the mediator in tests.
- `TestData()` extension provides seeded test entities (Club, User, Members, Courts, etc.).
- `SetTenantId(id)` sets the tenant context for a test.
- Domain services (e.g., `IBookingDomainService`) are mocked in handler tests; domain service logic is tested separately in `Bookennis.Domain.Tests`.
- The test database template (`bookennis_test`) is only created when it does not exist. After adding a migration, drop it once (`ALTER DATABASE bookennis_test IS_TEMPLATE false; DROP DATABASE bookennis_test;` in the `postgres_test` container), otherwise the tests fail with "relation does not exist".
- In the test container `[OutOfBand]` handlers run inline and `SendEmail` (Fusonic) has no handler: test handlers that send emails by constructing them with a substituted `IMediator`.

## Gamification & Badge System

### Overview

There are two kinds of badges, both **scoped to a season** and per-club (not per-play-mode):

- **Tier badges** — earned automatically by completing bookings within a season. Each season has its own set of tiers (e.g., Bronze, Silver, Gold) with configurable thresholds.
- **One-time badges** — manually awarded by club staff for special achievements (e.g., club champion). They have no level/threshold.

### Domain Model

- `BadgeTier` (`TenantDomainEntity`) — a badge variant for a club **and season**. Has `SeasonId`, `Name`, `Level` (int, higher = better), `MatchesRequired` (int threshold), `Description`, `SortOrder`. Unique index on `(ClubId, SeasonId, Level)`; FK to `Season` with cascade delete. Image is stored separately in `BadgeTierImage`.
- `MemberBadge` — awarded to a member when they reach a badge tier threshold within a season. Linked to `MemberId`, `BadgeTierId`, `SeasonId`, `EarnedAt`. Raises `MemberBadgeAwardedDomainEvent`.
- `OneTimeBadge` (`TenantDomainEntity`) — `SeasonId`, `Name`, `Description`. Unique index on `(ClubId, SeasonId, Name)`; cascade-deleted with its club/season. Image is stored separately in `OneTimeBadgeImage` (keyed by `OneTimeBadgeId`).
- `MemberOneTimeBadge` — award of a one-time badge to a member: `MemberId`, `OneTimeBadgeId`, `AwardedAt`. Unique on `(MemberId, OneTimeBadgeId)`. Raises `MemberOneTimeBadgeAwardedDomainEvent`.
- `MemberBadgeSettings` — the member's display preferences: `DisplayBadgeId` (a `MemberBadge.Id`), `DisplayOneTimeBadgeId` (a `MemberOneTimeBadge.Id`, FK with `SetNull` on delete), `TrophyCasePublic` (bool).
    - **Only one display badge at a time**: `SetDisplayBadge` clears `DisplayOneTimeBadgeId` and vice versa. `UpdateMemberBadgeSettings` rejects requests that set both (`ErrorCode.CannotSetBothDisplayBadgeTypes`) and ignores badge ids that don't belong to the member.

### Seasonal Badge Tiers

- All tier queries (`GetBadgeTiers`, `BadgeProgressionService`, `GetMemberBadgeProgress`) filter by `ClubId` **and** `SeasonId`. `GET BadgeTiers` requires a `seasonId` query parameter.
- `CreateBadgeTier` assigns the next `Level` (max level in that season + 1) automatically; `SortOrder` = `Level`.
- `DeleteBadgeTier` is rejected once any member has earned the tier (`ErrorCode.BadgeTierAlreadyEarned`).
- `CopyBadgeTiers(SourceSeasonId, TargetSeasonId)` copies tiers + images into another season. Fails if the source has no tiers or the target already has tiers.
- Migration `MakeBadgeTiersSeasonal` split the former club-global tiers into one copy per existing season, re-pointed existing `MemberBadges` to the copy of the season they were earned in, and deleted the old season-less tiers. Its `Down()` only reverts the schema, not the data.

### One-Time Badges

- `OneTimeBadgesController` (`api/Clubs/{clubId}/OneTimeBadges`): list per season (`GetOneTimeBadges`, includes awardees), create/update/delete, copy between seasons, upload image, award (`AwardOneTimeBadge` with a list of `MemberIds`; already-awarded members are skipped), revoke (`RevokeOneTimeBadge`, no-op if not awarded), and `season-members` (`GetSeasonClubMembers` — `ClubMember`s enrolled in the season, used as candidates in the award dialog).
- `DeleteOneTimeBadge` is rejected once the badge has been awarded (`ErrorCode.OneTimeBadgeAlreadyAwarded`) — revoke all awards first.
- `CopyOneTimeBadges` copies only name/description/image, **never the awardees** (the previous season's champion must not carry over). Same precondition rules as `CopyBadgeTiers`.
- Image upload (`UploadOneTimeBadgeImage`, same as tier images): JPEG/PNG/WebP/GIF, max 5 MB, resized to max 128px and stored as JPEG via SkiaSharp.
- Management is restricted by the `AuthorizationPolicies.OneTimeBadgeManager` policy: `MemberRole.SportsDirector`, `YouthSportsDirector` or `Admin`. Reading (`GetOneTimeBadges`, image) only needs `AuthorizationPolicies.Member`. Tier management stays `ClubAdministrator`-only.
- One-time badges are included (as `OneTimeBadges` lists of `OneTimeBadgeAwardDto`) in `GetMemberBadges`, `GetMemberBadgeProgress` (current season only) and `GetTrophyCase`. They have no level, so `DisplayBadgeLevel` / `BadgeLevel` is `null` when a one-time badge is displayed (`BadgeIcon` renders `Color.Tertiary` for `null`).

### Badge Awarding

- `BadgeProgressionService.CheckAndAwardBadges` handles the tier awarding logic: counts completed bookings for the member in the current season (only in play modes with `IsChargingBookingSubscription = true`), compares against each of **that season's** `BadgeTier.MatchesRequired`, and creates missing `MemberBadge` records.
- Awarding is triggered **inline** when `GetMemberBadgeProgress` is called (i.e., when the member loads their badge progress in My Club). There is no background job — the check runs on demand.
- An EF Core migration (`AddMissingMemberBadgesForPreviousSeasons`) backfills badges for past seasons based on historical booking counts.

### Badge Awarded Emails

- `SendMemberBadgeAwardedEmailHandler` and `SendMemberOneTimeBadgeAwardedEmailHandler` (in `Business/Badges/DomainEventHandlers`) handle the award domain events and send the club email `ClubEmailType.BadgeAwarded` (see Club Email Templates) with a link to the member's trophy case.
- The recipient is resolved via `EmailRecipients.ForMembers` (see Notifications): the member's own email, falling back to a parent's email (via `FamilyMembers`) for child members without an email, unless the owner of the address switched badge emails off. No email → no notification. `MemberEmailResolver.GetNotificationInfo` (`Business/Members`) applies the same fallback without preferences and is only for emails the member can't switch off.
- The badge image in the email uses the anonymous `.../public-image` endpoint (see Image Endpoint Security).

### Trophy Case

- `GetTrophyCase` query returns a member's earned tier badges and one-time badges grouped by club and season (`SeasonTrophiesDto` with `SeasonId`, `SeasonFrom`, `SeasonLabel`, `Badges`, `OneTimeBadges`). Seasons are ordered newest first; a season appears if it has either kind of badge.
- **Privacy**: If `TrophyCasePublic = false` and the viewer is not the owner, trophy/badge data is hidden but `ProfilePictureUrl` is still returned.
- `TrophyCase.razor` — self-view page (`/clubs/{id}/my-club/trophy-case`). Uses `MudTabs` with one tab per season ("Season {year}").
- `MemberProfile.razor` — public profile view. Also uses season tabs. Badge click opens `TrophyDetailDialog.razor`.
- `TrophyDetailDialog.razor` — shows large badge icon (96px via `ExtraStyle`), name, level (tier badges only), description, club, season, earned date.

### Badge Progress (My Club)

- `GetMemberBadgeProgress` returns the member's progress toward the next badge tier in the current season: `CurrentBadgeLevel`, `CurrentBadgeImageUrl`, `NextBadgeLevel`, `NextBadgeImageUrl`, `RequiredMatches`, `CurrentMatches`, `EarnedBadges` (list with season info), `OneTimeBadges` (current season), `DisplayOneTimeBadgeId`.
- `BadgeProgressCard.razor` — three-column layout: current badge icon + name (left) → progress bar + match count (center) → next badge icon at 55% opacity + name (right). Settings gear at top-right. Trophy case link at bottom.

### Badge Display Settings

- `BadgeSettingsDialog.razor` — a season dropdown (pre-selected to the season of the current display badge or the most recent season) and a badge dropdown listing both tier badges and one-time badges of that season. Options are a `BadgeOption(Id, IsOneTime)` record struct; saving sends either `DisplayBadgeId` or `DisplayOneTimeBadgeId` via `UpdateBadgeSettings`.
- `EarnedBadgeDto` / `OneTimeBadgeAwardDto` carry `SeasonId`, `SeasonLabel`, `SeasonFrom` (populated by joining the `Seasons` table in `GetMemberBadges`, `GetMemberBadgeProgress`, `GetTrophyCase`).

### Badge Settings Page (Club Administration)

- `Pages/ClubShell/Club/BadgeSettings.razor` (`/clubs/{id}/badge-settings`) — season selector (defaults to the active season) plus two tabs: **Tiers** (add/edit/delete/copy tiers, upload images; admin only) and **One-Time Badges** (add/edit/delete/copy, upload images, award/revoke).
- Dialogs live in `Pages/ClubShell/Club/Dialogs/`: `CreateBadgeTierDialog`, `CopyBadgeTiersDialog`, `CreateOneTimeBadgeDialog`, `CopyOneTimeBadgesDialog`, `AwardOneTimeBadgeDialog`.
- `NavMenu.razor` shows the Badge Settings link to admins and additionally to `SportsDirector` / `YouthSportsDirector`.

### Nav Menu Badge State Isolation

- **CRITICAL**: `BadgesStore.TrophyCase` is shared state that gets overwritten by any `LoadTrophyCase(memberId)` call (including when viewing other members' profiles). Never use `TrophyCase` for the nav menu avatar.
- `IBadgesStore` / `BadgesStore` expose separate isolated properties: `MyDisplayBadgeImageUrl`, `MyDisplayBadgeLevel`, `OnMyDisplayBadgeChanged`, and `LoadMyDisplayBadge(memberId)`.
- `LoadMyDisplayBadge` uses a distinct semaphore context name (`nameof(LoadMyDisplayBadge)`) so it never cancels or conflicts with `LoadTrophyCase`.
- `NavMenu.razor` subscribes to `OnMyDisplayBadgeChanged` and reads from `MyDisplayBadgeImageUrl` / `MyDisplayBadgeLevel` exclusively.
- After `UpdateBadgeSettings`, always call `LoadMyDisplayBadge` (not `LoadTrophyCase`) to refresh the nav badge.

### Image Endpoint Security

- Profile picture and badge image endpoints (`BadgeTiers/{tierId}/image`, `OneTimeBadges/{id}/image`) are protected by `[Authorize]` (cookie-based auth) — the browser sends the auth cookie automatically with `<img src>` requests, so no frontend changes are needed.
- **Exception**: `BadgeTiers/{tierId}/public-image` and `OneTimeBadges/{id}/public-image` are `[AllowAnonymous]` on purpose, because they are embedded in badge-awarded emails and mail clients fetch them without a session. They use `[SecurityHeadersPolicy(SecurityHeadersConfiguration.PublicAssetPolicy)]` (cross-origin resource policy) so mail clients can load them. Use the public variant only for emails; the app itself uses the authorized endpoints.

### UserAvatar Component

- `UserAvatar.razor` renders a `MudAvatar` inside a relative-positioned `<div>` with a small badge icon overlaid at the top-right corner.
- Parameters: `FirstName`, `LastName`, `ProfilePictureUrl`, `BadgeImageUrl`, `BadgeLevel` (`int?` — `null` for one-time badges), `Size`, `Class` (outer div), `AvatarClass` (inner `MudAvatar`).
- **Important**: Use `AvatarClass` (not `Class`) when applying CSS classes that should style the circular avatar (e.g., gold/silver/bronze leaderboard rings). `Class` applies to the outer wrapper div.

## Court Blockings

### Overview

Maintainers, sports directors and admins block one or more courts for a time window (tournament, maintenance, weather, weekly youth training). Blocked times are shown in the booking grid with the reason and can't be booked by anybody, not even with an overbooking play mode. This is separate from the single open/closed interval of a court (`Court.Inactive`, `EditCourtDialog`) and from bookings, so blockings never count toward statistics or badges.

### Domain Model (`Bookennis.Domain/Courts`)

- `CourtBlocking` (`TenantDomainEntity`, aggregate root): `Title` (the reason, max 100 characters), `StartDate`/`EndDate` + optional `StartTime`/`EndTime` (local times in `TimeZoneInfoId`, both `null` = all day), `RecurrenceIntervalWeeks?` + `RecurrenceEndDate?`, `Courts` (`CourtBlockingCourt`) and `Occurrences` (`CourtBlockingOccurrence` with a UTC `Interval`).
- A blocking lasts from the start date and time until the end date and time (one continuous window, also over several days). A recurring blocking repeats that window every n weeks until `RecurrenceEndDate` (last possible start date); the window must be shorter than the interval, at most `MaxOccurrences` (105).
- **The blocked time windows are materialized as occurrences** (calculated with the time zone, so a weekly blocking keeps its local time across daylight saving time). Always query `Occurrences` to check a time, never the dates/times of the blocking itself.
- `Update(BlockingData)` replaces all data; occurrences are only regenerated when the blocked time changed. It raises `CourtBlockedDomainEvent` when times or courts were added (not for renaming or removing a court).

### Bookings

- `BookingDomainService` rejects bookings with `ErrorCode.CourtBlocked` (error detail = titles) when `Context.CourtBlockingTitles` is not empty. `BookCourt`, `EditBooking`, `BookRecurringCourt` (first occurrence) and `EditRecurringBooking` (single edit) fill it with `context.GetCourtBlockingTitles(courtId, interval)`.
- Generating recurring bookings (`BookRecurringCourt`, `EditRecurringBooking` series edit) skips blocked occurrences (`context.IsCourtBlocked`), like court conflicts.
- **Deleting bookings**: `DeleteBookingsOnCourtBlocking` (`CourtBlockedDomainEvent`, built on `DeleteBookingsNotificationHandler`) deletes the bookings on the blocked courts that intersect an occurrence and **have not started yet**, and notifies the players (`BookingDeleted` club email with reason `ReasonCourtBlocked` + title, push). Bookings that started or are over are kept.
- Deleting has to be confirmed: `CreateCourtBlocking`/`UpdateCourtBlocking` throw `CourtBlockingHasConflictingBookings` when there are such bookings and `DeleteConflictingBookings` is not set. `GetCourtBlockingConflicts` returns them for an unsaved blocking. All three use `CourtBlockingQueryExtensions.GetConflictingBookings`.

### API / Authorization

- `CourtBlockingsController` (`api/Clubs/{clubId}/CourtBlockings`): `GET ?dayFrom=&dayTo=` (occurrences for the booking grid, `AuthorizationPolicies.Member` - guests see them too), `GET manage?includePast=` (blockings as entered, upcoming first), `POST conflicts`, `POST`, `PUT {id}`, `DELETE {id}`.
- Managing requires `AuthorizationPolicies.CourtBlockingManager` (`Maintainer`, `SportsDirector`, `Admin` - not `YouthSportsDirector`). The FE mirrors this in `CourtBlockingHelpers.CanManage`.
- **The times in `SaveCourtBlockingModel`/`CourtBlockingDto` are `TimeSpan`s, not `TimeOnly`**: the client's `TimeOnlyJsonConverter` converts `TimeOnly` to UTC time of day, which is wrong for local times that the server combines with a date and a time zone.

### FE

- `Pages/ClubShell/Club/CourtBlockings.razor` (`/clubs/{id}/court-blockings`, list with status, edit, delete, past blockings toggle) and `Dialogs/EditCourtBlockingDialog.razor`: reason with templates, courts, period, all day, repetition. Saving first loads the conflicts; if there are any, they are listed and the submit button turns into "Delete n bookings and block" (second click confirms). Changing an input asks the server again.
- `Components/Booking/Day.razor` renders the occurrences (`Blockings` parameter, `.day__blocking`, violet stripes with title and time). `Booking.razor` loads them with the bookings (`ICourtBlockingsStore.LoadOccurrences`), shows chips, a legend entry and a dot in the day strip, and a lock button to the management page for managers.
- `NavMenu.razor` shows the link in the admin group and to maintainers and sports directors. Texts are in `CourtBlockingsLocale(.de).resx`.

## Play Mode Quotas

### Overview

Some play modes have a `MaxBookingsPerSeason` (int?) limit. Members can track their usage on the Statistics page.

### Statistics Page Integration

- `SeasonStatisticsResult` includes `List<PlayModeQuotaEntry> PlayModeQuotas` (season-specific, not in All-Time tab).
- `PlayModeQuotaEntry`: `PlayModeName`, `MaxBookingsPerSeason`, `UsedBookings`.
- `GetMemberStatistics` now requires `ClubId` (passed from `[FromRoute]` in `StatisticsController`) to scope play mode queries to the current club.
- Only play modes with `MaxBookingsPerSeason` set AND accessible to the member (via `PlayMode.AllowedRoles` intersected with `Member.UserRoles`, or admin) are included.
- Quotas are always shown for activated seasons (even when `UsedBookings = 0`).
- Progress bar color: Primary (< 75%), Warning (75–99%), Error (≥ 100%).

## Subscription Planner (Abo-Planer)

### Overview

Club members (not guests) plan shared winter court subscriptions: a date range, free-text participants (no club membership needed) with a percentage share, weeks nobody plays, per-participant unavailable weeks and a fixed number of players per week. The planner computes a fair weekly schedule that can be edited manually and exported to Excel.

### Domain Model (`Bookennis.Domain/SubscriptionPlans`)

- `SubscriptionPlan` (`TenantDomainEntity`, aggregate root): `OwnerUserId`, `Name`, `StartDate`/`EndDate` (inclusive), `PlayersPerWeek`, `ExcludedWeeks` (`List<DateOnly>` of ISO-week Mondays, Postgres `date[]`), `Participants`, `Assignments`.
- `SubscriptionPlanParticipant`: `Name`, `Percentage` (1–100, relative weight), `ColorIndex` (into `SubscriptionPlanColors.Palette` in Shared), `SortOrder`, `UnavailableWeeks`.
- `SubscriptionPlanAssignment`: `WeekStart` (Monday) + participant.
- Weeks are ISO calendar weeks overlapping the range (`Bookennis.Global.CalendarWeeks`). Week lists passed to the domain are normalized to Mondays inside the range.
- `Update(...)` replaces all inputs and **clears the schedule** when a scheduling-relevant input changed (dates, players per week, excluded weeks, participants added/removed, percentages, unavailable weeks). Renaming or recoloring keeps it. The FE asks for confirmation before saving such changes.
- Plans are private: the `OwnsSubscriptionPlan` policy (`SubscriptionPlanAuthorizationHandler`) only allows the creating user. The controller itself requires `AuthorizationPolicies.ClubMember`.

### Scheduling (`SubscriptionScheduler`, `ISubscriptionScheduler`)

- Pure domain service. Targets: all slots are split proportionally to the percentages, capped by each participant's availability (surplus goes to the others). Expected pair counts are proportional to `t_i * t_j`.
- Greedy chronological construction (participants furthest behind their even pace first) followed by simulated annealing (replace/swap moves) minimizing `50 * Σ(count - target)² + Σ(pairCount - expected)² + 3 * spacing`, with 4 restarts (2 for large plans). A seed makes it deterministic; the "compute again" button sends no seed.
- **Spacing**: for every stretch of 2..N consecutive active weeks (N ≈ 2 / min(pace, 1 - pace), max 12) a participant should play `pace * availableWeeksInStretch ± 0.75` games; each game outside that band counts squared. This prevents long streaks and long breaks. Unavailable weeks reduce the fair share of a stretch, so forced breaks are not penalized, and the tolerance avoids rigid rotations that would split the group into halves that never meet. Excluded weeks don't count as a break.
- Weeks with fewer available participants than `PlayersPerWeek` are filled as far as possible and flagged `IsUnderstaffed`.

### API / FE

- `SubscriptionPlansController` (`api/Clubs/{clubId}/SubscriptionPlans`): list, get, create, update (full inputs), delete, `POST {id}/compute`, `PUT {id}/schedule` (manual swaps), `GET {id}/export` (ClosedXML workbook: schedule, overview matrix, statistics; texts localized via `Resources/Localization/Export.SubscriptionPlan(.de).resx` using the request culture).
- `SubscriptionPlanMapper.ToDto` computes the per-participant counts/targets and the pair matrix. Every write endpoint returns the updated `SubscriptionPlanDto`.
- FE: `Pages/ClubShell/SubscriptionPlanner` — plan list and a `MudStepper` wizard (period → participants → weeks off → availability → schedule). Inputs are held in `SubscriptionPlanDraft` and saved when changing steps or leaving the page. File downloads go through `App.downloadFileFromStream` in `js-helpers.js` and `HttpResultExtensions.AsFileHttpResult`.

## Club Email Templates

### Overview

Emails triggered by something happening in a club are club-agnostic and can be customized per club by club admins (EN + DE). Platform emails (`VerifyUser`, `ForgotPassword`, `ConfirmEmailChange`, `EmailChanged`) are not club emails and stay fixed Razor views.

- `ClubEmailType` (Domain + a copy in Shared): `Welcome`, `SeasonActivated`, `BadgeAwarded`, `GuestCard`, `BookingDeleted`, `Announcement` (see Club Announcements), `BookingAdded`, `BookingReminder`, `ClubEventCreated`, `ClubEventReminder`, `ClubEventRegistrationDeadline` (see Notifications).
- `ClubEmailTemplate` (`TenantDomainEntity`): `Type`, `Language`, `Subject`, `Body`. Unique on `(ClubId, Type, Language)`. No row = built-in default.
- `Club.WebsiteUrl` (`{{ club.website_url }}`) and `Club.ReplyToEmail` (used as `ReplyTo` of every club email), set via `UpdateClubEmailSettings`.
- Built-in defaults: embedded resources `Resources/EmailTemplates/{Type}.{German|English}.md` (first line = subject, rest = body), loaded by `ClubEmailDefaultTemplates`. They must stay club neutral (no club names or URLs).

### Sending

- **Never send club emails with `SendEmail` directly.** Build the email's variables record and send `SendClubEmail(ClubId, Type, Recipient, RecipientDisplayName, Language, Variables)`. Files are attached with the optional `Attachments` parameter (see Club Announcements > Sending as email).
- `SendClubEmail` loads the club (fills `ClubVariables`), resolves the template (`ClubEmailTemplateResolver`: club template in the recipient's language → club template in the other language → default), renders it and sends the generic `Views/Emails/ClubEmail.cshtml` (`ClubEmailViewModel(Title, BodyHtml)`). If a saved template fails to render, it falls back to the default.
- Fusonic uses `SubjectKey` as the subject when no resource matches and runs it through `string.Format`, so `SendClubEmail` escapes `{`/`}` in the rendered subject.

### Rendering (`ClubEmailRenderer`, `IClubEmailRenderer`)

- Pipeline: Liquid (Fluid) → Markdown (Markdig, `DisableHtml`, generic attributes for `[text](url){.btn}` buttons) → `HtmlSanitizer` (http/https/mailto only).
- String values are Markdown-escaped before they are inserted into the body, so member input can't inject links/formatting. The subject is rendered as plain text. Text that is meant to be Markdown (written by someone who may format the email, e.g. the announcement text) is passed as `MarkdownText` and inserted as it is.
- `Validate` walks the Liquid AST and rejects variables that are not part of the email type (`ClubEmailTemplateErrorCode.UnknownEmailTemplateVariable`) and syntax errors (`InvalidEmailTemplateSyntax`). `UpdateClubEmailTemplate` validates and test-renders before saving.
- **Variables are an allowlist**: only the public properties of the records in `ClubEmailVariables.cs` are exposed (snake_case, e.g. `MemberVariables.FirstName` → `{{ member.first_name }}`). Never expose entities or data the recipient must not see.

### Adding a new club email

1. Add the value to both `ClubEmailType` enums (Domain and Shared).
2. Add a `…EmailVariables` record (inherits `ClubEmailVariables`) in `ClubEmailVariables.cs` and an entry with sample data in `ClubEmailCatalog.All`.
3. Add `Resources/EmailTemplates/{Type}.German.md` and `.English.md`.
4. Send it via `SendClubEmail` from the handler.
5. FE: add `Type_…`/`TypeDescription_…` and `Variable_…` entries to `ClubEmailTemplatesLocale(.de).resx` and an emoji in `EmailTypeInfo`.

### API / FE

- `ClubEmailTemplatesController` (`api/Clubs/{clubId}/ClubEmailTemplates`, `ClubAdministrator` policy): list + settings, `PUT settings`, get/update/delete (reset) per type, `POST {type}/preview` (sample data, not persisted), `POST {type}/test` (sends the unsaved template to the current user).
- FE: `Pages/ClubShell/Club/EmailTemplates/EmailTemplates.razor` (`/clubs/{id}/email-templates`, list + club details) and `EmailTemplateEditor.razor` (`/clubs/{id}/email-templates/{type}`): language chips, subject/body editor, variable chips that insert at the cursor (`App.insertAtCursor` in `js-helpers.js`), formatting help, debounced live preview (side by side ≥ 1100px, Edit/Preview switch below). A language is saved as customized only if its text differs from the default. The preview intentionally stays light in dark mode, because it shows the email as it looks in a mail client.

## Club Calendar & Events

### Overview

Clubs plan events (work efforts, parties, tournaments, meetings) in a club calendar. Club members see upcoming events, open the details and optionally register with a head count (including themselves) and answers to the organizer's questions. Guests (guest sessions / `GuestMember`) have no access.

### Domain Model (`Bookennis.Domain/ClubEvents`)

- `ClubEvent` (`TenantDomainEntity`, aggregate root): `Title`, `Description?` (Markdown, max 4,000 characters), `Location?`, `Category` (`WorkEffort`, `Social`, `Tournament`, `Meeting`, `Other`), `StartDate` + optional `EndDate` (multi-day), optional club-local `StartTime`/`EndTime` (no time zone math, `null` = all day), `RegistrationEnabled`, `MaxParticipants?` (cap on the **total head count**), `RegistrationDeadline?` (stored in UTC), `NotifyMembers` (organizer's choice in the editor, default on: announce the event and remind of the deadline, see Notifications), `ReminderSentAt?`/`DeadlineReminderSentAt?` (reset when the start or the deadline is moved), `CreatedByMemberId?`, `Questions`, `Registrations`.
- `ClubEventQuestion`: `Text`, `SelectionMode` (`SingleChoice`/`MultipleChoice`), `IsRequired`, `AllowQuantities` (only for multiple choice: members enter a number per option, e.g. 2x Schnitzel), `LimitQuantityToHeadCount` (default `true`: a quantity can't exceed the registration's head count, e.g. meals; `false`: up to `ClubEvent.MaxQuantity` = 99, e.g. drinks; always `true` without quantities), `Options` (`ClubEventQuestionOption`: `Label`).
- `ClubEventRegistration`: `MemberId` (a `ClubMember`), `HeadCount` (1-50), `Comment?`, `RegisteredAt`, `Answers` (`ClubEventRegistrationAnswer`: option id + `Quantity`, 1 unless the question allows quantities, then 1..HeadCount). Unique on `(ClubEventId, MemberId)`.
- `Update(EventData)` syncs questions/options by id (like `SubscriptionPlan.SetParticipants`); answers to removed options are deleted, registrations are kept. Disabling the registration keeps existing registrations.
- `Register` creates or replaces the member's registration and validates: registration enabled, deadline not passed, event not over, capacity (others + new head count ≤ max, `ClubEventFull` carries the remaining places), required questions answered, single choice ≤ 1 option, options belong to the event. `Unregister` is blocked after the deadline; organizers use `RemoveRegistration` (no deadline check).

### API / Authorization

- `ClubEventsController` (`api/Clubs/{clubId}/ClubEvents`): list by date range (`GET ?from=&to=`, events overlapping the range), detail, create/update/delete, `PUT/DELETE {id}/registration` (own registration), `DELETE {id}/registrations/{registrationId}` (organizer), `GET {id}/export` (ClosedXML, texts in `Resources/Localization/Export.ClubEvent(.de).resx`).
- Reading and registering require `AuthorizationPolicies.ClubMember`. Planning requires `AuthorizationPolicies.ClubEventManager` (`Maintainer`, `SportsDirector`, `YouthSportsDirector`, `Admin`). Policies are set per action, not on the controller. The FE mirrors this in `ClubEventHelpers.CanManage`.
- The current member is resolved with `ClubEventQueryExtensions.GetClubMemberId` (`MemberType.ClubMember`, never `Single`).
- The description is Markdown and rendered like the announcement text (`IClubEmailRenderer.RenderMarkdown`, see Club Announcements): `ClubEventDto` carries `Description` (Markdown, for the editor) and `DescriptionHtml`. `POST preview` (`PreviewClubEventDescription`, `ClubEventManager`) renders an unsaved description for the editor. The FE shows it with `AnnouncementBody`.
- `SaveClubEventModel` has nullable texts on purpose: MVC treats non-nullable strings as required and would answer with a generic 400 instead of the domain's localized error code.

### FE

- `Pages/ClubShell/Calendar`: `Calendar.razor` (`/clubs/{id}/calendar`, hero with the next event, "Upcoming" list grouped by month with a past-events toggle, and a CSS-grid `MonthCalendar` that shows dots on phones), `ClubEventDetail.razor` (`/clubs/{id}/calendar/{eventId}`, share target), `ClubEventEditor.razor` (`/new` and `/{eventId}/edit`, Markdown description with an Edit/Preview switch and formatting help, question builder with templates). `UpcomingEvents.razor` shows the next 3 events on My Club (not for guests).
- `ClubEventsStore.UpcomingEvents` is isolated from `Events` (calendar range) so the calendar can't overwrite the My Club card.
- Sharing: `App.shareOrCopy` (native share sheet, clipboard fallback) and `App.copyToClipboard` in `js-helpers.js`, plus a `https://wa.me/?text=` link with title, date, location and the absolute event url (`ClubEventHelpers.ShareText`).

### Deep links / login return url

- Shared links require a login. `RedirectToLogin` and the 401 handler call `NavigationManager.NavigateToLoginAndReturn()`, which passes the current page as `returnUrl`. `LogIn.razor` only follows local return urls (`NavigationManagerExtensions.IsLocalReturnUrl`, prevents open redirects).
- `ClubWithStoreLayout` stops when the profile could not be loaded (not signed in) instead of redirecting to access denied, and `App.razor` keeps the club of a `/clubs/{id}/...` deep link after login instead of switching to the favorite club.

## Club Announcements (News)

### Overview

The club posts news for its members: a title and a Markdown text, optionally pinned, with an expiry date and attached files. Members see the news on My Club (latest 3) and in the news list. An announcement can additionally be sent as email (with its attachments) to a group of members. Guests (guest sessions / `GuestMember`) have no access.

### Domain Model (`Bookennis.Domain/ClubAnnouncements`)

- `ClubAnnouncement` (`TenantDomainEntity`, aggregate root): `Title`, `Body` (Markdown, max 10,000 characters), `IsPinned`, `ExpiresOn?` (last day it is shown to members, compared with the UTC date), `PublishedAt`, `CreatedByMemberId?`, `Attachments`, and the last email dispatch (`EmailSentAt?`, `EmailAudience?`, `EmailRecipientCount?`, set by `MarkEmailSent`).
- `ClubAnnouncementAttachment`: `FileName`, `ContentType`, `Size`. The file itself is in `ClubAnnouncementAttachmentContent` (own table, navigation `Content`), so including the attachments never loads the files - only load `Content` when the bytes are needed.
- `AddAttachment(fileName, data)` validates: file type by extension (`ClubAnnouncementAttachment.AllowedFileTypes`: PDF, images, Office/OpenDocument, txt, csv, ics - **the content type always comes from this map, never from the upload**), not empty, max 5 attachments, 5 MB per file, 10 MB in total (emails!). The file name is normalized (no path, no control characters, max 150 characters). The limits are mirrored in `ClubAnnouncementLimits` (Shared) for the FE.
- `ClubAnnouncementAudience` (Domain + a copy in Shared): `AllMembers`, `ActiveSeasonMembers`, `Youth` (under 18), `Roles` (members with one of the given `MemberRole`s).

### API / Authorization

- `ClubAnnouncementsController` (`api/Clubs/{clubId}/ClubAnnouncements`): list (`GET ?includeExpired=&take=`, pinned first, then newest), detail, create/update/delete, `POST preview` (renders unsaved Markdown), `POST|DELETE|GET {id}/attachments[/{attachmentId}]`, `POST email-recipients` (number of addresses of an audience) and `POST {id}/email`.
- Reading requires `AuthorizationPolicies.ClubMember`, writing and emailing `AuthorizationPolicies.ClubAnnouncementManager` (`Maintainer`, `SportsDirector`, `YouthSportsDirector`, `Admin`). Policies are set per action; the FE mirrors this in `NewsHelpers.CanManage`.
- **Expired announcements (and their attachments) only exist for managers.** The controller checks the manager policy with `IAuthorizationService` and passes `CanManage` to the read handlers, which also hide the email details from other members.
- The text is rendered by `IClubEmailRenderer.RenderMarkdown` (same Markdown pipeline and sanitizer as the emails, but links open in a new tab and images are removed because the CSP only allows images from the app). DTOs carry `Body` (Markdown, for the editor) and `BodyHtml`; list items carry a plain text `Excerpt`.
- Attachments are always served as a download (`Content-Disposition: attachment`), never inline.

### Sending as email

- `SendClubAnnouncementEmail` resolves the recipients (`ClubAnnouncementRecipients.Resolve`, built on `MemberFilterQuery`: one recipient per email address, children without an email are reached via a parent, the owner of an address is the one addressed; addresses of users who switched announcement emails off in their notification preferences are left out, also in the recipient count), stores the dispatch on the announcement and sends one `SendClubAnnouncementEmailBatch` (`[OutOfBand]`, Hangfire) per 50 recipients.
- The batch sends the club email `ClubEmailType.Announcement` via `SendClubEmail` to each recipient. `{{ announcement.body }}` is a `MarkdownText`, which is inserted into the template as Markdown instead of being escaped like plain strings.
- **Email attachments**: `SendClubEmail` has an optional `Attachments` list (`Fusonic.Extensions.Email.Attachment(name, uri)`). The email job only carries the uri; `ClubAnnouncementAttachmentResolver` (`IEmailAttachmentResolver`, appended in `SimpleInjectorConfiguration`) loads `club-announcement-attachment:{id}` from the database when the email is sent. For other sources add another resolver.
- Sending again is possible (the FE warns); nothing prevents a recipient from getting an announcement twice.

### FE

- `Pages/ClubShell/News`: `News.razor` (`/clubs/{id}/news`, list, managers can show expired ones), `NewsDetail.razor` (`/clubs/{id}/news/{announcementId}`, target of the email and push link; managers get a "Manage" card with the email status, send, edit, delete) and `NewsEditor.razor` (`/new` and `/{announcementId}/edit`: Markdown text with debounced preview, pin, expiry, attachments, and for new announcements "also send as email").
- Attachment changes in the editor are applied on save: create/update, then remove, then upload, then (optionally) send the email. Files are read into memory when they are selected, because the browser file is gone once the `InputFile` is re-created.
- `AudiencePicker` (audience + roles + live recipient count) is shared by the editor and `SendAnnouncementEmailDialog`. `AnnouncementBody` renders the sanitized html with the Markdown styles.
- `ClubAnnouncementsStore.LatestAnnouncements` (My Club card `LatestNews.razor`) is isolated from `Announcements` (news list). Texts are in `NewsLocale(.de).resx`.

## Account & Onboarding

- The account pages (`Pages/Account`: login, register, forgot/reset password, email confirmations) use `Components/Shared/AuthShell.razor`: a brand panel ("club management & booking for every club", **100% free**, feature list) plus the page card. Secondary pages pass `ShowFeatures="false"`; on phones the panel is compacted so the form stays above the fold.
- `PasswordRequirements.razor` shows the rules of `PasswordValidation.Rules` as a live checklist (use `PasswordField` with `Immediate="true"`).
- The address (`Street`, `City`, `ZipCode`) is optional everywhere (registration, profile, DTOs send `null`/empty). The profile completion banner only asks for first/last name, birthday and profile picture, and can be dismissed for good (`User.ProfileCompletionBannerDismissedAt`, `DismissProfileCompletionBanner`).
- Users change their own email via `RequestEmailChange` (current password required, lockout on failure) → `SendEmailChangeLink` mails a link to the new address → `ConfirmEmailChange` changes it and notifies the old address (`EmailChanged`). Admins use `ChangeUserEmail`, which shares `SendEmailChangeLink`. Password changes go through `ChangePassword`.
- **Guest sessions sign in the underlying user**, so credential changes require the `AuthorizationPolicies.AccountOwner` policy (rejects the `guest_session` claim, see `GuestSessionClaims`). `GetUserProfileResult.IsGuestSession` hides the Login & security card.
- The router re-creates the current page when the auth state is (re)notified, e.g. on app start. Pages that consume single-use tokens (`ConfirmEmail`, `ConfirmEmailChange`) share one request per token and the handlers are idempotent; never call `IAuthStateChanged.AuthStateChanged()` from `OnInitializedAsync` of such a page (it loops), reload with `forceLoad` instead. `ConfirmEmailChange` uses `NoNavigationLayout` because `NotAuthorizedLayout` redirects signed-in users.

## Login Protection, Rate Limiting & Health

- Failed password checks lock the account (`IdentityAndAuthenticationConfiguration`: 5 attempts, 15 minutes). Always call `CheckPasswordSignInAsync(..., lockoutOnFailure: true)` when checking a password (login, re-authentication for email change / account deletion). A successful `ResetPassword` lifts the lockout.
- Don't reveal whether an email is registered: `LoginUser` answers unknown email and wrong password with `InvalidCredentials` (and only reports `NotAllowed`/unconfirmed email when the password was right), `ConfirmEmail`/`ResetPassword` treat an unknown email like an invalid token, `ForgotPassword` always succeeds.
- `RateLimitingConfiguration` adds per-IP fixed-window policies: `AuthenticationPolicy` (10/min: login, guest login, confirm email, reset password, change password, delete account) and `EmailPolicy` (5/15 min: register, forgot password, change email). Apply them with `[EnableRateLimiting(...)]` on anonymous or credential-checking endpoints. Rejections are 429 with an `ErrorCodeResponse` (`TooManyRequests`), which `SnackbarExtensions.ShowErrorDetails` localizes. The client IP comes from `X-Forwarded-For` (`UseForwardedHeaders`, rightmost entry only).
- Swagger is only exposed in Development. `/health` is an anonymous health check (includes the database).

## Privacy (GDPR) & Legal Pages

- `Pages/Legal`: `/legal/imprint` and `/legal/privacy` (public, `NoNavigationLayout`), texts in `LegalLocale(.de).resx` (`LegalSection` renders lines starting with "- " as a list). The operator details differ per installation and are never committed: they come from the `Legal` section of the server settings (`AppSettings.Legal`, e.g. the environment variable `Legal__OperatorName`), are served by the anonymous `GET api/Legal` (`GetLegalSettings`) and loaded once by `ILegalStore`; the pages show a warning while the operator is not configured. Update `LegalDocument.LastUpdated` when the texts change. `LegalLinks` is shown in `AuthShell` and at the bottom of the `NavMenu`.
- Registration requires `AcceptPrivacyPolicy` (`User.PrivacyPolicyAcceptedAt`, `ErrorCode.PrivacyPolicyNotAccepted`).
- Self-service in the profile (`AccountOwner` policy, not for guest sessions): `GET api/Profile/export` (`ExportPersonalData`, JSON download of all personal data - extend `PersonalDataExport` when adding new personal data), `POST api/Profile/LeaveClub` (`LeaveClub`, removes the `ClubMember` only, never a `GuestMember`) and `POST api/Profile/DeleteAccount` (`DeleteOwnAccount`, requires the password, signs out).
- Removing members/users always goes through `MemberRemoval` (`Business/Members`) and `UserDeletion` (`Business/Users`, also used by the admin `DeleteAdminUser`): `FamilyMember.MemberId` has no FK, so family links are removed explicitly and families without a parent are deleted; upcoming bookings/recurring series without remaining players are deleted; children (`BelongsToUserId`) are handed over to another parent of their family or deleted. The last admin of a club can't leave it or delete the account (`LastClubAdmin`).
- A member can only be in one family (unique index on `FamilyMembers.MemberId`).

## Security Headers

- All responses get security headers from `NetEscapades.AspNetCore.SecurityHeaders`, configured in `Infrastructure/Configuration/SecurityHeadersConfiguration.cs` (`builder.AddSecurityHeaders()` + `app.UseSecurityHeaders()`, registered first in the pipeline). It also emits HSTS, so there is no separate `app.UseHsts()`.
- The default policy includes a strict **Content Security Policy**: scripts only from `'self'` (+ `'wasm-unsafe-eval'` for Blazor WASM), styles from `'self'`, inline and Google Fonts, fonts from `'self'`/`fonts.gstatic.com`, images from `'self'`/`data:`, `frame-ancestors 'none'`.
    - **No inline `<script>` blocks** in `index.html` or elsewhere — put JS in a file under `wwwroot` (e.g. `pwa.js`, `js-helpers.js`).
    - Any new external script/style/font/image/API origin must be added to the CSP in `SecurityHeadersConfiguration`, otherwise the browser blocks it.
- Endpoints that must be loadable cross-origin (e.g. images embedded in emails) use `[SecurityHeadersPolicy(SecurityHeadersConfiguration.PublicAssetPolicy)]`.

## Branding

- The product is called **Clubnetz** (future domain `clubnetz.app`). All user-visible texts, the app manifest, emails and notifications use this name. Namespaces, project names and storage keys still say `Bookennis` - don't rename them and never show that name to users.

## Installable App (PWA) & Staleness

- The app is installable: `wwwroot/manifest.webmanifest`, icons in `wwwroot/icons` (`icon.svg` / `icon-maskable.svg` / `badge.svg` are the sources of the PNGs and of `favicon.ico`).
- **`wwwroot/service-worker.js` must never get a `fetch` handler or use Cache Storage.** It only handles push notifications, so every request goes to the network and the HTTP cache headers set in `Program.cs` (`no-cache` for everything except the fingerprinted `_framework`/`_content` files) stay the single source of truth. A caching worker is what made deployments show up late in the past. There is no offline mode on purpose.
- `wwwroot/pwa.js` registers the worker (`updateViaCache: 'none'`) and holds the browser side of installing (`App.install`), push (`App.push`) and reloading (`App.appUpdate`).
- **Version check**: every API response carries `X-App-Version` (`AppVersionConfiguration`, the assembly's informational version = `1.0.0+<commit>`). The client compares it with its own version in `AppVersionHandler` (added to all HttpClients via `ConfigureHttpClientDefaults`) / `AppVersionService`. On a mismatch `AppUpdateNotifier` (in `BaseLayout`) shows a "new version" snackbar and loads the next navigation with a full page load. `App.appUpdate` remembers the version in sessionStorage so a persistent mismatch can't cause a reload loop.

## Push Notifications

### Overview

Web Push (VAPID) to the browsers/installed apps of a user. Notifications are per device: the user switches them on in the profile (`AppAndNotificationsCard`). On iOS they only work in the installed app.

- Settings: `AppSettings.Push` (`PushSettings`: `PublicKey`, `PrivateKey`, `Subject`, `BookingReminderLeadMinutes`). Without keys push is switched off everywhere (nothing is sent, the FE shows "not available"). Create keys with `scripts/New-VapidKeys.ps1`; **never replace the keys of a running environment**, all subscriptions are bound to them.
- `PushSubscription` (`Bookennis.Domain/User`, not tenant specific): `UserId`, `Endpoint` (unique - a device belongs to whoever signed in on it last), `P256dh`, `Auth`. Deleted with the user (cascade), listed in `ExportPersonalData` (`PushDevices`).
- `PushController` (`api/Push`): `GET Configuration`, `PUT Subscription`, `POST Unsubscribe`, `POST Test`.
- `SavePushSubscription` only accepts endpoints of the browser vendors' push services (`PushEndpoint.IsAllowed`) because the server posts to that url (SSRF). Add new push services there.

### Sending

- **Never call `IPushSender` or `SendPushNotification` directly.** Resolve the recipients with `PushRecipients` (`ForMembers` maps children without login to the user they belong to, `ForClub`, `ForUsers`; only users with a device are returned, and not those who switched push off for the `NotificationType` you pass - see Notifications) and call `IPushNotificationService.Notify(recipients, culture => new PushNotification(title, body, url, tag), ct)`. It creates the notification once per language and sends `SendPushNotification`, an `[OutOfBand]` (Hangfire) command that delivers to all devices and removes subscriptions the push service reports as gone (404/410).
- Texts live in `Resources/Localization/Push(.de).resx` and are read with `PushTexts.Get(culture, key, args)`. Use `PushTexts.DateAndTime`/`Date`/`Time` for dates (pass the time in the booking's time zone). Keep notifications short and neutral - they are also delivered to parents of child members.
- `Url` is a relative path of the app; the service worker opens it on tap. `Tag` makes notifications replace each other (e.g. `booking-{id}`).
- Don't notify the user who caused the change (pass their id as `exceptUserId`).

### Push notification types

Every one of them has a `NotificationType` and, except where noted, an email counterpart (see Notifications).

- **Booking added**: `NotifyBookingPlayersHandler` (`BookingAddedDomainEvent`) -> `IBookingPushNotifier.BookingAdded`. Bookings of a series are skipped; the series is announced once via `EntityCreated<RecurringBookingSeries>` -> `RecurringBookingAdded`.
- **Booking deleted**: `DeleteBooking`, `DeleteRecurringBooking` (one notification for "this and following") and `DeleteBookingsNotificationHandler` (overbooked / court set inactive / court blocked) call `IBookingPushNotifier.BookingDeleted` / `RecurringBookingDeleted` **before** removing the booking, because the players are read from it.
- **Booking reminder**: `SendBookingReminders`, run every 5 minutes for all clubs by the recurring Hangfire job `BookingReminderJob` (registered in `Program.cs`). Reminds the players of bookings that start within `BookingReminderLeadMinutes` (default 120) by push and email and marks them with `Booking.ReminderSentAt` (reset when a booking is moved). Bookings made within the lead time are marked without a reminder. The emails are also sent when push is not configured.
- **Club event created**: `NotifyClubEventCreatedHandler` (`EntityCreated<ClubEvent>`) -> `IClubEventNotifier.EventCreated`, all club members except the creator. Not for events in the past or with `NotifyMembers = false`.
- **Club event reminder / registration deadline**: `SendClubEventReminders` (see Notifications).
- **Club announcement published**: `NotifyClubAnnouncementCreatedHandler` (`EntityCreated<ClubAnnouncement>`), all club members except the author; not for announcements that are already expired. The email is only sent when a manager sends the announcement as email.
- **Badge awarded**: `NotifyBadgeAwardedHandler` (tier and one-time badges).

### FE

- `IPushStore` / `PushStore` (`Services/Store/Push`): `Load()` (called from `App.razor` after sign-in and by the card) registers the device's subscription again on every start, because browsers replace or drop subscriptions. `Enable()` must be called directly from a click (permission dialog needs a user gesture). `DetachDevice()` is called before logout.
- The service worker shows the notification (`push` event) and opens `data.url` on tap (`notificationclick`). It must always show a notification for a push, browsers revoke subscriptions that receive pushes silently.
- Blazor caches the JS functions it calls: when debugging in the browser, replacing `App.push.getState` after the first call has no effect.

## Notifications (push + email) & Notification Preferences

### Overview

Members are notified about what happens to them by push notification and by email. For every `NotificationType` the user chooses the channels in the profile: push, email, both or none. The push side is described in Push Notifications, the emails are club emails (see Club Email Templates).

| `NotificationType` | Push | Email (`ClubEmailType`) | Recipients |
| --- | --- | --- | --- |
| `BookingAdded` | `IBookingPushNotifier` | `BookingAdded` | the other players (a series is announced once) |
| `BookingDeleted` | `IBookingPushNotifier` | `BookingDeleted` | the other players; by a player or by the system (overbooked, court inactive/blocked) |
| `BookingReminder` | `SendBookingReminders` | `BookingReminder` | all players |
| `ClubEventCreated` | `IClubEventNotifier` | `ClubEventCreated` | all club members except the creator |
| `ClubEventReminder` | `IClubEventNotifier` | `ClubEventReminder` | the registered members |
| `ClubEventRegistrationDeadline` | `IClubEventNotifier` | `ClubEventRegistrationDeadline` | club members who have not registered |
| `ClubAnnouncement` | `NotifyClubAnnouncementCreatedHandler` | `Announcement` (only when a manager sends it) | club members / the chosen audience |
| `BadgeAwarded` | `NotifyBadgeAwardedHandler` | `BadgeAwarded` | the member |

`Welcome`, `SeasonActivated` and `GuestCard` (contains the login link) are not notifications: they are always sent and have no preference.

### Preferences

- `NotificationPreference` (`Bookennis.Domain/Notifications`, per user, not tenant specific): `UserId`, `Type`, `Push`, `Email`. Unique on `(UserId, Type)`, deleted with the user, part of `ExportPersonalData`.
- **No row = default, and both defaults are "on"** (`NotificationPreference.DefaultPush`/`DefaultEmail`). The recipient queries only look for rows that switch a channel off, so a default can't be changed to "off" without changing those queries.
- `NotificationType` exists in Domain and Shared (keep in sync). `NotificationPreferencesController` (`api/NotificationPreferences`): `GET` (one entry per type) and `PUT` (`UpdateNotificationPreferences`, types that are missing keep their channels).
- Preferences belong to the user who receives the notification: a parent's preferences also apply to what they receive for their children.

### Resolving recipients

- **Always pass the `NotificationType`** to `PushRecipients.ForMembers/ForClub/ForUsers` and resolve email recipients with `EmailRecipients.ForMembers/ForClub` (`Business/Notifications`). Both leave out users who switched the channel off. Never collect email addresses of members yourself for a notification.
- `EmailRecipients` returns one `EmailRecipient` per address (`MemberId`, `UserId` = owner of the address, `Email`, the member's name and language). Children without an own email are reached via a parent (`FamilyMembers`, same rule as the members list); if a parent and their child are both recipients, only the parent is addressed. `exceptUserIds` removes the user who caused the change.
- For bookings, `IBookingEmailNotifier` (`BookingAdded`, `RecurringBookingAdded`, `BookingDeleted`, `RecurringBookingDeleted`) is the email counterpart of `IBookingPushNotifier`; call both next to each other. `DeleteBookingsNotificationHandler` sends its own `BookingDeleted` emails with the reason.
- `IClubEventNotifier` (`EventCreated`, `EventReminder`, `RegistrationDeadlineReminder`) sends push and email for club events. The emails go out in `SendClubEventEmailBatch` jobs (`[OutOfBand]`, 50 recipients each), like announcements.
- Text fragments that are passed to the emails as variables (e.g. "every Tuesday from ...", the cancellation reason) are in `Resources/Localization/Notifications(.de).resx` (`NotificationTexts`).

### Club event reminders

- `SendClubEventReminders`, run every 15 minutes by the recurring Hangfire job `ClubEventReminderJob` (registered in `Program.cs`), lead time 24 hours for both reminders.
- **Event reminder**: registered members of events that start within the lead time (all-day events count as starting at 08:00), marked with `ClubEvent.ReminderSentAt`.
- **Registration deadline reminder**: club members who have not registered, when the deadline ends within the lead time, marked with `ClubEvent.DeadlineReminderSentAt`. Not for events with `NotifyMembers = false` or that are full.
- Events planned (or deadlines set) within the lead time are marked without a reminder.
- Club events have club-local dates and times without a time zone. `ClubEventInfo.TimeZone` (Europe/Vienna, shared by all supported countries) is used to tell when an event starts and to show the deadline in emails and push notifications.
- In tests, `IBookingEmailNotifier` is a substitute in `TestFixture` (`SendEmail` has no handler); test the notifiers directly with a substituted `IMediator`.

### FE

- `INotificationPreferencesStore` / `NotificationPreferencesStore` (`Services/Store/Notifications`) and `Pages/Account/Components/NotificationPreferencesCard.razor` in the profile (below `AppAndNotificationsCard`, which stays per device): one row per type with a switch per channel, saved immediately; the column headers switch a whole channel on or off. Texts are `Notif_*` in `AccountLocale(.de).resx`.
- **Adding a notification type**: add it to both `NotificationType` enums, to `Groups` in `NotificationPreferencesCard` and `Notif_Type_…`/`Notif_Hint_…` to `AccountLocale(.de).resx`, and pass it when resolving the recipients.

## Build & Dependencies

- The .NET SDK version is pinned in `global.json` at the repo root.
- NuGet versions are managed centrally in `src/Directory.Packages.props`. Vulnerable transitive dependencies are pinned to patched versions by adding a direct `PackageReference` (e.g. `AngleSharp`, `Microsoft.OpenApi` in `Bookennis.Api.csproj`) — keep these until the parent packages ship fixed versions.
- `.claude/launch.json` defines the `bookennis-api` dev server (`dotnet run` on `https://localhost:5000`).

## Frontend Misc

- `PlayerSelection.razor` search splits the input on whitespace; every term must match the start of the first or last name, so "first last" and "last first" both work.

## Dark Mode

- The FE supports **System / Light / Dark** (`ThemeToggle.razor` in the app bar of every layout). `ThemeService` (`Services/Theme`) holds the mode; `wwwroot/theme.js` stores it in localStorage (`bookennis-theme`) and sets `data-theme="dark|light"` on `<html>` before Blazor starts.
- Both palettes live in `Shared/BaseLayout.razor` (`PaletteLight` / `PaletteDark`). When adding a palette color, set it in both.
- **Every new FE feature must look good in light AND dark mode.** Prefer `var(--mud-palette-*)` (e.g. `surface`, `text-primary`, `text-secondary`, `lines-default`, `background-gray`) over hardcoded colors — they switch automatically.
- Never use hardcoded white backgrounds (`#FFFFFF`) or dark text colors (`#0F172A`, `#475569`, ...) on neutral surfaces. Exception: white pills/text on the colored hero gradients, which work in both modes.
- For accent text colors that are too dark on a dark surface, use the dark-only tokens from `wwwroot/css/app.css` with the light color as fallback, e.g. `color: var(--bk-dark-red, #B91C1C);` (`green`, `amber`, `red`, `blue`, `indigo`, `violet`, `pink`, heatmap levels `heat-1..4`, `day-0`, `heat-mix`). They are only defined under `:root[data-theme="dark"]`, so light mode stays unchanged.
- For component-specific tweaks, add a scoped rule: `:root[data-theme="dark"] .my-class { ... }`.
- Heatmaps/intensity scales must go from dim (low) to bright (high) in dark mode.
- Verify UI changes in both modes (toggle in the app bar, or set `localStorage['bookennis-theme']` to `light`/`dark`).
