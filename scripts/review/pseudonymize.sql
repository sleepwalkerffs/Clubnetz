-- Replaces every piece of personal data and every live secret in a COPY of the production
-- database, so the copy can back the review environments of pull requests.
-- Run it only through refresh-template.sh, never against the production database itself.
--
-- What it does, for every club:
--   - members get invented names, addresses, phone numbers, shifted birthdays and
--     email addresses on @example.test. Everyone gets the review password (its hash is passed
--     in as :password_hash, never a real one). The account passed in as :owner_email keeps
--     its email, so the owner can log in as themself and as any member, admin or treasurer
--   - free texts that may name people (comments, notes, guest names, announcements, event
--     texts and questions, court blocking titles, subscription plans) become neutral texts
--   - live secrets and payment references are removed: SumUp tokens, Stripe ids, club API
--     keys, push subscriptions, login tokens, email templates (may hold signatures)
--   - clubs and courts get neutral names
-- Club configuration (play modes, prices, membership plans, clubhouse items, badges) is kept.
--
-- When a migration adds a column that can hold personal data or a secret, add it here.

\set ON_ERROR_STOP on

begin;

create temp table first_names_m(i serial, n text);
insert into first_names_m(n) values
 ('Lukas'),('Jonas'),('Felix'),('Elias'),('Paul'),('Leon'),('Maximilian'),('Tobias'),('Simon'),('David'),
 ('Florian'),('Matthias'),('Andreas'),('Michael'),('Thomas'),('Markus'),('Stefan'),('Martin'),('Christian'),('Daniel'),
 ('Johannes'),('Philipp'),('Sebastian'),('Fabian'),('Julian'),('Niklas'),('Moritz'),('Valentin'),('Raphael'),('Benedikt'),
 ('Gerhard'),('Herbert'),('Werner'),('Wolfgang'),('Franz'),('Josef'),('Peter'),('Karl'),('Walter'),('Bernhard'),
 ('Noah'),('Ben'),('Luca'),('Theo'),('Emil'),('Anton'),('Jakob'),('Samuel'),('Oskar'),('Vincent');

create temp table first_names_f(i serial, n text);
insert into first_names_f(n) values
 ('Anna'),('Laura'),('Sarah'),('Lena'),('Julia'),('Katharina'),('Lisa'),('Hannah'),('Sophie'),('Marie'),
 ('Elena'),('Johanna'),('Theresa'),('Magdalena'),('Viktoria'),('Christina'),('Sabine'),('Andrea'),('Claudia'),('Petra'),
 ('Monika'),('Barbara'),('Elisabeth'),('Martina'),('Susanne'),('Karin'),('Birgit'),('Silvia'),('Nicole'),('Verena'),
 ('Emma'),('Mia'),('Lea'),('Ella'),('Clara'),('Valentina'),('Leonie'),('Emilia'),('Nora'),('Amelie'),
 ('Ingrid'),('Helga'),('Renate'),('Gabriele'),('Eva'),('Ursula'),('Doris'),('Anita'),('Carina'),('Melanie');

create temp table last_names(i serial, n text);
insert into last_names(n) values
 ('Berger'),('Huber'),('Wagner'),('Fischer'),('Gruber'),('Brunner'),('Hofer'),('Steiner'),('Moser'),('Mayer'),
 ('Bauer'),('Winkler'),('Lehner'),('Schwarz'),('Wieser'),('Pichler'),('Aigner'),('Fuchs'),('Egger'),('Holzer'),
 ('Haas'),('Schmid'),('Leitner'),('Baumgartner'),('Lang'),('Weber'),('Wolf'),('Koller'),('Reiter'),('Mair'),
 ('Ebner'),('Hager'),('Schuster'),('Lechner'),('Auer'),('Wimmer'),('Kofler'),('Riedl'),('Graf'),('Strasser'),
 ('Sommer'),('Winter'),('Brandner'),('Kaiser'),('Frank'),('Stadler'),('Hartmann'),('Neumann'),('Vogel'),('Zimmermann'),
 ('Lindner'),('Seidl'),('Kern'),('Thaler'),('Pfeifer'),('Ortner'),('Platzer'),('Hammerer'),('Kessler'),('Rainer');

create temp table cities(i serial, zip text, n text);
insert into cities(zip, n) values
 ('6830','Sonnental'),('6832','Obersonnental'),('6833','Weinberg'),('6834','Lindenau'),('6835','Am Bach'),('6836','Talheim');

create temp table streets(i serial, n text);
insert into streets(n) values
 ('Hauptstraße'),('Schulgasse'),('Lindenweg'),('Am Sportplatz'),('Kirchplatz'),('Wiesenweg'),('Bergstraße'),('Gartenweg'),
 ('Rosengasse'),('Bahnhofstraße'),('Feldweg'),('Mühlgasse'),('Sonnenweg'),('Weinbergstraße'),('Birkenallee'),('Tennisweg');

-- Users ----------------------------------------------------------------------------

create temp table owner_account as
select "Id" from "AspNetUsers" where "NormalizedEmail" = upper(:'owner_email');

