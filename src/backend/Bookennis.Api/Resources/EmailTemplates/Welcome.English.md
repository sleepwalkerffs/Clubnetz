Welcome to the Club!

Dear {{ member.first_name }},

Great to have you as part of {{ club.name }} – welcome! 🎉 We're thrilled to welcome you to our tennis club and hope to see you on the courts soon.

**At {{ club.name }} it's not just about tennis, but above all about community, fun on the court and a relaxed atmosphere – whether you're just starting out or have been playing for a while.**

Your membership is active for the season {{ season.period }}.

{% if club.website_url %}
### Everything you need to get started

[Visit our website]({{ club.website_url }}){.btn}

On our website you can find out how the online court booking works, our booking and playing rules, the court rules and club statutes, the contact persons in the club and everything else happening at the club.
{% endif %}

### Questions?

If anything is unclear or you have a few questions, feel free to reach out to us at any time.

The best way to get to know the club is right on the court 😊

We wish you many great matches, good rallies and above all lots of fun playing tennis at {{ club.name }}!

Sporty regards

**{{ club.name }} 🎾**
