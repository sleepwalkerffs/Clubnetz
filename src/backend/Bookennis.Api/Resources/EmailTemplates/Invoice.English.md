Invoice {{ invoice.number }} from {{ club.name }}

Hello {{ member.full_name }},

please find attached our invoice **{{ invoice.number }}**{% if invoice.subject %} ({{ invoice.subject }}){% endif %}.

- **Amount:** {{ invoice.amount }}
- **Invoice date:** {{ invoice.date }}
- **Due:** {{ invoice.due_date }}
{% if invoice.iban %}- **IBAN:** {{ invoice.iban }}
{% else %}- **Account number:** {{ invoice.account_number }}
- **Routing number:** {{ invoice.routing_number }}
{% endif %}- **Reference:** {{ invoice.number }}

{% if invoice.qr_code %}The QR code on the invoice lets you pay with your banking app.{% else %}Please pay to the account on the invoice and give the invoice number as reference.{% endif %}

Thank you!

**{{ club.name }}**
