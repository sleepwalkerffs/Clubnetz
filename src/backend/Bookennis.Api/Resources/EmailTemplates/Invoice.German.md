Rechnung {{ invoice.number }} von {{ club.name }}

Hallo {{ member.full_name }},

im Anhang findest du unsere Rechnung **{{ invoice.number }}**{% if invoice.subject %} ({{ invoice.subject }}){% endif %}.

- **Betrag:** {{ invoice.amount }}
- **Rechnungsdatum:** {{ invoice.date }}
- **Zahlbar bis:** {{ invoice.due_date }}
- **IBAN:** {{ invoice.iban }}
- **Verwendungszweck:** {{ invoice.number }}

Mit dem QR-Code auf der Rechnung zahlst du bequem mit deiner Banking-App.

Vielen Dank!

**{{ club.name }}**
