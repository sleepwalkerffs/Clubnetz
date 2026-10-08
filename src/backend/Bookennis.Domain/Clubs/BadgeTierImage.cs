namespace Bookennis.Domain.Clubs;

public class BadgeTierImage
{
    private BadgeTierImage() { }

    public BadgeTierImage(int badgeTierId, byte[] data, string contentType)
    {
        BadgeTierId = badgeTierId;
        Data = data;
        ContentType = contentType;
    }

    public int BadgeTierId { get; private set; }
    public byte[] Data { get; private set; } = [];
    public string ContentType { get; private set; } = null!;

    public void Update(byte[] data, string contentType)
    {
        Data = data;
        ContentType = contentType;
    }
}
