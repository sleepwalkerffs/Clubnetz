namespace Bookennis.Domain.Clubs;

public class OneTimeBadgeImage
{
    private OneTimeBadgeImage() { }

    public OneTimeBadgeImage(int oneTimeBadgeId, byte[] data, string contentType)
    {
        OneTimeBadgeId = oneTimeBadgeId;
        Data = data;
        ContentType = contentType;
    }

    public int OneTimeBadgeId { get; private set; }
    public byte[] Data { get; private set; } = [];
    public string ContentType { get; private set; } = null!;

    public void Update(byte[] data, string contentType)
    {
        Data = data;
        ContentType = contentType;
    }
}
