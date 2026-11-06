-- Invented demo content for the features added after the first landing page screenshots:
-- the club's books (two fiscal years, entries, a bank import, suggestions, invoices),
-- the clubhouse (price list, open and paid tabs) and play requests with player groups.
-- Texts are German or English (:lang = de | en). pseudonymize.sql empties these tables
-- first, so nothing here is mixed with real data.
-- Run it only through New-ScreenshotDatabase.ps1, after demo-content.sql.

\set ON_ERROR_STOP on

begin;

select set_config('screens.lang', :'lang', false) as lang \gset

create function pg_temp.l(de text, en text) returns text language sql as
$$ select case when current_setting('screens.lang') = 'en' then en else de end $$;

-- The admin of the template account books, hosts and asks for players.
create temp table me as select 3320 as id;

-- Active members of the current season, in a stable order, to pick counterparts from.
create temp table people as
select row_number() over (order by m."Id") as rn, m."Id" as id, u."FullName" as name
from "Members" m
join "AspNetUsers" u on u."Id" = m."UserId"
join "MemberSeasons" ms on ms."MemberId" = m."Id"
join "Seasons" s on s."Id" = ms."SeasonId" and s."ClubId" = 1 and current_date between s."Period_From" and s."Period_To"
where m."ClubId" = 1 and m."Id" not in (3320, 3340) and u."Email" like '%@example.test';

-- Accounting: settings, money accounts, categories, cost centers ------------------

create temp table acc(k text primary key, id int);
insert into acc values
 ('bank', nextval('"MoneyAccounts_Id_Sequence"')),
 ('cash', nextval('"MoneyAccounts_Id_Sequence"')),
 ('stripe', nextval('"MoneyAccounts_Id_Sequence"'));

insert into "MoneyAccounts"("Id", "ClubId", "Kind", "Name", "Iban", "SortOrder", "IsArchived", "Metadata_Created") values
 ((select id from acc where k = 'bank'), 1, 0, pg_temp.l('Girokonto', 'Bank account'), 'AT611904300234573201', 0, false, now()),
 ((select id from acc where k = 'cash'), 1, 1, pg_temp.l('Kassa', 'Cash box'), null, 1, false, now()),
 ((select id from acc where k = 'stripe'), 1, 2, 'Stripe', null, 2, false, now());

insert into "AccountingSettings"("Id", "ClubId", "FiscalYearStartMonth", "GermanTexts", "BankMoneyAccountId", "CashMoneyAccountId",
                                 "CardMoneyAccountId", "OnlineMoneyAccountId", "Metadata_Created")
values (nextval('"AccountingSettings_Id_Sequence"'), 1, 1, :'lang' = 'de', (select id from acc where k = 'bank'),
        (select id from acc where k = 'cash'), (select id from acc where k = 'bank'), (select id from acc where k = 'stripe'), now());

-- The categories of AccountingTemplate.cs.
create temp table cat(num text primary key, kind int, de text, en text, sphere int, id int default nextval('"AccountingCategories_Id_Sequence"'));
insert into cat(num, kind, de, en, sphere) values
 ('4000', 0, 'Mitgliedsbeiträge', 'Membership fees', 0),
 ('4010', 0, 'Beitrittsgebühren', 'Joining fees', 0),
 ('4100', 0, 'Platzgebühren und Gastspieler', 'Court fees and guest players', 2),
 ('4110', 0, 'Training und Kurse', 'Coaching and courses', 0),
 ('4200', 0, 'Turniere und Nenngelder', 'Tournaments and entry fees', 0),
 ('4300', 0, 'Kantine und Clubhaus', 'Canteen and clubhouse', 2),
 ('4310', 0, 'Vereinsfeste', 'Club parties', 2),
 ('4400', 0, 'Sponsoring und Werbung', 'Sponsoring and advertising', 3),
 ('4500', 0, 'Subventionen und Förderungen', 'Subsidies and grants', 0),
 ('4510', 0, 'Spenden', 'Donations', 0),
 ('4600', 0, 'Zinserträge', 'Interest', 1),
 ('4610', 0, 'Miet- und Pachteinnahmen', 'Rent received', 1),
 ('4900', 0, 'Sonstige Einnahmen', 'Other income', 0),
 ('5000', 1, 'Verbandsbeiträge (ÖTV, Landesverband)', 'Association fees', 0),
 ('5010', 1, 'Meisterschafts- und Nenngebühren', 'League and entry fees', 0),
 ('5100', 1, 'Platzpflege und Material', 'Court maintenance and material', 0),
 ('5110', 1, 'Bälle und Sportgeräte', 'Balls and equipment', 0),
 ('5200', 1, 'Trainerhonorare', 'Coaches', 0),
 ('5300', 1, 'Energie (Strom, Wasser, Gas)', 'Energy (electricity, water, gas)', 0),
 ('5310', 1, 'Pacht und Miete', 'Rent and lease', 0),
 ('5400', 1, 'Instandhaltung und Reparaturen', 'Repairs and upkeep', 0),
 ('5500', 1, 'Versicherungen', 'Insurance', 0),
 ('5600', 1, 'Kantine und Clubhaus Wareneinkauf', 'Canteen and clubhouse purchases', 2),
 ('5610', 1, 'Vereinsfeste Aufwand', 'Club party costs', 2),
 ('5700', 1, 'Büro, Porto und Software', 'Office, postage and software', 0),
 ('5710', 1, 'Bankspesen und Zahlungsgebühren', 'Bank and payment fees', 0),
 ('5800', 1, 'Jugendarbeit', 'Youth work', 0),
 ('5900', 1, 'Sonstige Ausgaben', 'Other expenses', 0);

