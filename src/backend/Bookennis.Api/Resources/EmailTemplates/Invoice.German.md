Rechnung {{ invoice.number }} von {{ club.name }}

Hallo {{ member.full_name }},

im Anhang findest du unsere Rechnung **{{ invoice.number }}**{% if invoice.subject %} ({{ invoice.subject }}){% endif %}.

- **Betrag:** {{ invoice.amount }}
- **Rechnungsdatum:** {{ invoice.date }}
- **Zahlbar bis:** {{ invoice.due_date }}
{% if invoice.iban %}- **IBAN:** {{ invoice.iban }}
{% else %}- **Kontonummer:** {{ invoice.account_number }}
- **Routing Number:** {{ invoice.routing_number }}
{% endif %}- **Verwendungszweck:** {{ invoice.number }}

{% if invoice.qr_code %}Mit dem QR-Code auf der Rechnung zahlst du bequem mit deiner Banking-App.{% else %}Bitte zahle auf das Konto auf der Rechnung und gib die Rechnungsnummer als Verwendungszweck an.{% endif %}

Vielen Dank!

**{{ club.name }}**
