Reminder: {{ event.title }}

Hi {{ member.first_name }},

the event you registered for is coming up:

**{{ event.title }}**

- **When:** {{ event.date }}{% if event.time %}, {{ event.time }}{% endif %}{% if event.location %}
- **Where:** {{ event.location }}{% endif %}{% if event.registration_deadline %}
- **Register by:** {{ event.registration_deadline }}{% endif %}

[View event]({{ event.url }}){.btn}

Sporty regards

**{{ club.name }} 🎾**
