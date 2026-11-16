Payment reminder: invoice {{ invoice.number }}

Hello {{ member.full_name }},

our invoice **{{ invoice.number }}** of {{ invoice.date }} for **{{ invoice.amount }}** was due on {{ invoice.due_date }}. Maybe it got lost - that happens.

Please pay the amount within the next days to {% if invoice.iban %}**{{ invoice.iban }}**{% else %}account **{{ invoice.account_number }}** (routing number **{{ invoice.routing_number }}**){% endif %} with the reference **{{ invoice.number }}**. The invoice is attached again.

Already paid? Then please ignore this message.

Thank you!

**{{ club.name }}**
