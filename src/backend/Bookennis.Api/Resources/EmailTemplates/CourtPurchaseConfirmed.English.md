Your court is booked: {{ purchase.date }}, {{ purchase.time }}

Hi {{ member.first_name }},

your court at {{ club.name }} is booked. Have a good game!

- **Court:** {{ purchase.court }}
- **Date:** {{ purchase.date }}
- **Time:** {{ purchase.time }}
- **Price:** {{ purchase.price }}
- **Paid:** {{ purchase.paid }}

[Open the booking calendar]({{ purchase.url }}){.btn}

Sporty regards

**{{ club.name }} 🎾**
