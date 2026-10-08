New event: {{ event.title }}

Hi {{ member.first_name }},

there is a new event in the calendar of {{ club.name }}:

**{{ event.title }}**

- **When:** {{ event.date }}{% if event.time %}, {{ event.time }}{% endif %}{% if event.location %}
- **Where:** {{ event.location }}{% endif %}{% if event.registration_deadline %}
- **Register by:** {{ event.registration_deadline }}{% endif %}

[View event]({{ event.url }}){.btn}

Sporty regards

**{{ club.name }} 🎾**