insert into "AccountingCategories"("Id", "ClubId", "Kind", "Number", "Name", "TaxSphere", "IsArchived", "Metadata_Created")
select id, 1, kind, num, pg_temp.l(de, en), sphere, false, now() from cat;

create temp table cc(k text primary key, id int default nextval('"CostCenters_Id_Sequence"'), de text, en text);
insert into cc(k, de, en) values ('youth', 'Jugend', 'Youth'), ('teams', 'Mannschaften', 'Teams'), ('club', 'Clubhaus', 'Clubhouse');
insert into "CostCenters"("Id", "ClubId", "Name", "IsArchived", "Metadata_Created")
select id, 1, pg_temp.l(de, en), false, now() from cc;

-- Fiscal years: last year closed and audited, this year open ------------------------

create temp table fy(y int primary key, id int);
insert into fy values
 (extract(year from current_date)::int - 1, nextval('"FiscalYears_Id_Sequence"')),
 (extract(year from current_date)::int, nextval('"FiscalYears_Id_Sequence"'));

-- What happens in a year, by day of the year. Amounts in cents; the year before uses about 92 % of them.
create temp table plan(doy int, de text, en text, acct text, num text, cc text, cents int, party text);
insert into plan values
 (12, 'Verbandsbeitrag ÖTV und Landesverband', 'Association fees ÖTV and regional association', 'bank', '5000', null, 186000, 'Österreichischer Tennisverband'),
 (20, 'Versicherung Haftpflicht und Unfall', 'Liability and accident insurance', 'bank', '5500', null, 94850, 'Sonnentaler Versicherung'),
 (31, 'Hallenmiete Wintertraining Jugend', 'Indoor court rent for the youth winter training', 'bank', '5310', 'youth', 132000, 'Tennishalle Lindenau'),
 (45, 'Subvention der Gemeinde', 'Grant from the municipality', 'bank', '4500', null, 250000, pg_temp.l('Gemeinde Sonnental', 'Municipality of Sonnental')),
 (60, 'Ziegelmehl und Linienbänder', 'Clay and court lines', 'bank', '5100', null, 412300, 'Platzbau Wieser'),
 (66, 'Frühjahrsinstandsetzung der Plätze', 'Spring court preparation', 'bank', '5100', null, 385000, 'Platzbau Wieser'),
 (75, 'Sponsoring Autohaus Berger', 'Sponsoring Autohaus Berger', 'bank', '4400', null, 300000, 'Autohaus Berger'),
 (82, 'Tennisbälle für die Saison', 'Tennis balls for the season', 'bank', '5110', 'teams', 168540, 'Sportfachhandel Huber'),
 (92, 'Mitgliedsbeiträge per Überweisung', 'Membership fees by bank transfer', 'bank', '4000', null, 1245000, null),
 (95, 'Mitgliedsbeiträge online', 'Membership fees online', 'stripe', '4000', null, 2187500, null),
 (100, 'Nenngeld Mannschaftsmeisterschaft', 'Team league entry fees', 'bank', '5010', 'teams', 64000, 'Landestennisverband'),
 (110, 'Mitgliedsbeiträge bar', 'Membership fees in cash', 'cash', '4000', null, 96000, null),
 (118, 'Getränkelieferung Clubhaus', 'Drinks delivery for the clubhouse', 'bank', '5600', 'club', 138760, 'Getränke Fuchs'),
 (125, 'Trainerhonorar April', 'Coach fee April', 'bank', '5200', 'youth', 182000, 'Tennisschule Moser'),
 (140, 'Strom Flutlicht und Clubhaus', 'Electricity for floodlights and clubhouse', 'bank', '5300', null, 61240, 'Energie Sonnental'),
 (150, 'Platzgebühren Gastspieler Mai', 'Guest player court fees May', 'stripe', '4100', null, 54000, null),
 (156, 'Trainerhonorar Mai', 'Coach fee May', 'bank', '5200', 'youth', 182000, 'Tennisschule Moser'),
 (163, 'Wasser Bewässerung', 'Water for the irrigation', 'bank', '5300', null, 38420, 'Energie Sonnental'),
 (170, 'Nenngelder Clubturnier', 'Club tournament entry fees', 'cash', '4200', null, 72000, null),
 (178, 'Sommercamp Jugend', 'Youth summer camp', 'bank', '4110', 'youth', 168000, null),
 (182, 'Reparatur Bewässerungsanlage', 'Repair of the irrigation system', 'bank', '5400', null, 74900, 'Installateur Egger'),
 (190, 'Platzgebühren Gastspieler Juni', 'Guest player court fees June', 'stripe', '4100', null, 78000, null),
 (196, 'Kantine Tageslosungen Juni', 'Canteen takings June', 'cash', '4300', 'club', 214560, null),
 (204, 'Getränkelieferung Clubhaus', 'Drinks delivery for the clubhouse', 'bank', '5600', 'club', 152330, 'Getränke Fuchs'),
 (214, 'Sommerfest Einnahmen', 'Summer party takings', 'cash', '4310', null, 186000, null),
 (215, 'Sommerfest Grillgut und Musik', 'Summer party food and music', 'bank', '5610', null, 98450, 'Metzgerei Gruber'),
 (230, 'Platzgebühren Gastspieler August', 'Guest player court fees August', 'stripe', '4100', null, 96000, null),
 (240, 'Spende Raiffeisenbank', 'Donation from the local bank', 'bank', '4510', 'youth', 50000, 'Raiffeisenbank Sonnental'),
 (248, 'Trainerhonorar August', 'Coach fee August', 'bank', '5200', 'youth', 182000, 'Tennisschule Moser'),
 (258, 'Website und Vereinssoftware', 'Website and club software', 'bank', '5700', null, 18900, 'Webhosting Lechner'),
 (265, 'Kantine Tageslosungen September', 'Canteen takings September', 'cash', '4300', 'club', 168230, null),
 (272, 'Stripe Gebühren', 'Stripe fees', 'stripe', '5710', null, 4386, 'Stripe'),
 (280, 'Strom Flutlicht und Clubhaus', 'Electricity for floodlights and clubhouse', 'bank', '5300', null, 58710, 'Energie Sonnental'),
 (290, 'Platzgebühren Gastspieler Oktober', 'Guest player court fees October', 'stripe', '4100', null, 42000, null),
 (296, 'Einwintern Netze und Planen', 'Winter covers and nets', 'bank', '5100', null, 46900, 'Platzbau Wieser'),
 (300, 'Bankspesen Quartal', 'Bank charges for the quarter', 'bank', '5710', null, 3870, 'Raiffeisenbank Sonnental'),
 (305, 'Zinsen Sparbuch', 'Interest on savings', 'bank', '4600', null, 2140, 'Raiffeisenbank Sonnental');

