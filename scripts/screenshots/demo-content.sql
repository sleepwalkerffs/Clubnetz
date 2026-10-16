-- Fills the pseudonymized screenshot database with lively, invented content for club 1:
-- a busy booking week around today, upcoming events with registrations, club news and
-- membership fee orders. Texts are German or English (:lang = de | en).
-- Run it only through New-ScreenshotDatabase.ps1, after pseudonymize.sql.

\set ON_ERROR_STOP on

begin;

create temp table t(k text primary key, de text, en text);
insert into t values
 ('ev1', 'Clubmeisterschaft 2026', 'Club Championship 2026'),
 ('ev1d', 'Einzel und Doppel für alle Altersklassen. Die Auslosung hängt ab Freitag im Clubhaus und steht hier in der App.', 'Singles and doubles for all age groups. The draw will be posted in the clubhouse and here in the app from Friday.'),
 ('ev2', 'Saisonabschluss mit Grillfest', 'End-of-season barbecue'),
 ('ev2d', 'Wir feiern die Saison! Grill, Getränke und Siegerehrung der Clubmeisterschaft. Familie und Freunde sind willkommen.', 'Let''s celebrate the season! Barbecue, drinks and the club championship awards. Family and friends are welcome.'),
 ('ev2q', 'Was dürfen wir für dich grillen?', 'What can we grill for you?'),
 ('ev2o1', 'Bratwurst', 'Sausage'),
 ('ev2o2', 'Steak', 'Steak'),
 ('ev2o3', 'Gemüsespieß', 'Veggie skewer'),
 ('ev3', 'Einwintern der Plätze', 'Winterizing the courts'),
 ('ev3d', 'Netze abbauen, Linien abdecken, Clubhaus winterfest machen. Jede helfende Hand zählt als Arbeitsstunde.', 'Taking down the nets, covering the lines and getting the clubhouse ready for winter. Every pair of hands counts as a work hour.'),
 ('ev4', 'Jahreshauptversammlung', 'Annual general meeting'),
 ('ev4d', 'Rückblick auf die Saison, Kassabericht und Wahl des Vorstands.', 'Review of the season, treasurer''s report and election of the board.'),
 ('loc', 'Clubhaus', 'Clubhouse'),
 ('loc2', 'Anlage TC Sonnental', 'TC Sonnental courts'),
 ('n1', 'Mitgliedsbeitrag jetzt online bezahlen', 'Pay your membership fee online'),
 ('n1b', E'Ab sofort wählst du unter **Meine Mitgliedschaft** deinen Tarif und bezahlst gleich mit Karte oder SEPA-Lastschrift. Familien erledigen das in einem Schritt für alle.\n\nÜberweisung und Barzahlung beim Kassier gehen natürlich weiterhin.', E'From now on you choose your plan under **My membership** and pay right away by card or SEPA direct debit. Families do it in one step for everyone.\n\nBank transfer and cash at the treasurer still work, of course.'),
 ('n2', 'Platz 3 und 4 am Samstag gesperrt', 'Courts 3 and 4 blocked on Saturday'),
 ('n2b', 'Wegen der Clubmeisterschaft sind Platz 3 und 4 am Samstag ganztägig belegt. Die anderen Plätze stehen wie gewohnt zur Verfügung.', 'Courts 3 and 4 are used for the club championship all day on Saturday. The other courts are open as usual.'),
 ('n3', 'Herren 35 steigen in die Landesliga auf', 'Men 35 promoted to the regional league'),
 ('n3b', 'Mit einem 5:1 im letzten Spiel hat unsere Herren-35-Mannschaft den Meistertitel geholt. Gratulation an das ganze Team!', 'With a 5:1 win in the last match our men 35 team won the title. Congratulations to the whole team!'),
 ('p10', 'Erwachsene', 'Adult'),
 ('p11', 'Jugend', 'Youth'),
 ('p12', 'Kinder bis 8', 'Children up to 8'),
 ('p13', 'Familie', 'Family'),
 ('p14', 'Studierende', 'Student'),
 ('p15', 'Partnerkarte', 'Partner'),
 ('p16', 'Passiv', 'Passive'),
 ('p17', 'Ehrenmitglied', 'Honorary member'),
 ('p20', 'Familie mit einem Erwachsenen', 'Single-parent family'),
 ('p11d', 'Für alle, die noch zur Schule gehen', 'For everyone still at school'),
 ('p13d', 'Zwei Erwachsene und alle Kinder', 'Two adults and all children'),
 ('p20d', 'Ein Erwachsener und alle Kinder', 'One adult and all children');

