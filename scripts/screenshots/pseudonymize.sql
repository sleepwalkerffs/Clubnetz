-- Replaces every piece of personal data in a COPY of the development database
-- with invented values, so the copy can be used for screenshots and demos.
-- Run it only through New-ScreenshotDatabase.ps1, never against the dev database itself.
--
-- All users get the same password afterwards (the password of the template
-- account passed in as :template_email), so any member, admin or treasurer
-- can be logged in for a screenshot.

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

-- Users (local test accounts on @bookennis.test keep their email for logging in) --

create temp table template_hash as
select "PasswordHash" from "AspNetUsers" where "NormalizedEmail" = upper(:'template_email');

do $$ begin
    if (select count(*) from template_hash) <> 1 then
        raise exception 'Template account not found';
    end if;
end $$;

create temp table new_names as
select u."Id",
       case when u."Gender" = 1
            then (select n from first_names_f where i = 1 + (u."Id" * 7919) % 50)
            else (select n from first_names_m where i = 1 + (u."Id" * 7919) % 50) end as first_name,
       (select n from last_names where i = 1 + (u."Id" * 104729) % 60) as last_name
from "AspNetUsers" u;

-- Members of one family share the last name of the parent who owns their account.
update new_names c set last_name = p.last_name
from "AspNetUsers" u join new_names p on p."Id" = u."BelongsToUserId"
where c."Id" = u."Id";

update "AspNetUsers" u set
    "FirstName" = n.first_name,
    "LastName" = n.last_name,
    "FullName" = n.first_name || ' ' || n.last_name,
    "Email" = case when u."Email" is null or u."Email" like '%@bookennis.test' then u."Email"
                   else lower(translate(n.first_name || '.' || n.last_name, 'äöüß', 'aous')) || '.' || u."Id" || '@example.test' end,
    "UserName" = case when u."UserName" is null or u."Email" like '%@bookennis.test' then u."UserName"
                      else lower(translate(n.first_name || '.' || n.last_name, 'äöüß', 'aous')) || '.' || u."Id" || '@example.test' end,
    "Street" = case when u."Street" is null then null
                    else (select s.n from streets s where s.i = 1 + (u."Id" * 31) % 16) || ' ' || (1 + u."Id" % 48) end,
    "City" = case when u."City" is null then null else (select c.n from cities c where c.i = 1 + u."Id" % 6) end,
    "ZipCode" = case when u."ZipCode" is null then null else (select c.zip from cities c where c.i = 1 + u."Id" % 6) end,
    "PhoneNumber" = case when u."PhoneNumber" is null then null else '+43 660 ' || lpad(((u."Id" * 7331) % 10000000)::text, 7, '0') end,
    "Birthday" = case when u."Birthday" is null then null else u."Birthday" + ((u."Id" * 37) % 241 - 120) end,
    "PasswordHash" = (select "PasswordHash" from template_hash),
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
delete from "UserProfilePictures";
delete from "PushSubscriptions";

-- Free texts that may name people -------------------------------------------------

update "Bookings" set "Comment" = null where "Comment" is not null;
update "RecurringBookingSeries" set "Comment" = null where "Comment" is not null;
update "ClubEventRegistrations" set "Comment" = null where "Comment" is not null;
update "MembershipOrders" set "Note" = null where "Note" is not null;
update "SubscriptionPlanParticipants" p set "Name" = (select n from first_names_m where i = 1 + (p."Id" * 7919) % 50)
    || ' ' || (select n from last_names where i = 1 + (p."Id" * 104729) % 60);

-- The home club gets an invented name ---------------------------------------------

update "Clubs" set
    "Name" = 'TC Sonnental',
    "ReplyToEmail" = case when "ReplyToEmail" is null then null else 'vorstand@tc-sonnental.example' end,
    "WebsiteUrl" = case when "WebsiteUrl" is null then null else 'https://tc-sonnental.example' end
where "Id" = 1;

-- Courts may be named after people of the club.
update "Courts" set "Name" = 'Center Court', "Alias" = case when "Alias" is null then null else 'CC' end
where "ClubId" = 1 and "Name" !~ '^[0-9]+$';

commit;

-- Old background jobs carry real email addresses in their arguments. Hangfire
-- recreates its schema on the next start.
drop schema if exists hangfire cascade;
