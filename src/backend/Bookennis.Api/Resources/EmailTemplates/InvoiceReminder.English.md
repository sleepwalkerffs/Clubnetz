Payment reminder: invoice {{ invoice.number }}

Hello {{ member.full_name }},

our invoice **{{ invoice.number }}** of {{ invoice.date }} for **{{ invoice.amount }}** was due on {{ invoice.due_date }}. Maybe it got lost - that happens.

Please transfer the amount within the next days to **{{ invoice.iban }}** with the reference **{{ invoice.number }}**. The invoice is attached again.

Already paid? Then please ignore this message.

Thank you!

**{{ club.name }}**
