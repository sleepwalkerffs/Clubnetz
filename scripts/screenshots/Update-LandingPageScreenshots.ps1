<#
.SYNOPSIS
    Rebuilds the screenshot database in one language and captures the landing page screenshots.

.DESCRIPTION
    Runs New-ScreenshotDatabase.ps1 and Capture-Screenshots.ps1 for the screens shown on
    the landing page and writes them to website/screenshots/<language>. The app must
    already run against the screenshot database on -BaseUrl, e.g.:

        $env:ConnectionString = "Server=127.0.0.1;Port=8432;Database=clubnetz_screens;User Id=postgres;Password=password"
        dotnet run --project src/backend/Bookennis.Api --urls http://localhost:5082

    Logs in as two local test accounts: an admin who is also treasurer (-AdminEmail,
    the template account whose password everyone gets) and a plain member (-MemberEmail),
    plus buyer@example.test, a member without season that demo-content.sql picks to buy court time.
    -Only captures just the named screenshots, e.g. -Only court-sales, phone-court-buy.
#>
param(
    [Parameter(Mandatory)] [string] $AdminEmail,
    [Parameter(Mandatory)] [string] $MemberEmail,
    [Parameter(Mandatory)] [string] $Password,
    [ValidateSet("de", "en")] [string] $Language = "en",
    [string] $BaseUrl = "http://localhost:5082",
    [switch] $SkipDatabase,
    [string[]] $Only
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/../.."

if (-not $SkipDatabase) {
    & "$PSScriptRoot/New-ScreenshotDatabase.ps1" -TemplateEmail $AdminEmail -Language $Language
}

function Query([string] $Sql) {
    (docker exec bookennis-dev-services-postgres-1 psql -U postgres -d clubnetz_screens -At -c $Sql) | Select-Object -First 1
}

$barbecueId = Query "select ""Id"" from ""ClubEvents"" where ""ClubId"" = 1 and ""Category"" = 2 order by ""StartDate"" limit 1"
$adminMemberId = Query "select m.""Id"" from ""Members"" m join ""AspNetUsers"" u on u.""Id"" = m.""UserId"" where m.""ClubId"" = 1 and u.""NormalizedEmail"" = upper('$AdminEmail')"
$topMemberId = Query "select ""MemberId"" from ""MemberBadges"" group by 1 order by count(*) desc limit 1"

# The busiest coming day of the moved booking week.
$day = [datetime]::Parse((Query "select ""Interval_From""::date from ""Bookings"" where ""ClubId"" = 1 and ""Interval_From"" > now() + interval '12 hours' and ""Interval_From"" < date_trunc('week', now()) + interval '7 days' group by 1 order by count(*) desc limit 1"))
$us = [Globalization.CultureInfo]::InvariantCulture
$bookingPath = "/clubs/1/booking?Start=$($day.ToString('MM/dd/yyyy', $us))&End=$($day.AddDays(6).ToString('MM/dd/yyyy', $us))&Day=$($day.ToString('MM/dd/yyyy', $us))"
# The buyer gets the day after: the busiest day is often booked out in the afternoon.
$buyDay = $day.AddDays(1)
$buyPath = "/clubs/1/booking?Start=$($buyDay.ToString('MM/dd/yyyy', $us))&End=$($buyDay.AddDays(6).ToString('MM/dd/yyyy', $us))&Day=$($buyDay.ToString('MM/dd/yyyy', $us))"
$openBuyDialog = Get-Content -Raw "$PSScriptRoot/open-buy-dialog.js"
$showSalesTab = "document.querySelectorAll('.choice-chip')[1].click(); true"
$scrollToAfternoon = "document.querySelector('.day-scroll-container').scrollTop = 420; true"

$desktop = @{ Width = 1280; Height = 800 }
$phone = @{ Width = 390; Height = 844; Mobile = $true }

$shots = @(
    @{ Name = "dashboard"; Path = "/clubs/1/my-club"; Email = $AdminEmail } + $desktop
    @{ Name = "booking"; Path = $bookingPath; Email = $AdminEmail; Script = $scrollToAfternoon } + $desktop
    @{ Name = "event"; Path = "/clubs/1/calendar/$barbecueId"; Email = $AdminEmail } + $desktop
    @{ Name = "membership-fees"; Path = "/clubs/1/membership-payments"; Email = $AdminEmail } + $desktop
    @{ Name = "members"; Path = "/clubs/1/members"; Email = $AdminEmail } + $desktop
    @{ Name = "statistics"; Path = "/clubs/1/club-statistics"; Email = $AdminEmail } + $desktop
    @{ Name = "court-sales"; Path = "/clubs/1/court-sales"; Email = $AdminEmail; Script = $showSalesTab } + $desktop
    @{ Name = "phone-booking"; Path = $bookingPath; Email = $AdminEmail; Script = $scrollToAfternoon } + $phone
    @{ Name = "phone-calendar"; Path = "/clubs/1/calendar"; Email = $AdminEmail } + $phone
    @{ Name = "phone-news"; Path = "/clubs/1/news"; Email = $AdminEmail } + $phone
    @{ Name = "phone-trophies"; Path = "/clubs/1/members/$topMemberId/trophy-case"; Email = $AdminEmail } + $phone
    @{ Name = "phone-membership"; Path = "/clubs/1/membership"; Email = $MemberEmail } + $phone
    @{ Name = "phone-court-buy"; Path = $buyPath; Email = "buyer@example.test"; Script = $openBuyDialog } + $phone
)
if ($Only) { $shots = $shots | Where-Object { $_.Name -in $Only } }

& "$PSScriptRoot/Capture-Screenshots.ps1" -Shots $shots -Password $Password -Language $Language -BaseUrl $BaseUrl `
    -OutputDirectory (Join-Path $root "website/screenshots/$Language")
