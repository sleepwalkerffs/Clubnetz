using Bookennis.Domain.Base;

namespace Bookennis.Domain.ClubEvents;

/// <summary>A selected option of a registration. <see cref="Quantity"/> is 1 unless the question allows quantities.</summary>
public class ClubEventRegistrationAnswer : DomainEntity
{
#pragma warning disable CS8618
    private ClubEventRegistrationAnswer() { }
#pragma warning restore CS8618

    internal ClubEventRegistrationAnswer(ClubEventRegistration registration, int optionId, int quantity)
    {
        Registration = registration;
        ClubEventRegistrationId = registration.Id;
        ClubEventQuestionOptionId = optionId;
        Quantity = quantity;
    }

    public ClubEventRegistration Registration { get; private set; }
    public int ClubEventRegistrationId { get; private set; }
    public int ClubEventQuestionOptionId { get; private set; }
    public int Quantity { get; private set; }

    internal void Update(int quantity) => Quantity = quantity;
}
