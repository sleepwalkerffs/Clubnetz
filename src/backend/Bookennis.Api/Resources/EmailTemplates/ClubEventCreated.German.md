Neue Veranstaltung: {{ event.title }}

Liebe\*r {{ member.first_name }},

im Kalender von {{ club.name }} gibt es eine neue Veranstaltung:

**{{ event.title }}**

- **Wann:** {{ event.date }}{% if event.time %}, {{ event.time }}{% endif %}{% if event.location %}
- **Wo:** {{ event.location }}{% endif %}{% if event.registration_deadline %}
- **Anmeldung bis:** {{ event.registration_deadline }}{% endif %}

[Veranstaltung ansehen]({{ event.url }}){.btn}

Sportliche Grüße

**{{ club.name }} 🎾**
