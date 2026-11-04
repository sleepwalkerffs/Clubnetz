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
- Every feature that adds or changes an endpoint, a way to sign in, or data that belongs to a club has to hold up in the security tests (`Bookennis.Api.Tests/Security`, see "Security tests" below): run them, seed the new data in `ApiSecurityFixture` so the sweeps reach it, and add an attack test for every new rule about who may do what. A query that uses `IgnoreQueryFilters()` must filter its root entity by the club itself.
- Committing and opening pull requests is fine. Work on a branch, not on `master`, and never push to `stage` or `production` without being asked: those branches deploy.

## Members, Former Guests & Active Members

### Member Hierarchy

- `Member` (abstract) → `ClubMember` and `GuestMember` (TPH table `Members`, discriminator `MemberType`).
- **The guest system was removed** (guest cards, guest links, guest sessions, `GuestsController`, `LoginGuest`). Migration `AddCourtSales` turned the bookings left on guest cards into booking credits on a `ClubMember` (see Court Sales) and set `IsAllowedToBook = false` on every guest member.
- `GuestMember` and `MemberType.GuestMember`/`MemberRole.Guest` stay only for history: historic bookings reference guest members, leaderboards and histories still show them. A guest member gives **no rights**: `AuthorizationHandlerService.GetMember`, `GetClubProfile`, `GetBookingOptions` and `BecomeClubMember` only look at the `ClubMember` of a user. Never create guest members.
- A user can still have both a `ClubMember` and an old `GuestMember` for the same club: never use `Single*` on `Set<Member>()` filtered by `UserId`; filter by `MemberType.ClubMember` (or use `ClubMembers`) and `FirstOrDefault` with an `OrderBy`.
- `ClubMember.CanParticipate` is always `false` — ClubMembers book for free only if enrolled in the active season (`MemberSeasons`); members who are not enrolled buy court time (see Court Sales).
- Former guests have no password: they sign in after "Forgot password" (`ResetPassword` also confirms the email address).

### Active members (`Business/Members/ActiveMembership.cs`)