create function pg_temp.tx(key text) returns text language sql as
$$ select case when current_setting('screens.lang') = 'en' then en else de end from t where k = key $$;

select set_config('screens.lang', :'lang', false) as lang \gset

-- No "complete your profile" banner on the screenshots.
update "AspNetUsers" set "ProfileCompletionBannerDismissedAt" = now();

-- Bookings: move a busy September week to the current week ----------------------

create temp table shift as select (date_trunc('week', now())::date - date '2026-09-07') as days;

delete from "Bookings" b
where b."ClubId" = 1 and b."RecurringBookingSeriesId" is null
  and b."Interval_From" >= date_trunc('week', now()) and b."Interval_From" < date_trunc('week', now()) + interval '7 days';

update "Bookings" set
    "Interval_From" = "Interval_From" + (select days from shift) * interval '1 day',
    "Interval_To" = "Interval_To" + (select days from shift) * interval '1 day',
    "ReminderSentAt" = null
where "ClubId" = 1 and "RecurringBookingSeriesId" is null
  and "Interval_From" >= '2026-09-07' and "Interval_From" < '2026-09-14';

-- Drop moved bookings that now collide with series bookings on the same court.
delete from "Bookings" a
using "Bookings" b
where a."ClubId" = 1 and a."RecurringBookingSeriesId" is null and b."RecurringBookingSeriesId" is not null
  and a."CourtId" = b."CourtId" and a."Interval_From" < b."Interval_To" and b."Interval_From" < a."Interval_To"
  and a."Interval_From" >= date_trunc('week', now()) and a."Interval_From" < date_trunc('week', now()) + interval '7 days';

-- The local test admin plays in three of the coming bookings.
with next as (
    select bp."Id", row_number() over (partition by b."Id" order by bp."Id") as rn, b."Id" as booking,
           b."Interval_From"::date as day, b."Interval_From" as starts
    from "Bookings" b join "BookingPlayers" bp on bp."BookingEntryId" = b."Id"
    where b."ClubId" = 1 and b."RecurringBookingSeriesId" is null and b."Interval_From" > now() + interval '12 hours'
      and not exists (select 1 from "BookingPlayers" x where x."BookingEntryId" = b."Id" and x."MemberId" = 3320)
), picked as (
    select booking from (select distinct on (day) booking, starts from next order by day, starts desc) d order by starts limit 3
)
update "BookingPlayers" bp set "MemberId" = 3320
from next n
where n."Id" = bp."Id" and n.rn = 1 and n.booking in (select booking from picked);

-- Events ----------------------------------------------------------------------------

delete from "ClubEvents" where "ClubId" = 1;

create temp table ev(k text, id int, category int, start_date date, end_date date, start_time time, end_time time,
                     registration boolean, max_participants int, deadline timestamptz, loc text);
insert into ev values
 ('ev1', nextval('"ClubEvents_Id_Sequence"'), 3, current_date + 3, current_date + 4, '09:00', '18:00', true, 32, now() + interval '2 days', 'loc2'),
 ('ev2', nextval('"ClubEvents_Id_Sequence"'), 2, current_date + 10, null, '18:00', '23:00', true, null, now() + interval '8 days', 'loc'),
 ('ev3', nextval('"ClubEvents_Id_Sequence"'), 1, current_date + 17, null, '09:00', '13:00', true, 20, null, 'loc2'),
 ('ev4', nextval('"ClubEvents_Id_Sequence"'), 4, current_date + 37, null, '19:30', '21:30', false, null, null, 'loc');