-- Entries of both years, numbered by date.
create temp table ent as
select nextval('"AccountingEntries_Id_Sequence"') as id, f.id as fy_id, f.y,
       make_date(f.y, 1, 1) + (p.doy - 1) as d,
       pg_temp.l(p.de, p.en) as text, p.acct, p.num, p.cc, p.party,
       case when f.y = extract(year from current_date) then p.cents else round(p.cents * 0.92)::int end as cents
from plan p cross join fy f
where make_date(f.y, 1, 1) + (p.doy - 1) <= current_date - 2;

-- Stripe pays out to the bank account twice a season, and the cash box is emptied into the bank once.
insert into ent
select nextval('"AccountingEntries_Id_Sequence"'), f.id, f.y, make_date(f.y, 1, 1) + (x.doy - 1), pg_temp.l(x.de, x.en),
       x.acct, null, null, null, x.cents
from fy f cross join (values
    (120, 1800000, 'stripe>bank', 'Auszahlung Stripe', 'Stripe payout'),
    (275, 300000, 'stripe>bank', 'Auszahlung Stripe', 'Stripe payout'),
    (220, 450000, 'cash>bank', 'Einzahlung Kassa auf das Girokonto', 'Cash paid into the bank account')) x(doy, cents, acct, de, en)
where make_date(f.y, 1, 1) + (x.doy - 1) <= current_date - 2;

