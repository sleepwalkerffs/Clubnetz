#!/usr/bin/env bash
# Fills the UI check database with invented test data. start-app.sh runs it, it can run again any time.
#
# Club 1 ("TC Vorderland", 7 courts, play modes "Einzel" and "Doppel", from the seed migrations) gets:
#   anna.admin@example.test    club admin and member
#   ben.member@example.test    member
#   clara.member@example.test  member
#   david.member@example.test  member
# All of them are active members of the current season and have the password below.
# This is a throwaway password for a database that only lives in a CI job, never use it anywhere else.

set -euo pipefail

PORT="${UI_PORT:-5080}"
DB="${UI_DB:-clubnetz_ui}"
PASSWORD="UiCheck-2026!"
export PGPASSWORD=developer

sql() { psql -h 127.0.0.1 -U postgres -d "$DB" -v ON_ERROR_STOP=1 -qtA "$@"; }

register() {
    local first="$1" last="$2" email="$3" gender="$4"
    if [[ "$(sql -c "select count(*) from \"AspNetUsers\" where \"NormalizedEmail\" = upper('$email')")" != "0" ]]; then
        return
    fi
    # Registering sends a confirmation email. There is no mail server in CI, the email job just fails.
    local status
    status=$(curl -s -o /tmp/clubnetz-ui-register.txt -w '%{http_code}' -X POST "http://localhost:$PORT/api/Account/Register" \
        -H 'Content-Type: application/json' \
        -d "{\"firstName\":\"$first\",\"lastName\":\"$last\",\"birthday\":\"1990-05-17\",\"email\":\"$email\",\"userName\":\"$email\",\"password\":\"$PASSWORD\",\"gender\":\"$gender\",\"street\":\"Teststraße 1\",\"city\":\"Wien\",\"zipCode\":\"1010\",\"country\":\"Austria\",\"acceptPrivacyPolicy\":true}")
    if [[ "$status" != 2* ]]; then
        echo "Registering $email failed with HTTP $status:"
        cat /tmp/clubnetz-ui-register.txt
        exit 1
    fi
}

register Anna Admin anna.admin@example.test Female
register Ben Berger ben.member@example.test Male
register Clara Huber clara.member@example.test Female
register David Gruber david.member@example.test Male

# Ids far above what the HiLo sequences hand out in a short CI run, so the app never collides with them.
sql <<'SQL'
update "AspNetUsers"
set "EmailConfirmed" = true, "ProfileCompletionBannerDismissedAt" = coalesce("ProfileCompletionBannerDismissedAt", now())
where "Email" like '%@example.test';

-- A season around today, unless club 1 already has one
insert into "Seasons" ("Id", "ClubId", "Period_From", "Period_To")
select 900001, 1, date_trunc('year', now())::date, (date_trunc('year', now()) + interval '1 year - 1 day')::date
where not exists (
    select 1 from "Seasons" where "ClubId" = 1 and "Period_From" <= current_date and "Period_To" >= current_date);

-- Members: roles are 0 = User, 2 = Admin (Bookennis.Domain.Members.MemberRole), member type 0 = club member
insert into "Members" ("Id", "ClubId", "UserId", "MemberType", "IsAllowedToBook", "UserRoles")
select 900000 + u."Id", 1, u."Id", 0, true,
       case when u."Email" = 'anna.admin@example.test' then array[0, 2] else array[0] end
from "AspNetUsers" u
where u."Email" like '%@example.test'
  and not exists (select 1 from "Members" m where m."UserId" = u."Id" and m."ClubId" = 1);

insert into "MemberSeasons" ("Id", "MemberId", "SeasonId", "LeaderboardOptOut")
select 900000 + m."Id", m."Id", s."Id", false
from "Members" m
join "AspNetUsers" u on u."Id" = m."UserId" and u."Email" like '%@example.test'
cross join lateral (
    select "Id" from "Seasons"
    where "ClubId" = 1 and "Period_From" <= current_date and "Period_To" >= current_date
    order by "Period_From" desc limit 1) s
where m."ClubId" = 1
  and not exists (select 1 from "MemberSeasons" ms where ms."MemberId" = m."Id" and ms."SeasonId" = s."Id");
SQL

echo "Test data ready: anna.admin@example.test (admin), ben.member@, clara.member@, david.member@example.test, password $PASSWORD"
