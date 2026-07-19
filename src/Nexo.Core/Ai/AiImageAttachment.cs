namespace Nexo.Core.Ai;

public sealed record AiImageAttachment(
    string MediaType,
    string Base64Data,
    string? Name = null)
{
    public string DataUrl => $"data:{MediaType};base64,{Base64Data}";

    public static AiImageAttachment FromBytes(
        byte[] data,
        string mediaType = "image/png",
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
        {
            throw new ArgumentException("La imagen no puede estar vacía.", nameof(data));
        }

        if (string.IsNullOrWhiteSpace(mediaType) ||
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("El tipo MIME debe ser una imagen válida.", nameof(mediaType));
        }

        return new AiImageAttachment(
            mediaType.Trim().ToLowerInvariant(),
            Convert.ToBase64String(data),
            string.IsNullOrWhiteSpace(name) ? null : name.Trim());
    }
}
