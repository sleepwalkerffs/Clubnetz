Zahlungserinnerung: Rechnung {{ invoice.number }}

Hallo {{ member.full_name }},

unsere Rechnung **{{ invoice.number }}** vom {{ invoice.date }} über **{{ invoice.amount }}** war am {{ invoice.due_date }} fällig. Vielleicht ist sie untergegangen – das kann passieren.

Bitte überweise den Betrag in den nächsten Tagen auf **{{ invoice.iban }}** mit dem Verwendungszweck **{{ invoice.number }}**. Die Rechnung hängt noch einmal an.

Hast du schon bezahlt? Dann betrachte diese Nachricht bitte als gegenstandslos.

Vielen Dank!

**{{ club.name }}**
