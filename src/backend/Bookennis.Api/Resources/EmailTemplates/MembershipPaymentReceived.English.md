Thank you for your membership fee

Hi {{ member.first_name }},

thank you, we have received your membership fee for {{ club.name }}.

- **Plan:** {{ membership.plan }}
- **Season:** {{ membership.season }}
- **Amount:** {{ membership.amount }}
- **For:** {{ membership.members }}{% if membership.payment_method != "" %}
- **Paid by:** {{ membership.payment_method }}{% endif %}

[View my membership]({{ membership.url }}){.btn}

Sporty regards

**{{ club.name }} 🎾**
