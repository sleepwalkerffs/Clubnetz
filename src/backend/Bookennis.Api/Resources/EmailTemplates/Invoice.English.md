Invoice {{ invoice.number }} from {{ club.name }}

Hello {{ member.full_name }},

please find attached our invoice **{{ invoice.number }}**{% if invoice.subject %} ({{ invoice.subject }}){% endif %}.

- **Amount:** {{ invoice.amount }}
- **Invoice date:** {{ invoice.date }}
- **Due:** {{ invoice.due_date }}
- **IBAN:** {{ invoice.iban }}
- **Reference:** {{ invoice.number }}

The QR code on the invoice lets you pay with your banking app.

Thank you!

**{{ club.name }}**