- **The one helper that decides it**: a member is active when enrolled in the active season (the season whose period contains today), a parent of a family with an enrolled child, or has a staff role (`ActiveMembership.StaffRoles`: Admin, Maintainer, Treasurer, SportsDirector, YouthSportsDirector, Trainer). `ClubhouseHost` is **not** a staff role: the host is a business partner of the club and only gets names and pictures in the clubhouse screens. Application administrators (support mode) count as active. Use `ActiveMembership.IsActive` / `IsEnrolledInActiveSeason` / `OwnAndChildMemberIds`, never write the rule again.
- Policy `AuthorizationPolicies.ActiveMember` (`ActiveMemberRequirement`, `ActiveMemberAuthorizationHandler`, accepts API keys like the other club policies; a key's creator is an admin and therefore active) guards everything that shows other members: `Leaderboards` (GET), own `Statistics`, `Wrapped` (GETs), `OneTimeBadges` (GET list with awardees), `Bookings/RecurringSeries/{id}`, and - for another member than oneself - `Members/{id}/profile`, `MemberBadges/Members/{id}/badges|trophy-case` (`IAuthorizationService.EnsureSelfOrActiveMember`).
- Members who are not active keep: their own profile and member record, the booking calendar **anonymized**, courts, club info, play modes, court blockings, news and the club calendar (without names of others: `CreatedByName` and other registrations are stripped in the controllers), the membership page (they must be able to buy a membership), notifications, court sales and the own family. `Families/AvailableFamilyMembers` returns an empty list to them (treasurers put their families together in the payments dialogs).
- **Anonymized calendar**: `GET Bookings` / `GET Bookings/{id}` pass `AnonymousViewerUserId` for members who are not active (`BookingsController.AnonymousViewer`). `BookingAnonymizer` empties `Players`, `Comment`, `RecurringBookingSeriesId` and replaces the play mode by `BookingAnonymizer.NeutralPlayMode` (id 0, empty name, gray) with `BookingResult.IsAnonymized = true` - except for bookings the caller or a child of their family plays in. `GET Players` returns only the caller and their family's children (`GetPlayers(OnlyMemberIds)`).
- `GetClubProfileResult.IsActiveMember` and `GetBookingOptionsResult.IsActiveMember` tell the FE what to hide (leaderboard, statistics, wrapped, subscription planner, trophy cases of others).
- `ActiveMemberSecurityTests` checks it over HTTP, including a sweep of every GET club endpoint for the name of an active member.

### Query Filter Note

- The tenant query filter IS applied to `Member` (the root type). `ClubMember` and `GuestMember` inherit it automatically (EF Core TPH inheritance). They are listed in the exclusion array of `AddQueryFilterToAllEntitiesAssignableFrom` only because EF Core does not allow applying filters directly to derived types — but the filter still works via the root `Member` entity.
- When `tenantService.GetTenantId()` is null (e.g., admin routes without `{clubId}`), the filter passes all records through.
- Existing `IgnoreQueryFilters()` on `Set<Member>()` in handlers like `GetBookings`, `GetLeaderboard`, `GetMemberBookingHistory`, etc. is used to show players from other clubs (ATP cross-club feature).

## Court Sales (bought court time)

### Overview

A member of the club who is **not enrolled in the active season** (joined with "Join another club", staff that is not enrolled, former guests) buys free court time: clicks a free slot, pays with Stripe (or a booking credit, or a free price rule) and the court is booked. Join first, then buy (no anonymous purchases). Enrolled members get `AlreadyEnrolled` and book for free as before. Treasurers and admins set the prices, see the sales, cancel purchases and give credits.

### Domain (`Bookennis.Domain/CourtSales`)

- `CourtSaleSettings` (`TenantDomainEntity`, unique per club, no row = switched off): `IsEnabled`, `PlayModeId?` (play mode of bought bookings, required to switch on, FK set null when the play mode is deleted), `TimeZoneId` (default Europe/Vienna; the prices are local times of it - **never the client's time zone**), `MaxBookingMinutes` (30-240, multiple of 30, default 120), `MaxDaysInAdvance` (0-90, default 14), `MaxUpcomingBookings` (1-10, default 1). The play mode may not overbook (`CourtSalePlayModeCanOverbook`) or have a fixed duration (`CourtSalePlayModeHasFixedDuration`).
- `CourtPriceRule` (`TenantDomainEntity`): `CourtIds` (empty = all courts), `Weekdays`, `From`/`To` (local `TimeOnly` on full/half hours, `To` 00:00 = midnight), `PricePerHourCents` (≥ 0, 0 = free), `Currency` ("eur"), `Name?`. Rules of a club never overlap (shared weekday + shared court + intersecting times → `CourtPriceRulesOverlap` with name and times of the other rule). The DTOs carry the times as `TimeSpan` (24:00 = midnight) because the client converts `TimeOnly` to UTC.
- `CourtPricing.Quote(rules, courtId, interval, timeZone)` walks the slot minute by minute in local time (spans two rules, daylight saving days are priced by the time really played); a minute without a rule = not for sale (`null`). The amount is rounded to cents once at the end. `QuoteFirstMinutes(..., 60)` is what a credit covers. `CourtPricingTests` covers it.
- `CourtPurchase` (`TenantDomainEntity`): `MemberId?` (buyer, set null when the member is removed), `BookingId?` (set null), `CourtId`, `Interval`, `PriceCents`, `CreditUsed`, `AmountDueCents`, `Currency`, `Status`, `PaymentMethod?` (`Online`, `Credit`, `Free`), Stripe session/payment intent, `HoldExpiresAt?`, `PaidAt?`, `CancelledAt?`, `CancelledByMemberId?`. Status `AwaitingPayment` → `Processing` (SEPA) → `Paid`; `Expired` (hold released), `Failed`, `Cancelled`, `Refunded`, `Disputed`, `Compensated` (the club needed the court, `CompensationCredits?`). Transitions are methods (`CourtPurchaseInvalidStatus`), `MarkPaid/MarkProcessing/MarkFailed/MarkRefunded/MarkDisputed/Expire` are no-ops when repeated. `MarkPaid`/`MarkProcessing` also accept an `Expired` purchase (the money arrived late).
- `BookingCreditGrant` (`TenantDomainEntity`): `MemberId` (cascade), `Count` (≠ 0, negative to take back), `Reason` (`GuestCardMigration`, `GrantedByClub`), `Note?`, `GrantedByMemberId?`. **Balance** = Σ grants − purchases with `CreditUsed` in `CourtPurchase.CreditHoldingStatuses` (AwaitingPayment, Processing, Paid): a credit comes back by itself when its purchase expires, fails or is cancelled. A negative grant can't take the balance below 0 (`BookingCreditBalanceTooLow`).

### Buying (`Business/CourtSales`, `StartCourtPurchase`)

- The booking (buyer = only player, play mode of the settings) is created **when the purchase starts**, so the slot is held. It runs through `BookingDomainService.BookCourt` with `Context.IsPurchase = true`: the buyer needs no enrollment and no active season, `FixedPlayerCount` is not enforced, and prime time with `PrimeTimeSettings.RestrictGuests` ("no bought slots in prime time", the property kept its name) throws `CannotBuyWithinPrimeTime`. Opening hours, inactive/blocked courts, overlaps and the grace period apply as always.
- The handler also checks: settings switched on, slot starts on a full/half hour and lasts a multiple of 30 minutes up to `MaxBookingMinutes`, at most `MaxDaysInAdvance` days ahead (club time zone), upcoming purchases < `MaxUpcomingBookings`, the slot has a price. Everything after `LockCourts` (advisory lock per club, one-key form so it never collides with `LockMembershipOrders`). **Every handler that books a court takes the same lock before its overlap check** (`BookingContextBuilder.Load`, so `BookCourt`, `BookCourts`, `EditBooking`, `EditRecurringBooking`; `BookRecurringCourt`; purchases; a slot booked again after a late payment), so nothing is sold twice.
- Paying: a credit is never spent on free time (price 0 or a free first hour); credit and ≤ 60 minutes → `Credit`, paid at once; credit and longer → the credit pays the first hour, the rest online; no credit and price 0 → `Free`; otherwise a Stripe Checkout Session (direct charge, `price_data`, product "Court 2 · Sat, 18 Oct 17:00–18:00", `expires_at` = now + 31 minutes = `HoldExpiresAt`). Success/cancel url: `/clubs/{id}/booking?purchase={id}&date=yyyy-MM-dd&checkout=success|cancelled`. Stripe is checked before anything is held (`PaymentsNotConfigured`, `PaymentAccountNotReady`).
- **No self-cancel after payment**: `DeleteBooking`/`EditBooking` reject bought bookings (`BookingIsBought`). A treasurer cancels the purchase (`POST CourtSales/purchases/{id}/cancel`): the booking is deleted if it has not started, a credit comes back; money paid online is refunded **in the Stripe dashboard** (`CancelCourtPurchaseResult.RefundInStripeDashboard`).
- Releasing holds: `checkout.session.expired` (only for the session the purchase waits for), the buyer coming back with `checkout=cancelled` (`POST CourtSales/purchases/{id}/release`, buyer only: closes the session, books it when Stripe says it was completed after all), and the recurring job `CourtPurchaseHoldReleaseJob` (every 5 minutes, holds 2 minutes past `HoldExpiresAt`, asks Stripe first; every hold in its own command `ReleaseExpiredCourtPurchaseHold`, so its own transaction).
- Bookings of bought slots that the club removes (court blocked or set inactive, overbooked): `DeleteBookingsNotificationHandler` sends `NotifyBoughtBookingsRemoved` with the reason of each removal (`BoughtBookingRemoval`). A hold is released (purchase expired, payment page closed). A **paid** purchase is `Compensated`: the buyer gets **one credit per started hour** (`CourtPurchase.CreditsForCompensation`, none for a free slot; the credit the purchase used comes back by itself and counts toward it, the rest is a `BookingRemovedByClub` grant, `GrantedCompensationCredits`) and the email `CourtPurchaseCompensated` instead of the booking deleted email; the treasurers get no email. A purchase whose SEPA debit has not settled stays `Processing` and the treasurers get `NeedsAttention(BookingRemoved)`. When the money of a compensated purchase is refunded or disputed later, the granted credits are taken back as far as the buyer still has them, otherwise the treasurers get `CompensationNotTakenBack`.
- `BookingResult.IsBought` marks bought bookings (false when anonymized): `BookingDetail` shows no edit/delete for them but who can cancel, the planner can't move them.

### Stripe kind discrimination

- `StripeCheckoutData.Kind` (`StripeCheckoutKind.MembershipOrder` default, `CourtPurchase`) and `ExpiresAt`. Membership sessions keep the plain order id as `client_reference_id`; court purchases use `court_purchase:{id}` and metadata `court_purchase_id` (`StripeCheckoutReference.For/TryParse`, `StripeCheckoutSession.Kind/ReferenceId/IsForCourtPurchase`).
- `ProcessStripeEvent.HandleCheckout` branches on the kind; refunds and disputes look up `CourtPurchases` by payment intent before `MembershipOrders`. The club is always the one of the Stripe account; a purchase of another club changes nothing.
- `ICourtPurchaseSettlement` is the one place that books a court payment (webhook, `SyncCourtPurchaseCheckout` on `GET CourtSales/purchases/{id}`, release, hold job). A payment for an `Expired` purchase (or a hold whose booking was removed) books the slot again when it is still free (under `LockCourts`), otherwise the purchase is paid without booking and the treasurers are told (`PaidWithoutSlot`); when the credit of an expired purchase was spent in the meantime it is settled without credit and reported (`CreditNoLongerAvailable`); a payment for a cancelled/failed/refunded purchase is reported (`UnexpectedPayment`). A failed debit marks `Failed`, deletes the upcoming booking and mails buyer and treasurers. A full refund marks `Refunded` and deletes the upcoming booking; a dispute (also of a cancelled purchase) marks `Disputed`.

### Emails

`CourtPurchaseConfirmed`, `CourtPurchaseFailed` (to the buyer) and `CourtPurchaseNeedsAttention` (to Treasurers and Admins, reason text `CourtPurchase_Reason_…` in `Notifications(.de).resx`) are club emails without a notification preference. Send them with `ICourtPurchaseNotifier` (a substitute in `TestFixture`) after saving. The "paid" line comes from `CourtPurchaseTexts.Paid`.

### API (`CourtSalesController`, `api/Clubs/{clubId}/CourtSales`)

- **Cookie only, never an API key** (policies `CourtSalesMember` = any member, `CourtSalesManager` = Treasurer/Admin, `OwnsCourtPurchase` = buyer or Treasurer/Admin, the buyer only for `release`). Not in `ControllersOpenToKeys`.
- `GET|PUT settings`, `POST|PUT|DELETE price-rules[/{ruleId}]`, `GET purchases?from&to&status`, `POST purchases/{id}/cancel`, `GET|POST credits`, `GET credits/members?search` (names only): `CourtSalesManager`. `GET price-rules`, `GET options`, `GET quote?courtId&from&to`, `POST purchases`: `CourtSalesMember`. `GET purchases/{id}` (runs the sync), `POST purchases/{id}/release`: `OwnsCourtPurchase`.
- The treasurers' lists carry names only, never email, birthday or Stripe ids. `ExportPersonalData` lists `CourtPurchases` and `BookingCredits`.
- `CourtSalesSecurityTests` attack it over HTTP (other buyer, other club, members editing prices or credits, enrolled member buying, releasing a hold of somebody else, webhook of a foreign account).

## Clubhouse Tabs (Wirt)

### Overview

The clubhouse host ("Wirt", role `MemberRole.ClubhouseHost` = 8, English "Clubhouse host") rents the clubhouse and sells food and drinks. Instead of a paper list, the host books what a guest takes onto the guest's tab; the guest pays at the end. **The money goes to the host, not to the club**: Clubnetz keeps the list and records how it was paid (cash, the host's own card terminal, other), and members can pay their tab in the app to the host's own SumUp account (see Paying in the app).

### Domain (`Bookennis.Domain/Clubhouse`)

- `ClubhouseItem` (`TenantDomainEntity`): `Name` (≤ 60), `Category` (`Drink`, `Food`, `Other`), `PriceCents` (0-100000, 0 = free), `Currency` ("eur"), `IsAvailable` (hidden from the host's buttons when `false`, kept), `SortOrder`. Menu order: category, sort order, name (`InMenuOrder`).
- `ClubhouseTab` (`TenantDomainEntity`, aggregate root): for a member (`MemberId`) **or** a guest from outside (`GuestName` ≤ 80, e.g. "Table 3"), never both (`ClubhouseTabGuestInvalid`). `Status` `Open` → `Paid` (`MarkPaid(method, paidTo, now)`, not for an empty tab, never `Online`), or `Open` → `AwaitingPayment` (`StartOnlinePayment`, the member pays in the app: the tab is **locked**, every change throws `ClubhouseTabPaymentRunning`) → `Paid` (`MarkPaidOnline`) or back to `Open` (`EndOnlinePayment`). `OpenedAt`, `OpenedByMemberId?`, `PaidAt?`, `PaymentMethod?` (`Cash`, `Card`, `Other`, `Online`), `PaidToMemberId?`. **A member has one unpaid tab per club** (unique filtered index on `ClubId, MemberId` where `Open` or `AwaitingPayment`): opening again returns it. Only an open, empty tab can be discarded (deleted).
- **Every change of a tab takes `LockClubhouseTab` first** (`GetLockedClubhouseTab`, advisory lock per tab until the transaction ends): booking a round, removing a line, paying, discarding, starting an online payment and settling one. So the amount of a payment is always the amount of the tab.
- `ClubhouseTabLine`: a snapshot of the item (`Name`, `Category`, `UnitPriceCents`) plus `Quantity` (1-50), `AddedAt`, `AddedByMemberId?`, `ItemId?` (set null when the item is deleted). **Changing or deleting an item never changes a tab.** At most 300 lines per tab; a paid tab can't be changed (`ClubhouseTabNotOpen`).
- The tab stays as the host's record when the member leaves (FK set null, the name then shows as "former member"). `ExportPersonalData` lists the own tabs with their lines (`ClubhouseTabs`).

### API (`ClubhouseController`, `api/Clubs/{clubId}/Clubhouse`)

- **Cookie only, never an API key** (money), not in `ControllersOpenToKeys`. Policies: `ClubhouseHost` (ClubhouseHost or Admin) and `ClubhouseGuest` (any member).
- Host: `GET|POST items`, `PUT|DELETE items/{itemId}`, `GET board` (`GetClubhouseBoard`), `GET guests` (all members with name and picture, filtered on the device), `POST tabs` (open / get the open one), `GET tabs?from&to` (paid tabs by the local day they were paid, sums per method), `GET tabs/{tabId}`, `POST tabs/{tabId}/items` (one round: several items at once, the same item is merged into one line), `DELETE tabs/{tabId}/lines/{lineId}`, `POST tabs/{tabId}/pay`, `DELETE tabs/{tabId}` (discard an empty tab).
- Member: `GET my-tabs` - the unpaid and the last 10 paid tabs of the caller and of the children of the families they are a parent of (`ActiveMembership.OwnAndChildMemberIds`), and `CanPayOnline`.
- Paying in the app (see below): `GET|DELETE sumup`, `POST sumup/connect` (`ClubhouseHost`; connecting and disconnecting also need a member with ClubhouseHost or Admin, so not in support mode), `POST tabs/{tabId}/online-payment` and `GET tabs/{tabId}/online-payment/{paymentId}` (`ClubhouseGuest` + resource policy `PaysClubhouseTab` = the member of the tab or a parent of their family; not the host, not an application administrator). `SumUpController` (`api/SumUp`, no club route): `GET Callback` (OAuth, `AccountOwner`, cookie only) and `POST Webhook` (anonymous).
- **The board is built for speed**: open tabs (last booked first), then the members who play today (bookings of the local day, on court now first, members of other clubs left out), then the regulars (most tabs in the last 60 days); every member is listed once. Days are local days of `ClubEventInfo.TimeZone` (`ClubhouseQueries.LocalDate/LocalMidnight`). "Playing today" shows that a member plays today, nothing else (no times, no courts).
- `ClubhouseSecurityTests` attack it over HTTP (keys, members, treasurers, the guest of a tab, a host of another club with ids of club A).

### FE

- `Pages/ClubShell/Clubhouse`: `Clubhouse.razor` (`/clubs/{id}/clubhouse`, `?view=menu|history`): the start screen with search (every word matches the start of the first or last name, `ClubhouseHelpers.Matches`), "Guest from outside" (`WalkInDialog`), open tabs, playing today and regulars; the menu (`ClubhouseMenu`, `ClubhouseItemDialog`) and the paid tabs (`ClubhouseHistory`). `ClubhouseTabPage.razor` (`/clubs/{id}/clubhouse/tabs/{tabId}`): big item buttons (a tap = one more, "−" one less), a bar at the bottom books the round and goes back to the start screen; "Pay" at the top opens `PayTabDialog` (one tap per method). Texts in `ClubhouseLocale(.de).resx`, store `IClubhouseStore`.
- My Club: `ClubhouseCard` (only while a tab is open or one was paid in the last 14 days).
- Nav: section "Clubhouse" for ClubhouseHost and Admin (also in support mode).

### Paying in the app (SumUp)

- **The installation is a SumUp OAuth app, the host is the merchant.** `AppSettings.SumUp` (`SumUp__ClientId`, `SumUp__ClientSecret`, scopes `payments user.profile_readonly`; `payments` must be approved by SumUp for the app). Without it `ISumUpGateway.IsConfigured` is `false` and the clubhouse works as before. The redirect URL registered at SumUp is `{AppUrl}/api/SumUp/Callback`. Never commit the secret.
- **Nothing but `SumUpGateway` (`ISumUpGateway`) talks to SumUp** (a substitute in `TestFixture`). Hosted Checkout: `POST /v0.1/checkouts` with `hosted_checkout.enabled`, amounts in major units, `valid_until` = 30 minutes, `return_url` = the webhook, `redirect_url` = My Club with `?clubhouseTab=&clubhousePayment=`. The member leaves for `hosted_checkout_url` with a full page load (CSP unchanged).
- `ClubhouseSumUpAccount` (`TenantDomainEntity`, unique per club): `MerchantCode`, `MerchantName?`, `AccessToken`/`RefreshToken` **encrypted** with ASP.NET data protection (`SumUpTokenProtector`, the key ring volume of the container), `AccessTokenExpiresAt`, `ConnectedByMemberId?`, `NeedsReconnect`. The tokens never leave the server. Connecting: `StartClubhouseSumUpConnect` (state = `SumUpOAuthState`, time-limited and bound to the user) → SumUp → `CompleteClubhouseSumUpConnect` (exchanges the code, reads `/v0.1/me`, redirects to `?view=sumup&sumup=connected|failed`). Disconnecting is refused while a payment is pending (`ClubhouseSumUpPaymentsRunning`).
- **Tokens**: `ISumUpAccessTokens.Get` refreshes an access token that expires within 2 minutes. SumUp may rotate the refresh token, so `SumUpTokenRefresher` refreshes **in its own transaction and connection under an advisory lock per account** (a rolled back request never loses a new refresh token, two requests never refresh at once). A refused refresh or a token that can't be decrypted marks `NeedsReconnect` the same way. Tests use `InlineSumUpTokenRefresher` (same idea on the test database).
- `ClubhouseTabPayment` (`TenantDomainEntity`): one attempt per payment page: `TabId`, `PayerMemberId?`, `AmountCents`/`Currency` (the locked tab), `MerchantCode`, `Reference` (Guid; `CheckoutReference` = `clubhouse_tab:{tabId}:{guid}`), `CheckoutId`, `CheckoutUrl`, `Status` (`Pending`, `Paid`, `Expired`), `ExpiresAt`, `PaidAt?`, `TransactionCode?`. One pending payment per tab (unique filtered index). Starting again while the page is open returns the same page.
- **What the browser says proves nothing; only what SumUp answers counts.** `IClubhouseTabPaymentSettlement.Check` is the one place that books a payment: it asks SumUp with the host's account and only accepts a checkout with the payment's id, reference, merchant, amount and currency. `PAID` → paid (the tab is paid `Online` when it is locked for it, or still open with the same amount; otherwise the host gets `NeedsAttention` `PaidTwice`/`TabChanged`); `EXPIRED` → expired, tab unlocked; `FAILED` is only the last attempt (the member may retry on the same page). Three ways lead there: the member coming back (`GetClubhouseTabPayment`, polled a few times by `ClubhouseCard`), the webhook (`ReceiveSumUpWebhook`: unsigned, only `{event_type, id}`, an unknown id is ignored, a known one is checked by the `[OutOfBand]` `CheckClubhouseTabPayment`) and the recurring `ClubhouseTabPaymentJob` (every 5 minutes, pages that ran out 2 minutes ago: asks SumUp, closes an unpaid checkout at SumUp, unlocks the tab; when SumUp can't be asked any more it unlocks and tells the host `CouldNotVerify`).
- Refunds are made in the SumUp dashboard; Clubnetz does not see them (SumUp sends no event for them). Card payments on the host's own SumUp reader are still marked by hand as `Card`.
- Emails `ClubhouseTabPaid` (to the payer) and `ClubhouseTabNeedsAttention` (to the hosts, or the admins when the club has no host) are club emails without a preference, sent by `IClubhouseTabPaymentNotifier` (a substitute in `TestFixture`) after saving.
- FE: `ClubhouseSumUp.razor` (`?view=sumup`, connect/reconnect/disconnect), the lock banner on `ClubhouseTabPage`, the "Paying in the app" chip on the board, "Pay" on `ClubhouseCard`. `ClubhouseHelpers.PaymentMethods` (what the host may pick) leaves out `Online`.
- `ClubhouseSecurityTests` attacks it over HTTP: keys, other members, the host, another club, forged and foreign webhooks, a forged OAuth state.

### Not built yet

Receipts, stock, happy hours, a tab for a whole team, sending the amount to the host's SumUp reader (Cloud API), guests from outside paying in the app, reflecting refunds made in SumUp.

## Member Data & Privacy

- **What a member gets to know about somebody else is name and picture, nothing more.** `GET Members/{memberId}/profile` (`GetMemberProfile`, `MemberProfileResult`, policy `Member`) returns exactly that, for members of the club and for players of a club it shares its players with (both `IsAtpClub`). Never add email, birthday, gender, roles or seasons to it.
- The full record `GET Members/{memberId}` (`GetMember`, `MembersDetailResult`: email, birthday, roles, seasons, ...) is only for the member themself and for `ClubMemberViewer` (Maintainer, Admin); the controller checks that and answers everybody else with 403. It only returns members of the own club, also for viewers: a partner club never gets the full record.
- In the FE, show another player with `IMembersStore.GetMemberProfile` (`TrophyCaseView` does). `IMembersStore.LoadMember` / `MemberDetail` is for the signed-in member and the member administration only; calling it for somebody else as a normal member ends on the access-denied page.
- New endpoints that return members to normal members follow the same rule; `MemberPrivacySecurityTests` checks it over HTTP.

### Families

- **A family belongs to its members.** `FamiliesController` authorizes every action on a family with the resource `FamilyAuthorizationModel(FamilyId)`: `AuthorizationPolicies.FamilyMember` to read it (parents and children), `AuthorizationPolicies.FamilyParent` to change or delete it (parents only; a member may also take themself out). Admins and treasurers of the club pass both (`FamilyAuthorizationHandler`): a treasurer puts the family together when the fee of a couple or family is recorded. Never add a family endpoint without one of them.
- Why it matters: a family holds the birthdays of children, and whoever is a parent of a family may book for its children, so joining a family is a way to gain rights.
- **Who may book for players** (`BookingContextBuilder`, all booking create/edit handlers): at least one player must be the user themself or a child of a family the user is a parent of, otherwise `UserNotAllowedToBook`. Being in some other family gives no rights; `BookingFamilyRightsTests` and `FamilySecurityTests.Parent_CanOnlyBookForTheOwnChildren` check it.
- `POST Families` (`CreateFamily`) is only allowed when the caller is one of the parents of the new family (admins and treasurers may found any). `GET Families` (all families of the club) is for `ClubMemberViewer`; the app itself only loads the own family by id.
- `GetAvailableFamilyMembers` stays open to every club member: it only returns names of members without a family, to pick a partner or children from. A member can add any of them to the own family without their consent - that is how the feature works today.
- **The account of a new child belongs to a parent**, never to the admin or treasurer who typed it in: `FamilyCreateUpdateHelper.GetOwnerOfNewChildren` takes the caller when they are a parent of the family, otherwise the first parent. Whoever owns the account books for the child.
- FE: `Components/Family/Family.razor` is the one editor for a family: on *My club* (the own family), on the member page of the admins and inside `RecordPaymentDialog` and `ApproveOrderDialog` of the treasurers (`ManagedByClub="true"`, `Embedded` without the card; both use `CoveredMembers.razor`, the chips for who a couple or family plan covers with the family editor below). It only shows the add, edit and remove buttons to parents or with `ManagedByClub` (`CanManage`), because the API refuses everybody else and a 403 sends the user to the access-denied page. `OnChanged` tells the page to load what depends on the family. Members are picked with `MemberPickList` (search field + list, one or several), not with a `MudSelect`.
- `FamilySecurityTests` checks all of this over HTTP with two families in the same club.

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
- Only enrolled `ClubMember`s create recurring bookings.

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
- Recurring bookings are restricted to `ClubMember` participants enrolled in the active season.

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

## Booking Planner (desktop)

### Overview

Sports directors, admins and the other elevated booking roles plan bookings (e.g. tournaments) over several days on `Pages/ClubShell/Planner/Planner.razor` (`/clubs/{id}/planner?Start=&Days=`, link in "Courts & play" of the `NavMenu` for Admin, Maintainer, SportsDirector, YouthSportsDirector, Trainer). Desktop only: below 960px the page shows a hint to the booking page. Courts are rows, the opening hours of 3/7/14 days run horizontally in 30-minute slots.

- **Drag** a booking to move it (time, day, court), **drag its edges** to resize it (not for play modes with `FixedDuration`), **drag over free time** to select a region (courts × time within one day), **Ctrl/Cmd + drag** adds regions; "Book selection" opens `BookSelectionDialog`, which creates one booking per court per region.
- The pointer handling lives in `wwwroot/planner-interop.js` (instance id per timeline, `[JSInvokable]` callbacks on `PlannerTimeline`). It pre-checks drops (past, blockings, closed courts, opening hours, overlaps for play modes that can't overbook) only to give feedback - **the server decides**.
- Opening hours and prime time are UTC `TimeOnly`s that the client converter shifts with today's offset; `PlannerHelpers.DailyWindow` rebuilds them per day so days after a DST change are right.

### Rules (top priority: the play mode rules always hold)

- Moves and resizes use `EditBooking`, or `EditRecurringBooking` with `RecurringEditScope.Single` for occurrences of a series. Both build the domain context with `BookingContextBuilder` (with the edited booking left out of intersections and counts) and `BookingEditRules.EnsureMayEdit` - exactly the rules of a new booking.
- `POST Bookings/Batch` (`BookCourts`, policy `CanBookPlayMode`) books up to 100 slots with one play mode. Every slot runs through `BookingDomainService.BookCourt` with a context from `BookingContextBuilder` (shared with `BookCourt`); earlier slots of the batch count toward the quotas of later ones, and slots may not overlap each other on a court (`BookingSlotsOverlap`, also for overbooking modes). `DryRun` returns per-slot errors and the bookings an overbooking mode would remove without saving; a real run is all-or-nothing.
- **Overbooking only removes what the user confirmed**: the dialog sends the ids of the dry run's `BookingsToRemove` as `ExpectedBookingIdsToRemove`, and the server rejects the save with `BookingsToRemoveChanged` when they differ. A move with an overbooking mode reloads the bookings and asks with the fresh list before saving.
- Never send a comment for a play mode with `CommentAllowed = false` (the domain throws).
- `BookCourtsTests`, `EditRecurringBookingTests` and `BookingPlannerSecurityTests` cover it.

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

- `CourtBlockingsController` (`api/Clubs/{clubId}/CourtBlockings`): `GET ?dayFrom=&dayTo=` (occurrences for the booking grid, `AuthorizationPolicies.Member`, also for members who are not active), `GET manage?includePast=` (blockings as entered, upcoming first), `POST conflicts`, `POST`, `PUT {id}`, `DELETE {id}`.
- Managing requires `AuthorizationPolicies.CourtBlockingManager` (`Maintainer`, `SportsDirector`, `Admin` - not `YouthSportsDirector`). The FE mirrors this in `CourtBlockingHelpers.CanManage`.
- **The times in `SaveCourtBlockingModel`/`CourtBlockingDto` are `TimeSpan`s, not `TimeOnly`**: the client's `TimeOnlyJsonConverter` converts `TimeOnly` to UTC time of day, which is wrong for local times that the server combines with a date and a time zone.

### FE

- `Pages/ClubShell/Club/CourtBlockings.razor` (`/clubs/{id}/court-blockings`, list with status, edit, delete, past blockings toggle) and `Dialogs/EditCourtBlockingDialog.razor`: reason with templates, courts, period, all day, repetition. Saving first loads the conflicts; if there are any, they are listed and the submit button turns into "Delete n bookings and block" (second click confirms). Changing an input asks the server again.
- `Components/Booking/Day.razor` renders the occurrences (`Blockings` parameter, `.day__blocking`, violet stripes with title and time). `Booking.razor` loads them with the bookings (`ICourtBlockingsStore.LoadOccurrences`), shows chips, a legend entry and a dot in the day strip, and a lock button to the management page for managers.
- `NavMenu.razor` shows the link (section "Courts & play") to admins, maintainers and sports directors. Texts are in `CourtBlockingsLocale(.de).resx`.

## Season Wrapped (Saisonrückblick)

### Overview

At the end of a season every active club member gets a story-style review of their season ("Wrapped"): matches, hours, place among the enrolled members, favourite partners, play style, home court, records, regular slot, badges, events and the numbers of the whole club. A season is the club's existing `Season` (`Period`), nothing new to define.

- **The club admins release it** (`Season.WrappedPublishedAt`, `PublishWrapped`/`UnpublishWrapped`): any season that has started, also past ones and the running one (`ErrorCode.WrappedSeasonNotStarted` for upcoming seasons). Repeating keeps the first date. Before that only `ClubAdministrator`s can open it, as a preview.
- **Nothing is stored**: `GetSeasonWrapped` calculates it from the bookings whenever it is opened, so every past season can be generated and the numbers always match the statistics. Same counting rule as statistics and badges (`WrappedBookings.Load`: play modes with `IsChargingBookingSubscription`, local date of the booking within the season). Reuses `GetMemberStatistics.Handler.GetPlayerType`/`GetLongestWeekStreak`.
- Rank: place by matches among the members enrolled in the season (ties share a place), only for a member who played. Other members only appear as top partners with name and picture (see Member Data & Privacy); club numbers are anonymous totals.

### API

- `WrappedController` (`api/Clubs/{clubId}/Wrapped`): `GET` (seasons the caller can open + `CanManage`; members get released seasons only), `GET {seasonId}` (`GetSeasonWrapped`, `ClubMember`; `ErrorCode.WrappedNotPublished` for members on an unreleased season), `PUT|DELETE {seasonId}/publish` (`ClubAdministrator`). `CanManage` comes from the controller (`IAuthorizationService`, like the announcements).
- Support mode has no member: the Wrapped then only shows the club.
- `WrappedSecurityTests` checks who may release and open it over HTTP.

### FE

- `Pages/ClubShell/Wrapped/WrappedSeasons.razor` (`/clubs/{id}/wrapped`, nav "Personal" → Season Wrapped): one card per season, admins get release/take back and preview. `SeasonWrapped.razor` (`/clubs/{id}/wrapped/{seasonId}`): full-screen story player (fixed overlay above the app bar, phone-sized card on desktop) with progress segments, tap left/right, hold or space to pause, arrow keys, Escape, and a summary card with share (`App.shareOrCopy`). Slides without data are left out (`BuildSlides`).
- Auto-advance is driven by the CSS animation of the active progress segment (`@onanimationend`); pausing only pauses that animation, never the entrance animations of the slide content (they would stay invisible).
- `WrappedTeaser` on My Club advertises a season released within the last `WrappedHelpers.TeaserDays` (60) days. `SeasonsCard` links to the page and marks released seasons with 🎁. Store: `IWrappedStore`, texts in `WrappedLocale(.de).resx`. Times of day are sent as `TimeSpan` (the client converts `TimeOnly` to UTC).
- Not built yet: a notification when a season is released, Wrapped for the children of a family.

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

Club members plan shared winter court subscriptions: a date range, free-text participants (no club membership needed) with a percentage share, weeks nobody plays, per-participant unavailable weeks and a fixed number of players per week. The planner computes a fair weekly schedule that can be edited manually and exported to Excel.

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

- `ClubEmailType` (Domain + a copy in Shared): `Welcome`, `SeasonActivated`, `BadgeAwarded`, (3 was `GuestCard`, removed - the number is not used again), `BookingDeleted`, `Announcement` (see Club Announcements), `BookingAdded`, `BookingReminder`, `ClubEventCreated`, `ClubEventReminder`, `ClubEventRegistrationDeadline` (see Notifications), `MembershipPaymentReceived`, `MembershipApproved`, `MembershipPaymentFailed`, `MembershipNeedsAttention` (see Memberships & Payments), `CourtPurchaseConfirmed`, `CourtPurchaseFailed`, `CourtPurchaseNeedsAttention` (see Court Sales), `ClubhouseTabPaid`, `ClubhouseTabNeedsAttention` (see Clubhouse Tabs).
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

Clubs plan events (work efforts, parties, tournaments, meetings) in a club calendar. Club members see upcoming events, open the details and optionally register with a head count (including themselves) and answers to the organizer's questions. Members who are not active see the events without the names of other members.

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

- `Pages/ClubShell/Calendar`: `Calendar.razor` (`/clubs/{id}/calendar`, hero with the next event, "Upcoming" list grouped by month with a past-events toggle, and a CSS-grid `MonthCalendar` that shows dots on phones), `ClubEventDetail.razor` (`/clubs/{id}/calendar/{eventId}`, share target), `ClubEventEditor.razor` (`/new` and `/{eventId}/edit`, Markdown description with an Edit/Preview switch and formatting help, question builder with templates). `UpcomingEvents.razor` shows the next 3 events on My Club.
- `ClubEventsStore.UpcomingEvents` is isolated from `Events` (calendar range) so the calendar can't overwrite the My Club card.
- Sharing: `App.shareOrCopy` (native share sheet, clipboard fallback) and `App.copyToClipboard` in `js-helpers.js`, plus a `https://wa.me/?text=` link with title, date, location and the absolute event url (`ClubEventHelpers.ShareText`).

### Deep links / login return url

- Shared links require a login. `RedirectToLogin` and the 401 handler call `NavigationManager.NavigateToLoginAndReturn()`, which passes the current page as `returnUrl`. `LogIn.razor` only follows local return urls (`NavigationManagerExtensions.IsLocalReturnUrl`, prevents open redirects).
- `ClubWithStoreLayout` stops when the profile could not be loaded (not signed in) instead of redirecting to access denied, and `App.razor` keeps the club of a `/clubs/{id}/...` deep link after login instead of switching to the favorite club.

## Club Announcements (News)

### Overview

The club posts news for its members: a title and a Markdown text, optionally pinned, with an expiry date and attached files. Members see the news on My Club (latest 3) and in the news list. An announcement can additionally be sent as email (with its attachments) to a group of members. Members who are not active see them without the author.

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
- Credential changes require the `AuthorizationPolicies.AccountOwner` policy (a signed-in user with a cookie, never an API key). Guest links were removed; the old guest link (`/account/guestLogin`) shows an info page (`LoginGuest.razor`) that explains how to sign in instead.
- The router re-creates the current page when the auth state is (re)notified, e.g. on app start. Pages that consume single-use tokens (`ConfirmEmail`, `ConfirmEmailChange`) share one request per token and the handlers are idempotent; never call `IAuthStateChanged.AuthStateChanged()` from `OnInitializedAsync` of such a page (it loops), reload with `forceLoad` instead. `ConfirmEmailChange` uses `NoNavigationLayout` because `NotAuthorizedLayout` redirects signed-in users.

## Login Protection, Rate Limiting & Health

- Failed password checks lock the account (`IdentityAndAuthenticationConfiguration`: 5 attempts, 15 minutes). Always call `CheckPasswordSignInAsync(..., lockoutOnFailure: true)` when checking a password (login, re-authentication for email change / account deletion). A successful `ResetPassword` lifts the lockout.
- Don't reveal whether an email is registered: `LoginUser` answers unknown email and wrong password with `InvalidCredentials` (and only reports `NotAllowed`/unconfirmed email when the password was right), `ConfirmEmail`/`ResetPassword` treat an unknown email like an invalid token, `ForgotPassword` always succeeds.
- `RateLimitingConfiguration` adds per-IP fixed-window policies: `AuthenticationPolicy` (10/min: login, confirm email, reset password, change password, delete account) and `EmailPolicy` (5/15 min: register, forgot password, change email). Apply them with `[EnableRateLimiting(...)]` on anonymous or credential-checking endpoints. Rejections are 429 with an `ErrorCodeResponse` (`TooManyRequests`), which `SnackbarExtensions.ShowErrorDetails` localizes. The client IP comes from `X-Forwarded-For` (`UseForwardedHeaders`, rightmost entry only).
- The Swagger UI and the OpenAPI document with all endpoints are only exposed in Development; the document of the club API is public (see Club API Keys & OpenAPI). `/health` is an anonymous health check (includes the database).

## Club API Keys & OpenAPI

### Overview

Club admins connect their own scripts and tools to their club with an API key (`Authorization: Bearer cnz_...`). There is no separate public API: a key is accepted by the club endpoints the app itself uses (`api/Clubs/{clubId}/...`).

- **A key acts as the user who created it** (`ClubApiKey.CreatedByUserId`), limited to its club. The request runs with that user's id, so handlers, the audit metadata and "don't notify the user who caused it" work unchanged. There is no user-less service account - don't build handlers that expect one.
- A key is rejected (401) when it is unknown, expired, used for another club or for a route without `{clubId}`, or **when its creator is no longer a `ClubMember` with `MemberRole.Admin` in the club** (`ClubApiKeyStatus.CreatorNotAdmin` in the list). An administrator in support mode has no member and therefore can't create a key (`ApiKeyCreatorNotClubAdmin`), only view and revoke.
- A read-only key (`IsReadOnly`) may only send GET/HEAD requests; `ClubApiKeyReadOnlyFilter` (global MVC filter) answers everything else with 403 `ApiKeyReadOnly`, also POST endpoints that only read (previews, conflicts).

### Domain / handlers

- `ClubApiKey` (`Bookennis.Domain/Clubs/ApiKeys`, `TenantDomainEntity`): `Name`, `KeyHash` (SHA-256 hex of the key, unique), `KeyPrefix` (first characters, for the list), `IsReadOnly`, `CreatedByUserId` (cascade), `ExpiresAt?`, `LastUsedAt?`. `ClubApiKey.Create` returns the entity and the key; **the key is never stored and only returned by `CreateClubApiKey`**. Revoking deletes the row.
- `Business/ClubApiKeys`: `GetClubApiKeys`, `CreateClubApiKey` (max `ClubApiKey.MaxKeysPerClub` = 20), `DeleteClubApiKey` and `AuthenticateClubApiKey` (used by the authentication handler; writes `LastUsedAt` at most every 5 minutes; ignores the tenant filter on purpose, see `ClubApiKeyQueryExtensions.ClubAdmins`).

### Authentication / authorization

- `ClubApiKeyAuthenticationHandler` (scheme `CustomAuthenticationSchemes.ClubApiKey`, not a default scheme) builds a principal with the creator's `sub`, the role `User` (**never `Administrator`**, even if the creator is one) and the `ClubApiKeyClaims`.
- **Closed by default**: a key only reaches endpoints whose policy lists the scheme (`CustomAuthenticationSchemes.CookieOrClubApiKey`). These are the club policies in `AuthorizationConfiguration`; `ApplicationUser`, `AccountOwner`, `ApplicationAdministrator`, `OwnsSubscriptionPlan` and `ClubApiKeyManager` stay cookie-only, so profile, account, push, admin panel and the key management itself can't be called with a key. A new club endpoint is reachable with a key as soon as it uses a club policy; a club endpoint that only has `ApplicationUser` at the controller needs `AuthorizationPolicies.AnyMember` (see `MembersController.GetBookingHistory`).
- `ClubApiKeysController` (`api/Clubs/{clubId}/ClubApiKeys`, policy `ClubApiKeyManager` = signed-in Admin): list, create, delete.
- Requests with a key are limited to `RateLimitingConfiguration.ClubApiKeyRequestsPerMinute` (120) per key by the global limiter; signed-in users are not limited there.

### OpenAPI

- `Infrastructure/Swagger/ClubApiDocument`: the document `club` contains exactly the endpoints whose policies accept the key scheme (derived from the endpoint metadata, nothing to maintain by hand) and is served in every environment at `/api/openapi/club.json`. The document with all endpoints (`/api/openapi/v1.json`) and the Swagger UI only exist in Development.
- The description in `ClubApiDocument.Info` is the documentation integrators read; update it when the rules above change.
- `OperationNameFilter` gives every operation an id and a summary derived from the action name ("Get club announcements") unless the action has an XML `<summary>`; the XML comments of controllers, actions and models are included in the documents. A comment on a controller, action or shared model is therefore public documentation.
- The club document is sent with `Access-Control-Allow-Origin: *` (`Program.cs`) so the landing page can load it. This is the only CORS exception; the API itself is not callable from other websites.

### Security tests (`Bookennis.Api.Tests/Security`)

- `ApiSecurityFixture` starts **the real app** (`Program.cs`, Production environment, `WebApplicationFactory`) against its own copy of the test database and seeds two clubs. `ClubApiKeySecurityTests` attack it over HTTP. They run with `dotnet test`, so also in CI.
- **The sweeps read every endpoint from the running app** (`ApiEndpoint.All`), nothing is listed by hand: a new endpoint is attacked automatically. They check that a key is only accepted on the routes of its club, that every kind of rejected key (unknown, expired, revoked, other club, creator no longer admin) gets 401 everywhere, that a read-only key gets 403 on every write, and that a key of club A never reads or changes anything of club B.
- Leaks are detected with a canary: every text of club B contains `ApiSecurityFixture.Canary`, which must never appear in an answer to club A. Changes are detected with `Fingerprint` (a hash over all rows with the club's `ClubId`). A probe middleware reports who a request really ran as (`ApiResponse.RanAsKey`), so the tests don't have to guess from status codes.
- `ControllersOpenToKeys` in the test is the reviewed list of controllers a key can reach. **A new club controller fails the test until it is added there on purpose**; `ClubApiKeys`, `PaymentAccount`, `CourtSales` and `Clubhouse` must never be in it.
- When you add a tenant entity that a club endpoint returns or changes by id, seed one in `ApiSecurityFixture.AddClub` (with the canary text) and add its route parameter to `ApiEndpoint.Value`, otherwise the sweeps call that endpoint with an id that does not exist.
- **`IgnoreQueryFilters()` switches the tenant filter off for the whole query, not only for the set it is called on.** A query that uses it for players of other clubs must filter its root entity by the club explicitly (`booking.ClubId == currentClub.Id`). The sweeps found exactly this in `GetBookings`, `GetBooking`, `GetUpcomingBookings`, `GetMemberBookingHistory` and `GetMember`.
- Handler tests (`TestBase`) can't test tenant isolation: the tenant set with `SetTenantId` does not reach the handler's `AppDbContext`, so the tenant filter is off there. Test isolation between clubs in the security tests.

### Documentation on the landing page

- `website/docs/api/index.html` and `website/de/docs/api/index.html` (static, like the rest of `website/`): guide, tested examples, errors, limits and the endpoint reference. The landing pages link to them (nav, card in "For the board", section `#api`, FAQ, footer) and the API keys page of the app links to the guide.
- The reference is not written by hand: `website/docs/api/reference.js` (plain JavaScript, no dependencies, inserts everything as text) loads `https://my.clubnetz.app/api/openapi/club.json` and renders it. For a local preview serve `website/` on localhost and append `?openapi=http://localhost:<port>/api/openapi/club.json` (only honoured on localhost).
- **Keep both languages and the app in sync**: when the rules of the keys change (limits, status codes, what a key can reach), update both docs pages, `ClubApiDocument.Info` and the how-to card in `ApiKeys.razor`.

### FE

- `Pages/ClubShell/Club/ApiKeys.razor` (`/clubs/{id}/api-keys`, admins only, link in the "Club setup" section of the `NavMenu`) and `Dialogs/CreateApiKeyDialog.razor` (name, validity, read-only or read and write). The new key is shown once in the page and only kept in the component. The page also explains the usage (curl example, link to the OpenAPI document and to the guide on the landing page). Texts are in `ClubApiKeysLocale(.de).resx`, store: `IClubApiKeysStore`.

## Navigation & Club Switcher

- `Shared/NavMenu.razor` builds its links in `Sections` (one place for all role rules): the club section without a title (My Club, Book, News, Calendar, Leaderboard), "Personal" (membership, own statistics, subscription planner), then the management sections "Members & fees", "Courts & play" and "Club setup", and "Account" at the bottom. A link is only listed in the section of its topic, whatever role sees it; empty sections are hidden. Support mode shows the management sections as for an admin. Section texts are `Section_*` in `NavigationLocale(.de).resx`.
- Who sees what must match the backend policies: Clubhouse (ClubhouseHost, Admin), Members (Admin, Maintainer), Court sales / Membership plans / Membership fees (Admin, Treasurer), Court blockings (Admin, Maintainer, SportsDirector), Badges (Admin, SportsDirector, YouthSportsDirector), Club statistics (Admin, Maintainer), Club setup (Admin).
- Members who are not active (`IClubProfileStore.IsActiveMember`, see Members, Former Guests & Active Members) don't get Leaderboard, own statistics, Season Wrapped and the subscription planner.
- Pages have no title heading in the content (no `PageHeader`): the nav shows where the user is, the browser tab gets the `<PageTitle>`.
- `Components/Shared/ClubSwitcher.razor` (top of the drawer) shows the open club with the own roles and lists the clubs of the user (`IProfileStore.Clubs`, from `GetUserProfileResult.Clubs` with name and roles). Switching is `NavigationManager.SwitchToClub` (full page load, the stores still hold the data of the other club). "Join another club" opens `SelectClubDialog` (searchable cards, without the clubs the user is already a member of), which `App.razor` also shows to a user without a club. `ClubAvatar` draws the club initials on a color derived from the club id.

## Admin Panel & Support Mode

### Overview

The operator of the installation (ASP.NET role `Administrator`, policy `ApplicationAdministrator`) supports the club admins. **Club features are never rebuilt in the admin panel**: the administrator opens the club itself and works in the same pages as its club admin (support mode). The admin panel (`/admin`) only holds what belongs to the platform.

### Support mode

- The backend lets application administrators pass every club policy (`ClubAuthorizationHandler` and the booking/play mode handlers succeed for `IsAdministrator()`), also for clubs they are not a member of.
- `GetClubProfile` gets `SupportClubId` from the controller for administrators. Without a member in that club it returns `IsSupportMode = true`, `MemberId = 0` and the role `Admin`; with a member the administrator is treated as that member. The FE exposes it as `IClubProfileStore.IsSupportMode`.
- `ClubWithStoreLayout` and `App.razor` let administrators open `/clubs/{id}/...` of any club. `NavMenu` then shows what a club admin manages (no My Club, statistics, leaderboard, subscription planner), `SupportModeBanner` (in `AuthorizedLayout`) shows whose club is being changed and leads back to the admin panel, and My Club redirects to the club settings.
- **There is no member in support mode.** Handlers that are reachable for club admins must not require one: resolve it with `GetClubMemberId` (nullable, e.g. `CreatedByMemberId` stays `null`), never with `GetRequiredClubMemberId`. In the FE, guard everything that uses `ClubProfileStore.MemberId` (`MembersStore.LoadMember`, own bookings, the event registration form) with `IsSupportMode`.
- Entering and leaving support mode is a full page load (`NavigationManager.OpenClubAsAdministrator` / `LeaveSupportMode`), because the stores still hold the data of the club that was open before.

### Admin panel

- `Pages/Admin` (`AdminLayout`, `@attribute [Authorize(Roles = "Administrator")]`): `Dashboard.razor` (`/admin`, `GetAdminOverview`), `Clubs/Clubs.razor` (cards with the club admins as contacts, create a club), `Clubs/ClubDetail.razor` (open the club, quick links into it, club admins, add an existing user as member or first admin, name and ATP flag, delete), `Users/Users.razor` and `Users/UserDetail.razor` (clubs of the user, personal data, email, password reset, impersonation, delete).
- The styles shared by the admin pages (`admin-page`, `admin-row`, `admin-chip`, `admin-club-card`, `admin-fields`, ...) are in `AdminLayout.razor.css`; they reach the pages via `.admin-shell ::deep`.
- `Controllers/Admin/ClubsController` still has endpoints for play modes, courts, seasons, members and families of a club (`Admin*` handlers). Except for adding a member the FE no longer uses them, these things are edited in support mode - don't add new ones.

## Memberships & Payments

### Overview

A club enters its price list as membership plans per season. Members choose a plan for themselves or their family and pay the fee: online with Stripe (card, SEPA direct debit), or by bank transfer or cash, which the treasurer records. A paid order enrolls its members in the season. **Plans, the treasurer's list and "mark as paid" work without Stripe**; only the online checkout needs it.

**Enrolling by hand stays as it was.** `UpdateMemberBookingOptions` and `SeasonEnrollmentEditor` are untouched. An order is one more way to create a `MemberSeason`: removing an enrollment by hand never changes an order, and an order never removes an enrollment (not when it is cancelled, refunded, disputed or its debit fails - the treasurer decides).

### Domain Model (`Bookennis.Domain/Memberships`)

- `MembershipPlan` (`TenantDomainEntity`, aggregate root): `SeasonId` (cascade), `Name` (unique per club and season), `Description?` (plain text), `PriceCents` (0 = free plan, confirmed instead of paid), `Currency` ("eur"), and the rules: `MaxAdults` (1-2) + `IncludesChildren` (1/false = one member, 2/false = couple, otherwise a family: `IsGroupPlan`), `BirthYearFrom?`/`BirthYearTo?`, `RequiresApproval`, `NewMembersOnly`, `RequiresEnrolledFamilyAdult` (partner card, discount on children; only for plans for one member), `GrantsSeasonEnrollment` (`false` = passive member: paid, but not enrolled), `AvailableFrom?`/`AvailableUntil?`, `IsSelfService` (off = only the treasurer assigns it), `SortOrder`. **The price is per order, however many members it covers.**
- `MembershipOrder` (`TenantDomainEntity`, aggregate root): plan, season, a snapshot of `PlanName`/`AmountCents`/`Currency` (a plan that changes later never changes an order), `PayerMemberId?`, `Members` (`MembershipOrderMember`), `Status`, `PaymentMethod?` (`Online`, `BankTransfer`, `Cash`, `Other`; `null` for a confirmed free plan), `StripeCheckoutSessionId?`, `StripePaymentIntentId?`, `ApprovedAt?`, `PaidAt?`, `MarkedPaidByMemberId?`, `Note?` (treasurers only).
    - `MembershipOrderStatus` (Domain + a copy in Shared): `AwaitingApproval` → `AwaitingPayment` → `Processing` (SEPA debit started) → `Paid`; `Failed`, `Cancelled`, `Refunded`, `Disputed`. The transitions are methods of the order (`Approve`, `Reject`, `StartCheckout`, `MarkProcessing`, `MarkPaid`, `ConfirmFree`, `MarkFailed`, `CancelByMember`, `CancelByTreasurer`, `MarkRefunded`, `MarkDisputed`); an invalid one throws `MembershipOrderInvalidStatus`. **`MarkPaid`, `MarkProcessing`, `MarkFailed`, `MarkRefunded` and `MarkDisputed` are no-ops when repeated**, because Stripe reports things twice.
    - **A member has one membership per season**: one order whose status is in `MembershipOrder.ActiveStatuses` (everything except `Cancelled`, `Failed`, `Refunded`). The join table can't carry a unique index for that, so every handler that creates an order (or reopens a failed one) calls `context.LockMembershipOrders(clubId, seasonId)` (advisory lock until the transaction ends) before it checks.
- `MembershipEligibility` (static domain service): `Check(plan, today, candidates, otherFamilyAdults)` returns the reasons a plan can't be chosen (`BirthYearOutOfRange`, `TooManyAdults`, `ChildrenNotIncluded`, `NotANewMember`, `NoEnrolledFamilyAdult`, `PlanNotAvailable`, `MemberNotInFamily`, `AlreadyHasMembership`, `OnlyOneMember`, `AdultRequired`). For a plan for one member the birth years and "new member" apply to that member, for couples and families to the adults. Its tests (`Bookennis.Domain.Tests/Memberships`) are the price lists of three real clubs - add a case there when a rule changes.
- `ClubPaymentAccount` (`TenantDomainEntity`, unique per club): `StripeAccountId`, `ChargesEnabled`, `PayoutsEnabled`, `DetailsSubmitted`. Deleting the row disconnects the club.
- `StripeWebhookEvent` (not tenant specific): `EventId` (unique), `Type`, `StripeAccountId?`, `ObjectId`, `ProcessedAt?`. No payload is stored.
- `MemberSeason.MembershipOrderId?` (set null): the order that enrolled the member, `null` = by hand.

### Rules of the handlers (`Business/Memberships`)

- `MembershipPeople.Load` gives the handlers what the rules need about members (family, birth year, earlier seasons, enrollment, active order). `OrderableBy(memberId)` is who a member may order for: themself and, **as a parent**, the family - the same right that lets a parent book for children. `CreateMembershipOrder` answers anything else with 403.
- `IMembershipFulfillment.Complete(order)` is the **only** place that turns an order into `MemberSeason`s (unless the plan does not enroll). Call it when an order becomes paid or its debit starts, and save together with the status change. It skips members who are already enrolled, so calling it twice does no harm. `Revoke(order)` takes back the enrollments **this order** created (`MemberSeason.MembershipOrderId`), never the ones made by hand or by another order. It runs when a SEPA debit fails (`checkout.session.async_payment_failed`; paying again enrolls again), when a member withdraws an order, and when a treasurer cancels a paid order with "also remove from the season" (`cancel?removeEnrollments=true`, ticked by default in `CancelOrderDialog`). Refunds and disputes don't revoke: the treasurers get an email and decide. Bookings already made stay.
- The treasurer decides who gets which plan: `RecordMembershipPayment` does **not** check the rules of the plan, only that the members belong to the club and have no membership yet.
- The payments list (`GetMembershipPayments`) is the treasurers' only list of members (they are no `ClubMemberViewer`). It carries names, family, plan and payment - **never email, birthday or address**. The one exception outside the list: treasurers may open and change a family (`FamiliesController`) to set it up for a couple or family plan, and a family shows the birthdays of its children. `MembershipOrderDto` (what members see) never carries the note or anything of Stripe.
- Support mode has no member: treasurer handlers use the nullable `GetClubMemberId` (`MarkedPaidByMemberId` stays `null`). The member's own endpoints (`MembershipsController`) need a member.
- `DeleteSeason` is rejected with `SeasonHasPayments` when the season has orders that are `Paid` or `Processing`; unpaid orders are deleted with the season. `DeleteMembershipPlan` is rejected once the plan has any order (`MembershipPlanHasOrders`).
- `MemberRemoval`: paid orders stay as the club's record without the person (the database takes the member out and empties the payer); open orders that would cover nobody are deleted. `ExportPersonalData` lists `MembershipOrders` without Stripe ids.

### Stripe (`Business/Payments`)

- Two kinds of Checkout Sessions exist: membership orders and court purchases (see Court Sales > Stripe kind discrimination). Membership behaviour is unchanged.
- **Nothing but `StripeGateway` (`IStripeGateway`) talks to Stripe.** It is a substitute in `TestFixture`; wrap calls made for a user in `StripeCalls.Run`, which turns a failure into the localized `PaymentProviderUnavailable`.
- Settings: `AppSettings.Stripe` (`SecretKey`, `WebhookSecret`, `ApplicationFeePercent` = 0). Without keys `IStripeGateway.IsConfigured` is `false`: the FE hides the online part and the endpoints answer `PaymentsNotConfigured`. Never commit keys.
- **Connect**: the installation is the platform, the club is the merchant. `ConnectPaymentAccount` creates a connected account with the full Stripe dashboard (the club pays the Stripe fees and answers its disputes, Stripe collects the requirements) and returns the link to Stripe's onboarding. The account and the onboarding link are created with the **Accounts v2 API** (`Client.V2.Core.Accounts` / `AccountLinks`, `merchant` configuration, `dashboard: full`, fees and losses collected by Stripe): Stripe refuses Accounts v1 for new Connect platforms. The account is still read with the v1 endpoint (`charges_enabled`, `payouts_enabled`, `details_submitted`), which answers for v2 accounts too. `account.updated` and `RefreshPaymentAccount` keep the status current.
- **Checkout**: `StartMembershipCheckout` creates a Checkout Session **on the connected account** (direct charge) with the name and amount inline (`price_data`) - nothing is kept in Stripe's product catalog. Payment methods are not listed in code: Stripe shows what the club switched on. A new session closes the previous one, and cancelling or marking an order as paid closes its open session (`IMembershipCheckout.ExpireSession`).
- **What the browser says proves nothing; only what Stripe answers counts.** Three ways lead to an order change, all idempotent:
  - The webhook: `POST api/Stripe/Webhook` (`StripeController`, anonymous, not a club route) → `ReceiveStripeWebhook` verifies the signature and hands the event to `StripeEventInbox.Accept`, which stores it (a known event id is acknowledged and ignored) and enqueues `ProcessStripeEvent` (`[OutOfBand]`).
  - The member coming back (Stripe recommends both): `GET Memberships/orders/{id}` first runs `SyncMembershipCheckout`, which asks Stripe for the Checkout Session of an order that waits for its payment and books a completed one. The session must name the order (`client_reference_id`). Best effort: errors are logged, the webhook books it later.
  - Missed events: the recurring `StripeEventRecoveryJob` (hourly) lists the undelivered events of every connected account (`delivery_success=false`, kept 30 days by Stripe, which retries for only 3) and feeds them into `StripeEventInbox.Accept`. The account of the listing counts, not what the event says. This does not catch events missed by `stripe listen`: the CLI is no registered endpoint.
  - `IMembershipSettlement.Settle` is the one place that books an online payment (used by the webhook and the sync); a second arrival finds the order settled. A race between the two ends in a concurrency error for one of them (`Metadata.Modified` is the concurrency token): the job is retried by Hangfire, the sync ignores it.
- `ProcessStripeEvent` **loads the object again from Stripe** instead of trusting the payload. **The club is the one the Stripe account of the event belongs to** (`ClubPaymentAccount.StripeAccountId`), and an order is only touched when it belongs to that club (the queries use `IgnoreQueryFilters()` and filter by `ClubId` themselves). An order id of another club in a session changes nothing.
- SEPA: `checkout.session.completed` with an unpaid session marks the order `Processing` and enrolls right away; `async_payment_succeeded` marks it paid; `async_payment_failed` marks it failed and mails the payer and the treasurers. A full refund (`charge.refunded`) or a dispute only changes the status and mails the treasurers.

### API / Authorization

- `MembershipPlansController` (`MembershipPlans`): read `ClubMember` (members only get self-service plans, treasurers also the rest and the order counts), write `ClubTreasurer` (create, update, delete, `copy`, `sort`). Copying shifts birth years and dates by the difference of the season years.
- `MembershipsController` (`Memberships`, `ClubMember`): `options`, `orders` (create), and per order get, `checkout`, `confirm` (free plan), delete (withdraw). **Every action on an order is authorized with the resource `MembershipOrderAuthorizationModel` and the policy `OwnsMembershipOrder`**: the payer, a covered member, a parent of a covered child, or a Treasurer/Admin of the club.
- `MembershipPaymentsController` (`MembershipPayments`, `ClubTreasurer`): list, `summary`, record a payment, `approve`, `reject`, `mark-paid`, `cancel`, `export` (ClosedXML, `Export.MembershipPayments(.de).resx`). `approve` and `mark-paid` take optional `memberIds`: the treasurer corrects who the order covers, e.g. adds the partner the member forgot (`MembershipPayments.SetMembers` -> `MembershipOrder.SetMembers`, only while `AwaitingApproval`, `AwaitingPayment` or `Failed`; once money is on its way the members are enrolled by the order). The price is per order, so it stays. As in `RecordMembershipPayment` the rules of the plan are not enforced for the treasurer, only that the members belong to the club and have no other membership in the season. In the FE a plan for one member is confirmed with one click, a couple or family plan opens `ApproveOrderDialog`; `MarkPaidDialog` shows the same block for such a plan.
- `PaymentsController` (`Payments`, `ClubTreasurer`): `configuration`, `account` (status only, never the Stripe account id).
- `PaymentAccountController` (`Payments/account/connect|refresh`, `DELETE Payments/account`): policy `ClubPaymentAccountManager` = a signed-in Admin, cookie only. **Where the money of a club goes is never decided by a treasurer or an API key.** It is its own controller so the rest of `Payments` can be open to keys (see `ControllersOpenToKeys`).
- `MembershipSecurityTests` attacks all of this over HTTP, including the webhook with forged, foreign and replayed signatures (the fixture runs with a known webhook secret).

### Emails

`MembershipPaymentReceived`, `MembershipApproved`, `MembershipPaymentFailed` (to the payer, or a parent for a child without email) and `MembershipNeedsAttention` (to Treasurers and Admins; the reason is a text of `Notifications(.de).resx`, `Membership_Reason_…`). They are club emails that are always sent and have no notification preference. Send them with `IMembershipNotifier` (a substitute in `TestFixture`) **after** the changes are saved.

### FE

- Stores (`Services/Store/Memberships`): `IMembershipPlansStore`, `IMembershipsStore` (`Options` for the membership page, `Current` isolated for My Club), `IMembershipPaymentsStore`, `IPaymentAccountStore`. Texts are in `MembershipsLocale(.de).resx`, helpers (amounts, chips, the sentences that read a plan back, status colors) in `MembershipHelpers`.
- `Pages/ClubShell/Club/Memberships`: `MembershipPlans.razor` (`/clubs/{id}/membership-plans`, plan cards per season, `EditMembershipPlanDialog` with a live summary, copy, sort, and `PaymentAccountCard` for the Stripe account) and `MembershipPayments.razor` (`/clubs/{id}/membership-payments`, summary tiles, filter chips with counts, rows as table on desktop and cards on phones, `RecordPaymentDialog`, `MarkPaidDialog`, export). Both for Treasurers and Admins (`MembershipHelpers.CanManage`), linked in the `NavMenu`.
- `Pages/ClubShell/Membership/Membership.razor` (`/clubs/{id}/membership`): the member's page. State of the season on top, the own orders with their next step, then the plans. A plan for one member is offered per person, a couple or family plan once with the members it can cover. Coming back from Stripe with `checkout=success` it **polls the order** (the webhook may arrive after the redirect) and then removes the query parameters. Leaving for Stripe is `NavigateTo(url, forceLoad: true)`; the CSP needs no change because nothing of Stripe is embedded.
- My Club: `MembershipCard` (only when there is something to do or to wait for), the "not enrolled" chip of `MyClubHero` links to the page, `AccountCard` shows the plan. Member administration: `SeasonEnrollmentEditor` marks how a season was entered (`MembersDetailResult.SeasonEnrollments`) and asks before a paid season (`MemberResult.PaidSeasonIds`) is switched off.

### Not built yet

Add-ons (e.g. a locker fee), booking rules per plan (a "light" membership is a price only), automatic renewal, pro-rated prices, refunds from within Clubnetz, receipts as PDF, reminders for unpaid fees.

## Privacy (GDPR) & Legal Pages

- `Pages/Legal`: `/legal/imprint` and `/legal/privacy` (public, `NoNavigationLayout`), texts in `LegalLocale(.de).resx` (`LegalSection` renders lines starting with "- " as a list). The operator details differ per installation and are never committed: they come from the `Legal` section of the server settings (`AppSettings.Legal`, e.g. the environment variable `Legal__OperatorName`), are served by the anonymous `GET api/Legal` (`GetLegalSettings`) and loaded once by `ILegalStore`; the pages show a warning while the operator is not configured. Update `LegalDocument.LastUpdated` when the texts change. `LegalLinks` is shown in `AuthShell` and at the bottom of the `NavMenu`.
- Registration requires `AcceptPrivacyPolicy` (`User.PrivacyPolicyAcceptedAt`, `ErrorCode.PrivacyPolicyNotAccepted`).
- Self-service in the profile (`AccountOwner` policy): `GET api/Profile/export` (`ExportPersonalData`, JSON download of all personal data - extend `PersonalDataExport` when adding new personal data), `POST api/Profile/LeaveClub` (`LeaveClub`, removes the `ClubMember` only, never a `GuestMember`) and `POST api/Profile/DeleteAccount` (`DeleteOwnAccount`, requires the password, signs out).
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
- **Booking reminder**: `SendBookingReminders`, run every 5 minutes for all clubs by the recurring Hangfire job `BookingReminderJob` (scheduled in `Program.cs`). **Every recurring job class must also be registered in the container** (`ServicesConfiguration.AddServices`, checked by `RecurringJobRegistrationTests`): Hangfire resolves it from SimpleInjector, which does not create unregistered types, and the job would only fail when it runs. Reminds the players of bookings that start within `BookingReminderLeadMinutes` (default 120) by push and email and marks them with `Booking.ReminderSentAt` (reset when a booking is moved). Bookings made within the lead time are marked without a reminder. The emails are also sent when push is not configured.
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

`Welcome`, `SeasonActivated`, the four membership emails, the court purchase emails and the two clubhouse payment emails (they are about money) are not notifications: they are always sent and have no preference.

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

- **Error pages** (`Pages/Errors`): `AccessDenied` (`/accessdenied`, shown for a 403), `NotFound` (`/notfound` and, rendered in place by `App.razor`, every unknown address) and `Error` (`/error`, shown for a 500) all use `ErrorPage.razor` (emoji, optional status code, title, text, hint, buttons). Texts are in `ErrorsLocale(.de).resx`. A new error page uses the same component. The bar Blazor shows when the app itself crashed (`#blazor-error-ui` in `index.html`) is plain HTML: styled in `app.css`, German text set by `App.localizeErrorBar` in `js-helpers.js`.

- Pickers with `PickerVariant.Dialog` inside a `MudDialog` only work because `app.css` drops the transform MudBlazor leaves on `.mud-dialog` after its opening animation (`animation-fill-mode: backwards`). Without it the picker is squeezed into the dialog and cut off on desktop. Don't add `transform`, `filter` or `backdrop-filter` to a dialog for the same reason.
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
