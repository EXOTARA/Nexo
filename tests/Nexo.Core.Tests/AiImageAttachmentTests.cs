using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class AiImageAttachmentTests
{
    [Fact]
    public void FromBytes_CreatesDataUrl()
    {
        var attachment = AiImageAttachment.FromBytes([1, 2, 3], "image/png", "captura");

        Assert.Equal("image/png", attachment.MediaType);
        Assert.Equal("captura", attachment.Name);
        Assert.Equal("data:image/png;base64,AQID", attachment.DataUrl);
    }

    [Fact]
    public void FromBytes_RejectsEmptyImage()
    {
        Assert.Throws<ArgumentException>(() => AiImageAttachment.FromBytes([]));
    }

    [Fact]
    public void FromBytes_RejectsNonImageMimeType()
    {
        Assert.Throws<ArgumentException>(() =>
            AiImageAttachment.FromBytes([1], "application/octet-stream"));
    }
}