do $$ begin
    if (select count(*) from owner_account) <> 1 then
        raise exception 'The owner account was not found';
    end if;
end $$;

create temp table new_names as
select u."Id",
       case when u."Gender" = 1
            then (select n from first_names_f where i = 1 + (u."Id"::bigint * 7919) % 50)
            else (select n from first_names_m where i = 1 + (u."Id"::bigint * 7919) % 50) end as first_name,
       (select n from last_names where i = 1 + (u."Id"::bigint * 104729) % 60) as last_name
from "AspNetUsers" u;

-- Members of one family share the last name of the parent who owns their account.
update new_names c set last_name = p.last_name
from "AspNetUsers" u join new_names p on p."Id" = u."BelongsToUserId"
where c."Id" = u."Id";

update "AspNetUsers" u set
    "FirstName" = n.first_name,
    "LastName" = n.last_name,
    "FullName" = n.first_name || ' ' || n.last_name,
    "Email" = case when u."Email" is null then null
                   when u."Id" = (select "Id" from owner_account) then u."Email"
                   else lower(translate(n.first_name || '.' || n.last_name, 'äöüß', 'aous')) || '.' || u."Id" || '@example.test' end,
    "UserName" = case when u."UserName" is null then null
                      when u."Id" = (select "Id" from owner_account) then u."UserName"
                      else lower(translate(n.first_name || '.' || n.last_name, 'äöüß', 'aous')) || '.' || u."Id" || '@example.test' end,
    -- Empty stays empty, so incomplete profiles stay incomplete
    "Street" = case when nullif(u."Street", '') is null then u."Street"
                    else (select s.n from streets s where s.i = 1 + (u."Id"::bigint * 31) % 16) || ' ' || (1 + u."Id" % 48) end,
    "City" = case when nullif(u."City", '') is null then u."City" else (select c.n from cities c where c.i = 1 + u."Id" % 6) end,
    "ZipCode" = case when nullif(u."ZipCode", '') is null then u."ZipCode" else (select c.zip from cities c where c.i = 1 + u."Id" % 6) end,
    "PhoneNumber" = case when nullif(u."PhoneNumber", '') is null then u."PhoneNumber"
                         else '+43 660 ' || lpad(((u."Id"::bigint * 7331) % 10000000)::text, 7, '0') end,
    "Birthday" = case when u."Birthday" is null then null else u."Birthday" + ((u."Id"::bigint * 37) % 241 - 120)::int end,
    "PasswordHash" = :'password_hash',
    "SecurityStamp" = upper(replace(gen_random_uuid()::text, '-', '')),
    "ConcurrencyStamp" = gen_random_uuid()::text,
    "EmailConfirmed" = true,
    "LockoutEnd" = null,
    "AccessFailedCount" = 0,
    "TwoFactorEnabled" = false
from new_names n
where n."Id" = u."Id";

update "AspNetUsers" set
    "NormalizedEmail" = upper("Email"),
    "NormalizedUserName" = upper("UserName");

delete from "AspNetUserTokens";
delete from "AspNetUserLogins";
delete from "AspNetUserClaims";
delete from "UserProfilePictures";
delete from "PushSubscriptions";

-- Free texts that may name people -------------------------------------------------

update "Bookings" set "Comment" = null where "Comment" is not null;
update "RecurringBookingSeries" set "Comment" = null where "Comment" is not null;
update "ClubEventRegistrations" set "Comment" = null where "Comment" is not null;
update "MembershipOrders" set "Note" = null where "Note" is not null;
update "BookingCreditGrants" set "Note" = null where "Note" is not null;
update "ClubhouseTabs" set "GuestName" = 'Gast ' || "Id" where "GuestName" is not null;
update "CourtBlockings" set "Title" = 'Platzsperre ' || "Id";

update "SubscriptionPlans" set "Name" = 'Abo ' || "Id";
update "SubscriptionPlanParticipants" p set "Name" = (select n from first_names_m where i = 1 + (p."Id"::bigint * 7919) % 50)
    || ' ' || (select n from last_names where i = 1 + (p."Id"::bigint * 104729) % 60);

update "ClubAnnouncements" set
    "Title" = 'Neuigkeit ' || "Id",
    "Body" = 'Dieser Text ersetzt den Inhalt der Neuigkeit in der Review-Umgebung. '
          || 'Die Platzpflege beginnt nächste Woche, bitte die Hinweise am Clubhaus beachten.';
delete from "ClubAnnouncementAttachmentContents";
delete from "ClubAnnouncementAttachments";

update "ClubEvents" set
    "Title" = 'Veranstaltung ' || "Id",
    "Description" = case when "Description" is null then null
                         else 'Dieser Text ersetzt die Beschreibung der Veranstaltung in der Review-Umgebung.' end,
    "Location" = case when "Location" is null then null else 'Clubhaus' end;
update "ClubEventQuestions" set "Text" = 'Frage ' || "Id";
update "ClubEventQuestionOptions" set "Label" = 'Antwort ' || "Id";

