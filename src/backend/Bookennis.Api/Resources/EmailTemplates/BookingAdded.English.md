New booking: {{ booking.date }}

Hi {{ member.first_name }},

{{ booked_by }} booked a court with you:

- **When:** {{ booking.date }}, {{ booking.time }}
- **Court:** {{ booking.court }}
- **Play mode:** {{ booking.play_mode }}
- **Players:** {{ booking.players }}

[Open in the app]({{ booking.url }}){.btn}

Sporty regards

**{{ club.name }} 🎾**