insert into "FiscalYears"("Id", "ClubId", "StartDate", "EndDate", "Status", "ClosedAt", "ClosedByMemberId", "NextEntryNumber",
                          "OpeningBalancesCarried", "AuditStatus", "AuditedAt", "AuditedByMemberId", "AuditComment", "Metadata_Created")
select f.id, 1, make_date(f.y, 1, 1), make_date(f.y, 12, 31),
       case when f.y < extract(year from current_date) then 1 else 0 end,
       case when f.y < extract(year from current_date) then make_date(f.y + 1, 1, 24) + time '19:00' end,
       case when f.y < extract(year from current_date) then (select id from me) end,
       1 + (select count(*) from ent e where e.fy_id = f.id),
       f.y = extract(year from current_date),
       case when f.y < extract(year from current_date) then 1 else 0 end,
       case when f.y < extract(year from current_date) then make_date(f.y + 1, 2, 10) + time '18:30' end,
       case when f.y < extract(year from current_date) then (select id from people where rn = 7) end,
       null, now()
from fy f;

insert into "AccountingEntries"("Id", "ClubId", "FiscalYearId", "Number", "Date", "Text", "Kind", "MoneyAccountId", "ToMoneyAccountId",
                                "TransferAmountCents", "CounterpartyName", "Status", "Metadata_Created")
select e.id, 1, e.fy_id, row_number() over (partition by e.fy_id order by e.d, e.id), e.d, e.text,
       case when e.acct like '%>%' then 1 else 0 end,
       (select id from acc where k = split_part(e.acct, '>', 1)),
       case when e.acct like '%>%' then (select id from acc where k = split_part(e.acct, '>', 2)) end,
       case when e.acct like '%>%' then e.cents end,
       e.party, 0, e.d + time '10:00'
from ent e;

insert into "AccountingEntryLines"("AccountingEntryId", "Position", "CategoryId", "CostCenterId", "AmountCents")
select e.id, 0, c.id, (select id from cc where k = e.cc), e.cents
from ent e join cat c on c.num = e.num;

-- Opening balances: invented for last year, carried into this year from last year's closing balance.
insert into "FiscalYearOpeningBalances"("FiscalYearId", "MoneyAccountId", "AmountCents", "IsCarried")
select (select id from fy order by y limit 1), a.id, case a.k when 'bank' then 1650480 when 'cash' then 28540 else 0 end, false
from acc a;

insert into "FiscalYearOpeningBalances"("FiscalYearId", "MoneyAccountId", "AmountCents", "IsCarried")
select (select id from fy order by y desc limit 1), a.id,
       (select o."AmountCents" from "FiscalYearOpeningBalances" o where o."FiscalYearId" = (select id from fy order by y limit 1) and o."MoneyAccountId" = a.id)
       + coalesce((select sum(case when c.kind = 0 then e.cents else -e.cents end) from ent e join cat c on c.num = e.num
                   where e.y = (select min(y) from fy) and e.acct = a.k), 0)
       + coalesce((select sum(case when split_part(e.acct, '>', 2) = a.k then e.cents else -e.cents end) from ent e
                   where e.y = (select min(y) from fy) and e.acct like '%>%' and a.k in (split_part(e.acct, '>', 1), split_part(e.acct, '>', 2))), 0),
       true
from acc a;



