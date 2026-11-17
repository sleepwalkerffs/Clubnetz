# Clubnetz Administrator's Manual

For club admins: the people who set up and run a club in Clubnetz. A separate manual for members (residents) can follow.

Clubnetz runs in the browser at **my.clubnetz.app** and can be installed on a phone like an app (browser menu → *Add to Home Screen* / *Install app*). Everything below works on a phone and on a computer; on a phone the menu is behind the ☰ button.

> **Draft.** Written from the source code in November 2026 for the first US club. Labels in *italics* are the texts you see in the app (English). If something looks different, the app is right and this manual needs an update.

---

## 1. Getting started

### 1.1 How a club comes to Clubnetz

1. **The platform operator creates the club** in the admin panel: name, country, time zone and currency (for Virginia: *United States*, *Eastern Time (New York)*, *US dollar*), opening hours and prime time. A club admin cannot create a club or change its time zone, country or currency. They are shown read-only under *Club settings → Region*. Ask the operator if one of them is wrong.
2. **The operator makes the first club admin**: an existing Clubnetz account is added to the club with the role *Admin*. So the future admin registers first (*Register* on the login page, confirm the email address) and tells the operator.
3. From then on the club admin does everything else: courts, play modes, seasons, members, roles.

### 1.2 Who sees what

The menu only shows what your roles allow. If you don't see *Club settings*, you are not an admin of this club, or you have another club open. The club switcher at the top of the menu shows the open club and your roles in it.

| Role | Typical person | What the role adds |
|---|---|---|
| *Admin* | board, HOA manager | everything, including club settings and roles. A club always keeps at least one admin. |
| *Maintainer* | court manager | members list, court blockings, club statistics |
| *Treasurer* | treasurer | membership plans and fees, court sales, accounting |
| *Sports Director* | tennis chair | court blockings, badges, booking for any member |
| *Youth Sports Director* | junior program | badges, booking for any member |
| *Trainer* | coach / pro | booking for any member (e.g. lessons) |
| *Auditor* | audit committee | reads the accounting and records the audit of a fiscal year |
| *Clubhouse host* | snack bar operator | the clubhouse menu and tabs |
| *User* | resident / member | booking and the member pages |

A member can have several roles. Under *Club setup → Roles & permissions* you decide what each role may do (for example let maintainers manage the news), and you can give a single member more or less. *Admin* always has every permission.

### 1.3 The order of the first setup

