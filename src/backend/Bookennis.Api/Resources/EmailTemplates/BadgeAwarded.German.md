Herzlichen Glückwunsch zu deinem neuen Abzeichen!

Liebe\*r {{ member.first_name }},

Du hast gerade das Abzeichen {{ badge.name }} bei {{ club.name }} verdient. Gut gespielt!

{% if badge.image_url %}
![{{ badge.name }}]({{ badge.image_url }}){.badge-image width=96 height=96}
{% endif %}

**{{ badge.name }}**

{{ badge.description }}

[Meine Trophäensammlung ansehen]({{ trophy_case_url }}){.btn}

Weiter so!

Sportliche Grüße

**{{ club.name }} 🎾**