update "MembershipPlans" set "Description" = null where "Description" is not null;

-- Accounting: texts of entries and the history name who paid; receipts are photos of invoices with names and addresses.
update "AccountingEntries" set
    "Text" = 'Buchung ' || "Number",
    "CounterpartyName" = case when "CounterpartyName" is null then null else 'Person ' || "Id" end,
    "CancelReason" = null;
delete from "AccountingEntryChanges";
delete from "AccountingReceiptContents";
delete from "AccountingReceipts";
update "FiscalYears" set "AuditComment" = null where "AuditComment" is not null;
-- Clubs name accounts and cost centers after people ("Sparbuch Max Muster", "Training Huber").
update "MoneyAccounts" set "Iban" = null, "Name" = 'Konto ' || "Id";
update "CostCenters" set "Name" = 'Kostenstelle ' || "Id";
-- Suggested entries and bank statements carry who paid, their IBANs and what they wrote; rules look for names.
update "AccountingProposals" set
    "Text" = 'Vorschlag ' || "Id",
    "CounterpartyName" = case when "CounterpartyName" is null then null else 'Person ' || "Id" end;
update "BankTransactions" set
    "CounterpartyName" = case when "CounterpartyName" is null then null else 'Partner ' || "Id" end,
    "CounterpartyIban" = null,
    "Reference" = 'Umsatz ' || "Id",
    "BankReference" = null,
    "Fingerprint" = 'fp-' || "Id";
update "BankStatementImports" set "FileName" = 'auszug-' || "Id" || '.xml';
update "BankRules" set "MatchText" = 'Regel ' || "Id", "MatchIban" = null, "Text" = null;

-- Email templates may carry names, phone numbers and bank details. The app falls back to its built-in texts.
delete from "ClubEmailTemplates";

-- Clubs and courts ---------------------------------------------------------------

update "Clubs" set
    "Name" = 'Testclub ' || "Id",
    "ReplyToEmail" = case when "ReplyToEmail" is null then null else 'vorstand.' || "Id" || '@example.test' end,
    "WebsiteUrl" = case when "WebsiteUrl" is null then null else 'https://testclub-' || "Id" || '.example' end;

-- Courts may be named after people of the club.
update "Courts" c set
    "Name" = 'Platz ' || n.nr,
    "Alias" = case when c."Alias" = '' then '' else 'P' || n.nr end
from (select "Id", row_number() over (partition by "ClubId" order by "SortOrder", "Id") as nr from "Courts") n
where n."Id" = c."Id";

-- Live secrets and payment references --------------------------------------------

-- A key hash of production would still open the review environment's API.
delete from "ClubApiKeys";
delete from "StripeWebhookEvents";

update "ClubPaymentAccounts" set "StripeAccountId" = 'acct_review_' || "Id";
update "CourtPurchases" set
    "StripeCheckoutSessionId" = case when "StripeCheckoutSessionId" is null then null else 'cs_review_' || "Id" end,
    "StripePaymentIntentId" = case when "StripePaymentIntentId" is null then null else 'pi_review_' || "Id" end;
update "MembershipOrders" set
    "StripeCheckoutSessionId" = case when "StripeCheckoutSessionId" is null then null else 'cs_review_' || "Id" end,
    "StripePaymentIntentId" = case when "StripePaymentIntentId" is null then null else 'pi_review_' || "Id" end;

update "ClubhouseSumUpAccounts" set
    "AccessToken" = 'removed',
    "RefreshToken" = 'removed',
    "AccessTokenExpiresAt" = '2000-01-01',
    "NeedsReconnect" = true,
    "MerchantCode" = 'MREVIEW' || "Id",
    "MerchantName" = case when "MerchantName" is null then null else 'Clubhaus ' || "ClubId" end;
update "ClubhouseTabPayments" set
    "MerchantCode" = 'MREVIEW',
    "CheckoutId" = case when "CheckoutId" is null then null else 'review-' || "Id" end,
    "CheckoutUrl" = null,
    "TransactionCode" = case when "TransactionCode" is null then null else 'TREVIEW' || "Id" end;

-- Checks: stop if anything slipped through ---------------------------------------

do $$ begin
    if exists (select 1 from "AspNetUsers"
               where "Email" is not null and "Email" not like '%@example.test'
                 and "Id" <> (select "Id" from owner_account)) then
        raise exception 'An email address was not replaced';
    end if;
    if exists (select 1 from "AspNetUsers" u join new_names n on n."Id" = u."Id"
               where u."FirstName" is distinct from n.first_name) then
        raise exception 'A name was not replaced';
    end if;
    if exists (select 1 from "PushSubscriptions") or exists (select 1 from "ClubApiKeys")
       or exists (select 1 from "AspNetUserTokens") or exists (select 1 from "UserProfilePictures") then
        raise exception 'Secrets or pictures were not removed';
    end if;
end $$;

commit;

-- Old background jobs carry real email addresses in their arguments. Hangfire
-- recreates its schema on the next start.
drop schema if exists hangfire cascade;
