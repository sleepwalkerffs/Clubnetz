Neue Buchung: {{ booking.date }}

Liebe\*r {{ member.first_name }},

{{ booked_by }} hat dich für eine Buchung eingetragen:

- **Wann:** {{ booking.date }}, {{ booking.time }}
- **Platz:** {{ booking.court }}
- **Spielmodus:** {{ booking.play_mode }}
- **Spieler\*innen:** {{ booking.players }}

[In der App öffnen]({{ booking.url }}){.btn}

Sportliche Grüße

**{{ club.name }} 🎾**