insert into "ClubEvents"("Id", "ClubId", "Category", "Title", "Description", "Location", "StartDate", "EndDate", "StartTime", "EndTime",
                         "RegistrationEnabled", "MaxParticipants", "RegistrationDeadline", "NotifyMembers", "CreatedByMemberId",
                         "ReminderSentAt", "DeadlineReminderSentAt")
select id, 1, category, pg_temp.tx(k), pg_temp.tx(k || 'd'), pg_temp.tx(loc), start_date, end_date,
       -- Event times are stored in UTC.
       ((start_date + start_time) at time zone 'Europe/Vienna' at time zone 'UTC')::time,
       ((start_date + end_time) at time zone 'Europe/Vienna' at time zone 'UTC')::time,
       registration, max_participants, deadline, false, 3320, now(), now()
from ev;

-- Registrations from active members of the current season.
create temp table active_members as
select ms."MemberId", row_number() over (order by (ms."MemberId" * 2654435761::bigint) % 1000003) as rn
from "MemberSeasons" ms join "Seasons" s on s."Id" = ms."SeasonId"
where s."ClubId" = 1 and current_date between s."Period_From" and s."Period_To";

insert into "ClubEventRegistrations"("Id", "ClubEventId", "MemberId", "HeadCount", "RegisteredAt")
select nextval('"ClubEventRegistrations_Id_Sequence"'), e.id, a."MemberId",
       case when e.k = 'ev2' then 1 + a.rn % 3 else 1 end,
       now() - (a.rn || ' hours')::interval
from ev e join active_members a on a.rn <= case e.k when 'ev1' then 24 when 'ev2' then 38 when 'ev3' then 13 else 0 end;

-- Barbecue question with answers.
create temp table q as select nextval('"ClubEventQuestions_Id_Sequence"') as id;
insert into "ClubEventQuestions"("Id", "ClubEventId", "Text", "SelectionMode", "IsRequired", "AllowQuantities", "LimitQuantityToHeadCount", "SortOrder")
select q.id, e.id, pg_temp.tx('ev2q'), 0, true, true, true, 0 from q, ev e where e.k = 'ev2';

create temp table qo as
select nextval('"ClubEventQuestionOptions_Id_Sequence"') as id, o.k, o.sort from (values ('ev2o1', 0), ('ev2o2', 1), ('ev2o3', 2)) o(k, sort);
insert into "ClubEventQuestionOptions"("Id", "ClubEventQuestionId", "Label", "SortOrder")
select qo.id, q.id, pg_temp.tx(qo.k), qo.sort from qo, q;

insert into "ClubEventRegistrationAnswers"("Id", "ClubEventRegistrationId", "ClubEventQuestionOptionId", "Quantity")
select nextval('"ClubEventRegistrationAnswers_Id_Sequence"'), r."Id",
       (select id from qo where sort = r."Id" % 3), r."HeadCount"
from "ClubEventRegistrations" r join ev e on e.id = r."ClubEventId" and e.k = 'ev2';

-- News --------------------------------------------------------------------------------

delete from "ClubAnnouncements" where "ClubId" = 1;

insert into "ClubAnnouncements"("Id", "ClubId", "Title", "Body", "IsPinned", "PublishedAt", "CreatedByMemberId", "EmailSentAt", "EmailAudience", "EmailRecipientCount")
values
 (nextval('"ClubAnnouncements_Id_Sequence"'), 1, pg_temp.tx('n1'), pg_temp.tx('n1b'), true, now() - interval '2 days', 3320, now() - interval '2 days', 0, 412),
 (nextval('"ClubAnnouncements_Id_Sequence"'), 1, pg_temp.tx('n2'), pg_temp.tx('n2b'), false, now() - interval '5 hours', 3320, null, null, null),
 (nextval('"ClubAnnouncements_Id_Sequence"'), 1, pg_temp.tx('n3'), pg_temp.tx('n3b'), false, now() - interval '9 days', 3320, null, null, null);

