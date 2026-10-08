Registration closes soon: {{ event.title }}

Hi {{ member.first_name }},

you have not registered for this event yet, and the registration closes soon:

**{{ event.title }}**

- **When:** {{ event.date }}{% if event.time %}, {{ event.time }}{% endif %}{% if event.location %}
- **Where:** {{ event.location }}{% endif %}{% if event.registration_deadline %}
- **Register by:** {{ event.registration_deadline }}{% endif %}

[View event]({{ event.url }}){.btn}

Sporty regards

**{{ club.name }} 🎾**
