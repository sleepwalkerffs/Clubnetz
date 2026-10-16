Dein Platz ist gebucht: {{ purchase.date }}, {{ purchase.time }}

Liebe\*r {{ member.first_name }},

dein Platz bei {{ club.name }} ist gebucht. Viel Spaß beim Spielen!

- **Platz:** {{ purchase.court }}
- **Datum:** {{ purchase.date }}
- **Uhrzeit:** {{ purchase.time }}
- **Preis:** {{ purchase.price }}
- **Bezahlt:** {{ purchase.paid }}

[Buchungskalender öffnen]({{ purchase.url }}){.btn}

Sportliche Grüße

**{{ club.name }} 🎾**