-- Club data that is German in the dev database ----------------------------------

create temp table names_en(de text primary key, en text);
insert into names_en values
 ('Einzel', 'Singles'), ('Doppel', 'Doubles'), ('Turnier', 'Tournament'), ('Ballmaschine', 'Ball machine'),
 ('Reservierung', 'Reservation'), ('Gästebuchung', 'Guest booking'), ('Training', 'Training'), ('VMM', 'League match'),
 ('Schnuppertennis', 'Taster session'),
 ('Sandler', 'Slacker'), ('Grundlinien-Junkie', 'Baseline Junkie'), ('Wadlbeißer', 'Bulldog'),
 ('Club-Inventar', 'Part of the Furniture'), ('Tiebreak-Titan', 'Tiebreak Titan'), ('Ballermann', 'Big Hitter'),
 ('Grand-Slam-Garant', 'Grand Slam Material'), ('Platzhirsch', 'King of the Court'),
 ('Einzel Vereinsmeister', 'Singles club champion'),
 ('Du stehst endlich am Platz, aber eigentlich bist du nur für das Bier danach hier!', 'You finally made it to the court, but honestly you are here for the drinks afterwards!'),
 ('Du hast deinen Rhythmus gefunden. Die Grundlinie ist dein Zuhause.', 'You found your rhythm. The baseline is your home.'),
 ('Ein zäher Gegner! Du kämpfst um jeden einzelnen Punkt und gibst nie auf.', 'A tough opponent! You fight for every single point and never give up.'),
 ('Die Clubmitglieder zahlen schon Miete an dich, weil du immer da bist.', 'The members already pay you rent because you are always here.'),
 ('Ernsthafter Enthusiast. Durchschnittlich solide 2 Matches pro Woche.', 'Serious enthusiast. A solid two matches a week on average.'),
 ('Du spielst schwer, schlägst hart und lebst praktisch auf dem Platz.', 'You play heavy, hit hard and practically live on the court.'),
 ('Elite-Stufe. Du klopfst an die Tür des absolut legendären Status.', 'Elite level. You are knocking on the door of legendary status.'),
 ('Der König/die Königin der Anlage. Wenn du den Platz betrittst, verneigt sich das Netz.', 'Ruler of the courts. When you step on court, the net takes a bow.');

update "PlayModes" p set "Name" = n.en from names_en n where :'lang' = 'en' and n.de = p."Name";
update "BadgeTiers" b set "Name" = n.en from names_en n where :'lang' = 'en' and n.de = b."Name";
update "BadgeTiers" b set "Description" = n.en from names_en n where :'lang' = 'en' and n.de = b."Description";
update "OneTimeBadges" b set "Name" = n.en from names_en n where :'lang' = 'en' and n.de = b."Name";

-- Membership plans and fee orders for the current season -------------------------

update "MembershipPlans" p set
    "Name" = coalesce(pg_temp.tx('p' || p."Id"), p."Name"),
    "Description" = case when p."Description" is null then null else coalesce(pg_temp.tx('p' || p."Id" || 'd'), p."Description") end
where p."ClubId" = 1;

delete from "MembershipOrders" o using "Seasons" s
where s."Id" = o."SeasonId" and s."ClubId" = 1 and current_date between s."Period_From" and s."Period_To";

