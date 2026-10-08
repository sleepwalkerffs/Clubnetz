Willkommen im Verein!

Liebe\*r {{ member.first_name }},

schön, dass du jetzt Teil von {{ club.name }} bist – herzlich willkommen! 🎉 Wir freuen uns sehr, dich in unserem Tennisclub begrüßen zu dürfen und hoffen, dich bald auf unserer Anlage zu sehen.

**Bei {{ club.name }} geht's nicht nur um Tennis, sondern vor allem um Gemeinschaft, Spaß am Spiel und ein entspanntes Miteinander – egal ob du gerade erst anfängst oder schon länger spielst.**

Deine Mitgliedschaft ist für die Saison {{ season.period }} aktiv.

{% if club.website_url %}
### Alles Wichtige für deinen Start bei uns

[Zu unserer Website]({{ club.website_url }}){.btn}

Auf unserer Website findest du, wie die Online-Platzbuchung funktioniert, unsere Buchungs- und Spielregeln, die Platzordnung und Vereinsstatuten, die Ansprechpartner im Verein und was sonst noch so im Verein läuft.
{% endif %}

### Noch Fragen?

Wenn dir noch etwas unklar ist oder du noch ein paar Fragen hast, kannst du dich jederzeit bei uns melden.

Am besten lernt man den Club sowieso direkt am Platz kennen 😊

Wir wünschen dir viele schöne Matches, gute Ballwechsel und vor allem viel Freude beim Tennis spielen bei {{ club.name }}!

Sportliche Grüße

**{{ club.name }} 🎾**