-- Automatic entries: fees and court sales are suggested, Stripe fees and payouts booked right away.
insert into "AccountingSourceSettings"("Id", "ClubId", "Source", "Mode", "Since", "CategoryId", "Metadata_Created") values
 (nextval('"AccountingSourceSettings_Id_Sequence"'), 1, 0, 1, make_date(extract(year from current_date)::int, 1, 1), (select id from cat where num = '4000'), now()),
 (nextval('"AccountingSourceSettings_Id_Sequence"'), 1, 1, 1, make_date(extract(year from current_date)::int, 1, 1), (select id from cat where num = '4100'), now()),
 (nextval('"AccountingSourceSettings_Id_Sequence"'), 1, 2, 0, make_date(extract(year from current_date)::int, 1, 1), (select id from cat where num = '4300'), now()),
 (nextval('"AccountingSourceSettings_Id_Sequence"'), 1, 3, 2, make_date(extract(year from current_date)::int, 1, 1), (select id from cat where num = '5710'), now());

insert into "AccountingProposals"("Id", "ClubId", "Source", "SourceKey", "Date", "Text", "Kind", "MoneyAccountId", "CategoryId", "AmountCents",
                                  "CounterpartyMemberId", "CounterpartyName", "Status", "CreatedAt", "Metadata_Created")
select nextval('"AccountingProposals_Id_Sequence"'), 1, x.src, 'demo-' || x.n, current_date - x.ago,
       case x.src when 0 then pg_temp.l('Mitgliedsbeitrag ', 'Membership fee ') || p.name else pg_temp.l('Platzkauf ', 'Court purchase ') || p.name end,
       0, (select id from acc where k = 'stripe'), (select id from cat where num = case x.src when 0 then '4000' else '4100' end),
       x.cents, p.id, p.name, 0, now() - (x.ago || ' days')::interval, now()
from (values (1, 0, 0, 22000), (2, 0, 1, 39000), (3, 1, 1, 2400), (4, 0, 2, 22000), (5, 1, 3, 3600)) x(n, src, ago, cents)
join people p on p.rn = 10 + x.n * 3;

-- A bank statement of the last weeks: most lines matched, a few still open, one rule.
create temp table imp as select nextval('"BankStatementImports_Id_Sequence"') as id;
insert into "BankStatementImports"("Id", "ClubId", "MoneyAccountId", "FileName", "Format", "FromDate", "ToDate", "ImportedCount", "SkippedCount",
                                   "ImportedByMemberId", "ImportedAt", "Metadata_Created")
select id, 1, (select id from acc where k = 'bank'), 'kontoauszug-oktober.xml', 0, current_date - 40, current_date - 3, 9, 0, (select id from me), now() - interval '2 days', now()
from imp;

insert into "BankTransactions"("Id", "ClubId", "MoneyAccountId", "ImportId", "BookingDate", "ValueDate", "AmountCents", "CounterpartyName",
                               "CounterpartyIban", "Reference", "Fingerprint", "Status", "EntryId", "Metadata_Created")
select nextval('"BankTransactions_Id_Sequence"'), 1, (select id from acc where k = 'bank'), (select id from imp), b.d, b.d, b.cents, b.party,
       b.iban, b.ref, 'demo-' || b.n, b.status,
       (select e.id from ent e where e.y = extract(year from current_date) and e.acct = 'bank' and e.d = b.d and abs(e.cents) = abs(b.cents) limit 1),
       now()
from (
    select n, d, cents, party, iban, ref, case when exists (select 1 from ent e where e.y = extract(year from current_date) and e.acct = 'bank' and e.d = x.d and e.cents = abs(x.cents)) then 1 else 0 end as status
    from (values
        (1, current_date - 3, -2890, 'Raiffeisenbank Sonnental', null, pg_temp.l('Kontoführung', 'Account fees')),
        (2, current_date - 5, 18000, 'Lena Brunner', 'AT021400000123456789', pg_temp.l('Kurs Damentraining Herbst', 'Ladies'' autumn course')),
        (3, current_date - 8, -64500, 'Getränke Fuchs', 'AT483500000000123456', pg_temp.l('Rechnung 2026/1182', 'Invoice 2026/1182')),
        (4, current_date - 12, 22000, 'Markus Hofer', 'AT131200000098765432', pg_temp.l('Mitgliedsbeitrag 2026 Hofer', 'Membership fee 2026 Hofer')),
        (5, current_date - 20, -38420, 'Energie Sonnental', 'AT411100000237571500', pg_temp.l('Abschlag Strom', 'Electricity instalment'))
    ) x(n, d, cents, party, iban, ref)
    union all
    select 10 + row_number() over (order by e.d), e.d, case when c.kind = 0 then e.cents else -e.cents end, e.party, null, e.text, 1
    from ent e join cat c on c.num = e.num
    where e.y = extract(year from current_date) and e.acct = 'bank' and e.d >= current_date - 40
) b;

