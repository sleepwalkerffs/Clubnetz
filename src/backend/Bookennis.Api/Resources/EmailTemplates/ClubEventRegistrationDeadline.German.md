Anmeldeschluss naht: {{ event.title }}

Liebe\*r {{ member.first_name }},

du hast dich für diese Veranstaltung noch nicht angemeldet und die Anmeldung endet bald:

**{{ event.title }}**

- **Wann:** {{ event.date }}{% if event.time %}, {{ event.time }}{% endif %}{% if event.location %}
- **Wo:** {{ event.location }}{% endif %}{% if event.registration_deadline %}
- **Anmeldung bis:** {{ event.registration_deadline }}{% endif %}

[Veranstaltung ansehen]({{ event.url }}){.btn}

Sportliche Grüße

**{{ club.name }} 🎾**