1. *Club settings*: opening hours, courts, a season, play modes, location ([section 2](#2-club-settings)).
2. Members join and you enroll them in the season ([section 3](#3-members)).
3. Optionally: roles for helpers, news and calendar, membership fees, accounting.

---

## 2. Club settings

Menu: *Club setup → Club settings*. Only admins see this page.

### 2.1 Opening hours & rules

- *Opening hours* (*From* / *To*): the first and last bookable time of the day. Bookings outside are refused.
- *Grace period*: how many minutes after the start a booking can still be made or cancelled.
- *Bookings per week* (1–50): despite its name, how many **upcoming** bookings a member may hold at the same time, e.g. 3: after playing one, they can book the next. It applies to every member and counts bookings in play modes with *Counts as match*. On the member page you can set a lower value for a single member.
- *Prime time*: the most popular hours, optionally only on some *Weekdays*. Two switches use it:
  - *No children in prime time*: members younger than the *Age limit* can't book prime time.
  - *No bought slots in prime time*: non-members who buy court time can't buy prime time.
- Changes are saved with the save button of the card (*Unsaved changes* shows until then).

All times follow the club's time zone (Eastern Time for a Virginia club), whatever the time zone of the device or of a member who travels.

### 2.2 Courts

Card *Courts* → *Add* opens *New court*:

- *Name*, e.g. "Court 1".
- *Short name*, e.g. "1": shown in the booking grid on small screens.
- *Indoor court*: no weather forecast for it.

Tap a court to rename it or to close it for a while (*Closed* with *Closed from* / *Closed until*, e.g. resurfacing). The arrows change the order on the booking page. Every court is booked on its own: two courts are two columns in the booking grid.

For a one-off or repeating block of a court (league match, lessons every Tuesday, maintenance) use *Court blockings* instead ([section 5](#5-court-blockings)).

### 2.3 Seasons

Members can only book in a season they are enrolled in. Card *Seasons* → *Add*: a name and a period. A season can be a calendar year (Jan 1 – Dec 31) or the outdoor season.

- Without a season that contains today, nobody can book (*No active season*).
- After creating a season, enroll the members ([section 3.3](#33-enrolling-members-in-a-season)). Next year, *Use … like last season* suggests the same period.

### 2.4 Play modes

A play mode is *what* is booked and under which rules, for example "Singles", "Doubles", "Lesson". Card *Play modes* → *Add*:

- *Name & color*: how the booking looks in the grid.
- *Rules*:
  - *Fixed number of players*: e.g. 2 for singles, 4 for doubles. The booking can only be made with that many players.
  - *Fixed duration*: e.g. 90 minutes. Members can then only book exactly this long.
  - *Limit per season*: at most this many bookings of this play mode per member and season.
  - *Counts as match*: counts for the booking limit, badges and the leaderboard.
  - *Recurring*: members may book weekly series.
  - *Overbooking*: may overlap other bookings (e.g. a social round on all courts). Leave it off for normal play.
  - *Comment*: members can add a comment.
- *Who may book*: the roles that can use the play mode (e.g. only *Trainer* for "Lesson").
- *Courts*: *Bookable on every court* or *Only on these courts*.

#### Rules that always apply

- **No double bookings of a court.** A court can only be booked once at a time, unless the play mode has *Overbooking*.
- **Start times** are on a 30-minute grid (8:00, 8:30, 9:00, …). A 90-minute play mode can start at any of them.
- **Opening hours**: a booking has to lie inside them.
- **Every player must be enrolled** in the running season ([section 3.3](#33-enrolling-members-in-a-season)).

These rules only apply to play modes with *Counts as match*:

- a member can't hold two bookings at the same time,
- a member can book **at most one booking in advance per day**. A booking that starts within the next 30 minutes is exempt, so a free court can still be used spontaneously.
- *Bookings per week* ([section 2.1](#21-opening-hours--rules)) applies.

So for normal play, switch *Counts as match* on.

#### What Clubnetz does not do (yet)

- A limit on how far ahead members can book (e.g. 7 days). Members can book any day of the season.
- A limit per household: limits count per player, and a booking with two players counts for both of them.
- Hiding who booked from other members. Enrolled members see the names of the players; members who are not enrolled see the grid without names.
- Check-in or releasing a court when nobody shows up.
- Slots on a fixed grid other than 30 minutes (e.g. only 8:00, 9:30, 11:00).

### 2.5 Location & weather

Enter the coordinates of the courts (*Latitude, longitude*, e.g. `39.0438, -77.4874`). Members then see the weather forecast for the next three days in the booking grid, in °F and mph for a US club.

### 2.6 Region

*Time zone*, *Country* and *Currency* of the club, read-only. Set by the platform operator ([section 1.1](#11-how-a-club-comes-to-clubnetz)).

---

## 3. Members

Menu: *Members & fees → Members* (admins and maintainers).

### 3.1 How members get into the club

1. The resident registers on the login page (*Register*) and confirms their email address.
2. They choose your club in the list of clubs (*Join*). They are now a member with the role *User*, but **can't book yet**.
3. You enroll them in the running season ([section 3.3](#33-enrolling-members-in-a-season)). That is your approval: only enrolled members can book.

There is no invitation link or import of a resident list yet. Every Clubnetz user can join any club, so check the new names on the members list (quick filter *Not enrolled this season*) before you enroll them.

### 3.2 The members list

Search by name or email, filter by season, age group or email, and copy the email addresses of the filtered members (*Copy emails*, separated by commas, semicolons for Outlook, or one per line) for your own mailings. Quick filters: *This season*, *New this season*, *Last season, not this one*, *Enrolled but never played*.

### 3.3 Enrolling members in a season

Open a member (*Edit*): under *Seasons* tick the season. Only enrolled members can book (*Members can only book in seasons they are enrolled in*). If the club charges a membership fee through Clubnetz, paying it enrolls the member automatically ([section 7](#7-membership-fees-optional)).

A member who is not enrolled still sees the booking grid, but without names, and can't book for free.

### 3.4 Roles and the per-member booking limit

On the member page:

- *Roles*: what the member may do in the club ([section 1.2](#12-who-sees-what)). Making someone *Admin* gives them the club settings too.
- *Bookings per week*: a lower limit for this member than the club's ([section 2.1](#21-opening-hours--rules)). A higher value has no effect: the club's value is the upper bound.

### 3.5 Households (families)

A family groups adults and their children. Parents can book for their children, and family membership plans cover the whole family. Members put their family together themselves on *My club*; admins and treasurers can do it on the member page.

### 3.6 Privacy

Members see each other's name and picture, nothing more. Email, birthday and the rest of a member's data are only visible to the member, to admins and maintainers (*See all members* permission).

---

## 4. Bookings

Members book on *Book*: choose a day, tap a free time on a court, pick the play mode and the other players. The other players get an email (and a push notification if they allowed it).

Admins can:

- open any booking in the grid and change or delete it,
- book for other members without playing (also *Sports Director*, *Youth Sports Director* and *Trainer*, or anyone with the permission *Book for any member*). All rules of the play mode still apply to the players.
- use the *Planner* (desktop) to book many slots at once, e.g. for a league season.

---

## 5. Court blockings

Menu: *Courts & play → Court blockings* (admins, maintainers, sports directors). *Block courts*:

- *Reason* (shown in the booking grid and in the email to affected players), with templates *Tournament*, *Maintenance*, *Weather*, *Training*, *Club event*.
- *Courts*, *Period*, start and end time or *All day*.
- *Repeat*: every week or every few weeks until a date, e.g. lessons every Tuesday evening.

If bookings are in the way, you can delete them with the blocking; their players get an email with the reason.

---

## 6. News and calendar

- *News*: announcements for the members, optionally pinned or with an end date, with attachments. *Send as email* sends one to *All members*, the *Current season* or another group.
- *Calendar*: club events (*Work effort*, *Social*, *Tournament*, *Meeting*, *Other*), optionally with registration and a limit. Members get reminders. *Share* copies a link for WhatsApp or email.

Who may write them: admins, and every role you give *Manage news* / *Manage events* under *Roles & permissions*.

---

## 7. Membership fees (optional)

Menu: *Members & fees → Membership plans* and *Membership fees* (admins, treasurers).

For an HOA whose residents pay through their HOA dues, this is usually not needed: enroll the residents by hand ([section 3.3](#33-enrolling-members-in-a-season)) or use a free plan.

- A *membership plan* has a price (or *Free*), a season, who it covers (*One member*, *Couple*, *Family, 1 adult*, *Family, 2 adults*) and optional conditions (age, new members only, *needs confirmation*).
- Members choose a plan on *My membership*. Depending on the plan they pay online, ask for confirmation, or just confirm. Paid or confirmed means enrolled.
- Online payment needs a Stripe account of the club (*Club setup*, admins only). Stripe pays the money out to the club's bank account. For a US club Clubnetz creates a US Stripe account (cards). Without Stripe you can still record cash or check payments by hand.

---

## 8. Accounting (optional)

Menu: *Finances → Accounting* (admins, treasurers; auditors read only).

1. *Start the club's accounting*: Clubnetz suggests a chart of income and expense categories for a US club (dues, court fees, events, maintenance, insurance, …). You can rename, add and hide categories.
2. *Money accounts*: the club's checking account (account number and ABA routing number), a cash box, the Stripe balance.
3. *Entries*: every income and expense with its receipt (photo or PDF).
4. *Reconcile*: import the CSV export of your online banking and match each line with its entry. Clubnetz understands the usual US formats (month-first dates, `$1,234.56`, `($12.00)` for expenses, separate Debit/Credit columns). For an unknown layout it asks once which column is what. *Bank rules* book recurring lines automatically, e.g. "USTA" → league fees.
5. *Suggested entries*: what Clubnetz knows was paid (membership fees, court sales, Stripe) is suggested as an entry.
6. *Invoices*: invoices to sponsors or other clubs, as PDF on US Letter, with the club's EIN.
7. *Reports*: income statement and assets per fiscal year, as PDF. Close a year when its books are complete; the *Auditor* can record the audit.

---

## 9. More

- *Badges* and *Leaderboard*: playful statistics and awards for the members.
- *Club statistics*: occupancy per court and hour, members, bookings.
- *Emails*: change the texts of the emails the club sends (welcome, booking confirmation, …).
- *Court sales*: sell court time to non-members (price per hour, prime time). Mostly not needed for a resident-only club.
- *API keys*: for a club website that shows bookings or news. Money is never reachable with an API key.

## 10. Help

Questions, problems or a wish: write to the platform operator. As platform operator they can open your club in *support mode* to look at a problem with you; your members see nothing of it, and the audit log records every change.