update "BankTransactions" set "Status" = 0, "EntryId" = null where "EntryId" is null;

insert into "BankRules"("Id", "ClubId", "MatchText", "Direction", "CategoryId", "Text", "MoneyAccountId", "Metadata_Created") values
 (nextval('"BankRules_Id_Sequence"'), 1, 'Getränke Fuchs', 2, (select id from cat where num = '5600'), pg_temp.l('Getränkelieferung Clubhaus', 'Drinks delivery for the clubhouse'), null, now()),
 (nextval('"BankRules_Id_Sequence"'), 1, 'Kontoführung', 2, (select id from cat where num = '5710'), pg_temp.l('Bankspesen', 'Bank charges'), null, now());

-- Invoices --------------------------------------------------------------------------

insert into "InvoiceSettings"("Id", "ClubId", "Address", "ZvrNumber", "Contact", "MoneyAccountId", "Bic", "NumberPrefix", "PaymentTermDays", "Footer", "Metadata_Created")
values (nextval('"InvoiceSettings_Id_Sequence"'), 1, E'TC Sonnental\nTennisweg 1\n6830 Sonnental', '123456789', 'kassier@tc-sonnental.example',
        (select id from acc where k = 'bank'), 'RZOOAT2L', 'RE', 14,
        pg_temp.l('Gemäß § 6 Abs. 1 Z 27 UStG umsatzsteuerbefreit (Kleinunternehmer).', 'Exempt from VAT under § 6 (1) 27 UStG (small business).'), now());

create temp table inv(n int, id int default nextval('"Invoices_Id_Sequence"'), status int, seq int, party text, addr text, email text, subject text,
                      d date, total int, sent boolean, reminders int, paid date, line text, qty numeric, num text);
insert into inv(n, status, seq, party, addr, email, subject, d, total, sent, reminders, paid, line, qty, num) values
 (1, 2, 1, 'Autohaus Berger GmbH', E'Hauptstraße 12\n6830 Sonnental', 'office@autohaus-berger.example', pg_temp.l('Bandenwerbung Saison', 'Court-side banner for the season'),
  current_date - 230, 300000, true, 0, current_date - 222, pg_temp.l('Bandenwerbung Platz 1, Saison', 'Court-side banner on court 1, season'), 1, '4400'),
 (2, 1, 2, 'Bäckerei Steiner', E'Kirchplatz 3\n6832 Obersonnental', 'steiner@baeckerei.example', pg_temp.l('Werbung Clubzeitung', 'Advert in the club magazine'),
  current_date - 34, 45000, true, 1, null, pg_temp.l('Halbseitige Anzeige, zwei Ausgaben', 'Half-page advert, two issues'), 2, '4400'),
 (3, 1, 3, 'TC Lindenau', E'Sportplatzweg 8\n6834 Lindenau', 'vorstand@tc-lindenau.example', pg_temp.l('Platzmiete Turnier', 'Court rent for a tournament'),
  current_date - 6, 72000, true, 0, null, pg_temp.l('Platzmiete 4 Plätze, 2 Tage', 'Rent of 4 courts for 2 days'), 8, '4610'),
 (4, 0, null, 'Fahrschule Wagner', E'Bahnhofstraße 21\n6830 Sonnental', 'info@fahrschule-wagner.example', pg_temp.l('Sponsoring Jugendturnier', 'Sponsoring of the youth tournament'),
  current_date, 80000, false, 0, null, pg_temp.l('Hauptsponsor Jugendturnier', 'Main sponsor of the youth tournament'), 1, '4400');

insert into "Invoices"("Id", "ClubId", "German", "Status", "Number", "NumberYear", "NumberSequence", "RecipientName", "RecipientAddress", "RecipientEmail",
                       "Subject", "InvoiceDate", "DueDate", "TotalCents", "IssuedAt", "SentAt", "SentTo", "ReminderCount", "LastReminderAt",
                       "PaidDate", "Metadata_Created")