-- Every third active member has an adult fee order, in a realistic mix of states.
create temp table orders as
select nextval('"MembershipOrders_Id_Sequence"') as id, ms."MemberId", ms."SeasonId", a.rn
from active_members a
join "MemberSeasons" ms on ms."MemberId" = a."MemberId"
join "Seasons" s on s."Id" = ms."SeasonId" and current_date between s."Period_From" and s."Period_To"
where a.rn % 3 = 0 and a."MemberId" <> 3340;

insert into "MembershipOrders"("Id", "ClubId", "SeasonId", "MembershipPlanId", "PlanName", "AmountCents", "Currency", "PayerMemberId",
                               "Status", "PaymentMethod", "PaidAt", "MarkedPaidByMemberId", "Metadata_Created")
select o.id, 1, o."SeasonId", p."Id", p."Name", p."PriceCents", p."Currency", o."MemberId",
       case when o.rn % 7 = 0 then 1 when o.rn % 11 = 0 then 2 else 3 end,
       case when o.rn % 7 = 0 then null when o.rn % 5 = 0 then 1 when o.rn % 13 = 0 then 2 else 0 end,
       case when o.rn % 7 = 0 or o.rn % 11 = 0 then null else now() - ((o.rn % 40) || ' days')::interval end,
       case when o.rn % 5 = 0 or o.rn % 13 = 0 then 3320 else null end,
       now() - ((o.rn % 40 + 1) || ' days')::interval
from orders o
join "MembershipPlans" p on p."SeasonId" = o."SeasonId" and p."SortOrder" = 1;

insert into "MembershipOrderMembers"("Id", "MembershipOrderId", "MemberId")
select nextval('"MembershipOrderMembers_Id_Sequence"'), o.id, o."MemberId" from orders o;

-- Court sales: price rules and bought court time of members who are not enrolled ---

insert into t values
 ('r1', 'Tagestarif', 'Daytime'),
 ('r2', 'Abendtarif', 'Evening'),
 ('r3', 'Wochenende', 'Weekend');

delete from "CourtPurchases" where "ClubId" = 1;
delete from "BookingCreditGrants" where "ClubId" = 1;
delete from "CourtPriceRules" where "ClubId" = 1;
delete from "CourtSaleSettings" where "ClubId" = 1;

insert into "CourtSaleSettings"("Id", "ClubId", "IsEnabled", "PlayModeId", "TimeZoneId", "MaxBookingMinutes", "MaxDaysInAdvance", "MaxUpcomingBookings")
select nextval('"CourtSaleSettings_Id_Sequence"'), 1, true, p."Id", 'Europe/Vienna', 120, 14, 2
from "PlayModes" p where p."ClubId" = 1 and not p."CanOverbook" and p."FixedDuration" is null order by p."Id" limit 1;

-- Weekdays as .NET DayOfWeek (Sunday = 0); no courts = all courts.
insert into "CourtPriceRules"("Id", "ClubId", "CourtIds", "Weekdays", "From", "To", "PricePerHourCents", "Currency", "Name")
values
 (nextval('"CourtPriceRules_Id_Sequence"'), 1, '{}', '{1,2,3,4,5}', '08:00', '17:00', 1800, 'eur', pg_temp.tx('r1')),
 (nextval('"CourtPriceRules_Id_Sequence"'), 1, '{}', '{1,2,3,4,5}', '17:00', '22:00', 2400, 'eur', pg_temp.tx('r2')),
 (nextval('"CourtPriceRules_Id_Sequence"'), 1, '{}', '{6,0}', '08:00', '20:00', 2000, 'eur', pg_temp.tx('r3'));

-- Members of club 1 who are not enrolled in the running season and no parents: they buy court time.
create temp table buyers as
select m."Id", m."UserId", row_number() over (order by (m."Id" * 2654435761::bigint) % 1000003) as rn
from "Members" m join "AspNetUsers" u on u."Id" = m."UserId"
where m."ClubId" = 1 and m."MemberType" = 0 and m."Id" not in (3320, 3340)
  and not exists (select 1 from "MemberSeasons" ms join "Seasons" s on s."Id" = ms."SeasonId"
                  where ms."MemberId" = m."Id" and s."ClubId" = 1 and current_date between s."Period_From" and s."Period_To")
  and not exists (select 1 from "FamilyMembers" f where f."MemberId" = m."Id");

