namespace Bookennis.Domain.User;

public class UserProfilePicture
{
    private UserProfilePicture() { }

    public UserProfilePicture(int userId, byte[] data, string contentType)
    {
        UserId = userId;
        Data = data;
        ContentType = contentType;
    }

    public int UserId { get; private set; }
    public byte[] Data { get; private set; } = [];
    public string ContentType { get; private set; } = null!;

    public void Update(byte[] data, string contentType)
    {
        Data = data;
        ContentType = contentType;
    }
}
