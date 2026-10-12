Danke für deinen Mitgliedsbeitrag

Liebe\*r {{ member.first_name }},

vielen Dank, wir haben deinen Mitgliedsbeitrag für {{ club.name }} erhalten.

- **Tarif:** {{ membership.plan }}
- **Saison:** {{ membership.season }}
- **Betrag:** {{ membership.amount }}
- **Für:** {{ membership.members }}{% if membership.payment_method != "" %}
- **Bezahlt per:** {{ membership.payment_method }}{% endif %}

[Meine Mitgliedschaft ansehen]({{ membership.url }}){.btn}

Sportliche Grüße

**{{ club.name }} 🎾**
