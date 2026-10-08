Congratulations on your new badge!

Hi {{ member.first_name }},

You've just earned the {{ badge.name }} badge at {{ club.name }}. Well played!

{% if badge.image_url %}
![{{ badge.name }}]({{ badge.image_url }}){.badge-image width=96 height=96}
{% endif %}

**{{ badge.name }}**

{{ badge.description }}

[View my trophy case]({{ trophy_case_url }}){.btn}

Keep up the great work!

Sporty regards

**{{ club.name }} 🎾**