select i.id, 1, :'lang' = 'de', i.status,
       case when i.seq is null then null else 'RE' || extract(year from current_date) || '-' || lpad(i.seq::text, 4, '0') end,
       case when i.seq is null then null else extract(year from current_date)::int end, i.seq,
       i.party, i.addr, i.email, i.subject, i.d, i.d + 14, i.total,
       case when i.status > 0 then i.d + time '09:00' end,
       case when i.sent then i.d + time '09:05' end, case when i.sent then i.email end,
       i.reminders, case when i.reminders > 0 then (current_date - 6) + time '09:00' end,
       i.paid, now()
from inv i;

insert into "InvoiceLines"("InvoiceId", "Position", "Text", "Quantity", "UnitPriceCents", "TotalCents", "CategoryId", "CostCenterId")
select i.id, 0, i.line, i.qty, (i.total / i.qty)::int, i.total, c.id, null from inv i join cat c on c.num = i.num;

-- Clubhouse: price list, open tabs of today, paid tabs of the last weeks -------------

create temp table item(n int primary key, id int default nextval('"ClubhouseItems_Id_Sequence"'), de text, en text, category int, cents int);
insert into item(n, de, en, category, cents) values
 (1, 'Bier 0,5 l', 'Beer 0.5 l', 0, 420), (2, 'Radler 0,5 l', 'Shandy 0.5 l', 0, 400), (3, 'Spritzer', 'Wine spritzer', 0, 350),
 (4, 'Mineralwasser', 'Sparkling water', 0, 250), (5, 'Almdudler', 'Lemonade', 0, 300), (6, 'Kaffee', 'Coffee', 0, 280),
 (7, 'Schinken-Käse-Toast', 'Ham and cheese toast', 1, 450), (8, 'Frankfurter mit Senf', 'Sausages with mustard', 1, 500), (9, 'Kuchen', 'Cake', 1, 350),
 (10, 'Dose Tennisbälle', 'Can of tennis balls', 2, 990);

insert into "ClubhouseItems"("Id", "ClubId", "Name", "Category", "PriceCents", "Currency", "IsAvailable", "SortOrder", "Metadata_Created")
select id, 1, pg_temp.l(de, en), category, cents, 'eur', true, n, now() from item;

create temp table tab(n int primary key, id int default nextval('"ClubhouseTabs_Id_Sequence"'), member int, guest text, status int, opened timestamptz, paid timestamptz, method int);
insert into tab(n, member, guest, status, opened, paid, method)
select x.n, (select id from people where rn = x.p), x.guest, x.status, x.opened, x.paid, x.method
from (values
    (1, 4, null, 0, now() - interval '35 minutes', null::timestamptz, null::int),
    (2, 9, null, 0, now() - interval '70 minutes', null, null),
    (3, null, pg_temp.l('Tisch 3', 'Table 3'), 0, now() - interval '50 minutes', null, null),
    (4, 15, null, 0, now() - interval '2 hours', null, null),
    (5, 21, null, 0, now() - interval '3 days', null, null),
    (6, null, pg_temp.l('Herr Gruber', 'Mr Gruber'), 0, now() - interval '20 minutes', null, null)
) x(n, p, guest, status, opened, paid, method);

-- Paid tabs: two to four a day over the last three weeks.
insert into tab(n, member, guest, status, opened, paid, method)
select 100 + g, (select id from people where rn = 3 + (g * 7) % 60), null, 1,
       date_trunc('day', now()) - ((g / 3) || ' days')::interval + time '17:30' + ((g % 3) || ' hours')::interval,
       date_trunc('day', now()) - ((g / 3) || ' days')::interval + time '19:45' + ((g % 3) || ' hours')::interval,
       case when g % 5 = 0 then 3 when g % 3 = 0 then 1 else 0 end
from generate_series(3, 62) g;

-- And a few paid a little while ago, so "paid today" isn't empty.
insert into tab(n, member, guest, status, opened, paid, method)
select 200 + g, (select id from people where rn = 40 + g), null, 1, now() - ((g + 3) || ' hours')::interval, now() - ((g + 1) || ' hours')::interval,
       case g when 0 then 3 when 1 then 1 else 0 end