-- The first one is logged in for the screenshot of the buy dialog and has nothing bought yet.
update "AspNetUsers" u set "Email" = 'buyer@example.test', "NormalizedEmail" = 'BUYER@EXAMPLE.TEST'
from buyers b where b.rn = 1 and u."Id" = b."UserId";

-- Fourteen sales over the coming two weeks and a few of this month, each on a free court.
do $$
declare
    b record;
    day date;
    starts timestamptz;
    minutes int;
    cents int;
    court int;
    booking int;
    status int;
    method int;
    credit boolean;
begin
    for b in select * from buyers where rn between 2 and 19 order by rn loop
        day := current_date + ((b.rn * 5) % 19 - 4)::int;
        minutes := case when b.rn % 3 = 0 then 90 else 60 end;
        court := null;
        for h in 0..7 loop
            starts := (day + time '09:00' + ((b.rn + h * 3) % 8) * interval '1 hour') at time zone 'Europe/Vienna';
            select c."Id" into court from "Courts" c
            where c."ClubId" = 1
              and not exists (select 1 from "Bookings" x where x."CourtId" = c."Id"
                              and x."Interval_From" < starts + minutes * interval '1 minute' and starts < x."Interval_To")
            order by (c."Id" + b.rn) % 7 limit 1;
            exit when court is not null;
        end loop;
        continue when court is null;

        cents := minutes * case when extract(isodow from day) >= 6 then 2000 else 1800 end / 60;
        credit := b.rn % 5 = 0;
        status := case when b.rn = 7 then 0 when b.rn = 13 then 6 else 2 end;
        method := case when credit and minutes = 60 then 1 else 0 end;

        booking := null;
        if status in (0, 2) then
            booking := nextval('"Bookings_Id_Sequence"');
            insert into "Bookings"("Id", "Interval_From", "Interval_To", "CourtId", "PlayModeId", "ClubId", "TimeZoneInfoId", "Metadata_CreationUserId")
            select booking, starts, starts + minutes * interval '1 minute', court, s."PlayModeId", 1, 'Europe/Vienna', b."UserId"
            from "CourtSaleSettings" s where s."ClubId" = 1;
            insert into "BookingPlayers"("BookingEntryId", "Id", "MemberId") values (booking, nextval('"BookingPlayers_Id_Sequence"'), b."Id");
        end if;

        if credit then
            insert into "BookingCreditGrants"("Id", "ClubId", "MemberId", "Count", "Reason", "GrantedByMemberId", "Metadata_Created")
            values (nextval('"BookingCreditGrants_Id_Sequence"'), 1, b."Id", 3, 1, 3320, now() - interval '20 days');
        end if;

        insert into "CourtPurchases"("Id", "ClubId", "MemberId", "BookingId", "CourtId", "Interval_From", "Interval_To", "PriceCents", "CreditUsed",
                                     "AmountDueCents", "Currency", "Status", "PaymentMethod", "HoldExpiresAt", "PaidAt", "CancelledAt", "Metadata_Created")
        values (nextval('"CourtPurchases_Id_Sequence"'), 1, b."Id", booking, court, starts, starts + minutes * interval '1 minute', cents, credit,
                case when credit then cents - (case when extract(isodow from day) >= 6 then 2000 else 1800 end) else cents end,
                'eur', status, method,
                case when status = 0 then now() + interval '25 minutes' end,
                case when status in (2, 6) then now() - (b.rn % 6) * interval '1 day' end,
                case when status = 6 then now() - interval '1 day' end,
                now() - (b.rn % 6) * interval '1 day' - interval '2 hours');
    end loop;
end $$;

commit;