from generate_series(0, 2) g;

insert into "ClubhouseTabs"("Id", "ClubId", "MemberId", "GuestName", "Status", "Currency", "OpenedAt", "OpenedByMemberId", "PaidAt", "PaymentMethod",
                            "PaidToMemberId", "Metadata_Created")
select id, 1, member, guest, status, 'eur', opened, (select id from me), paid, method, case when status = 1 then (select id from me) end, opened
from tab;

insert into "ClubhouseTabLines"("Id", "ClubhouseTabId", "ItemId", "Name", "Category", "UnitPriceCents", "Quantity", "AddedByMemberId", "AddedAt", "Metadata_Created")
select nextval('"ClubhouseTabLines_Id_Sequence"'), t.id, i.id, pg_temp.l(i.de, i.en), i.category, i.cents, q.qty, (select id from me), t.opened + interval '5 minutes', t.opened
from tab t
cross join lateral (values (1 + t.n % 2, 2 + t.n % 3), (4 + t.n % 3, 1), (7 + t.n % 3, case when t.n % 2 = 0 then 1 else 0 end)) q(item, qty)
join item i on i.n = q.item
where q.qty > 0;

-- Play requests and player groups ---------------------------------------------------

create temp table grp(n int primary key, id int default nextval('"PlayerGroups_Id_Sequence"'), de text, en text);
insert into grp(n, de, en) values (1, 'Donnerstagsrunde', 'Thursday crew'), (2, 'Herren 35', 'Men 35'), (3, 'Mixed-Partner', 'Mixed partners');
insert into "PlayerGroups"("Id", "ClubId", "OwnerMemberId", "Name", "Metadata_Created")
select id, 1, (select id from me), pg_temp.l(de, en), now() from grp;
insert into "PlayerGroupMembers"("Id", "PlayerGroupId", "MemberId", "Metadata_Created")
select nextval('"PlayerGroupMembers_Id_Sequence"'), g.id, p.id, now()
from grp g join people p on p.rn between g.n * 10 and g.n * 10 + 5;

-- Tomorrow evening: a doubles request of the admin with the Thursday crew, and a singles request to the admin.
-- starts is the wall clock time in Vienna.
create temp table req(n int primary key, id int default nextval('"PlayRequests_Id_Sequence"'), requester int, mode int, starts timestamp, players int, comment text);
insert into req(n, requester, mode, starts, players, comment) values
 (1, (select id from me), 2, (current_date + 1) + time '18:00', 4, pg_temp.l('Gemütliches Doppel nach der Arbeit, danach ein Getränk im Clubhaus?', 'A relaxed doubles after work, and a drink at the clubhouse afterwards?')),
 (2, (select id from people where rn = 33), 1, (current_date + 2) + time '09:00', 2, pg_temp.l('Wer hat Lust auf ein Einzel am Vormittag?', 'Anyone up for singles in the morning?'));

insert into "PlayRequests"("Id", "ClubId", "RequesterMemberId", "PlayModeId", "StartsAt", "EndsAt", "TimeZoneInfoId", "CourtIds", "PlayersNeeded",
                           "Comment", "ExpiresAt", "Status", "CreatedAt", "Metadata_Created")
select id, 1, requester, mode, (starts at time zone 'Europe/Vienna'), (starts at time zone 'Europe/Vienna') + interval '90 minutes',
       'Europe/Vienna', '{1,2,3}', players, comment, (starts at time zone 'Europe/Vienna') - interval '2 hours', 0, now() - interval '3 hours', now()
from req;

insert into "PlayRequestInvitees"("Id", "PlayRequestId", "MemberId", "Status", "RespondedAt", "Metadata_Created")
select nextval('"PlayRequestInvitees_Id_Sequence"'), (select id from req where n = 1), p.id,
       case p.rn when 10 then 1 when 11 then 1 when 12 then 2 else 0 end,
       case when p.rn in (10, 11, 12) then now() - interval '1 hour' end, now()
from people p where p.rn between 10 and 15;

insert into "PlayRequestInvitees"("Id", "PlayRequestId", "MemberId", "Status", "RespondedAt", "Metadata_Created")
select nextval('"PlayRequestInvitees_Id_Sequence"'), (select id from req where n = 2), m.id, 0, null, now()
from (select id from me union all select id from people where rn in (34, 35)) m;

commit;
