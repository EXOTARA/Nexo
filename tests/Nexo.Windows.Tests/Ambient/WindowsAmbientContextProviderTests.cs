using Nexo.Windows.Ambient;

namespace Nexo.Windows.Tests.Ambient;

public sealed class WindowsAmbientContextProviderTests
{
    [Fact]
    public void Capture_WithZeroHandle_ReturnsNull()
    {
        var provider = new WindowsAmbientContextProvider();

        var result = provider.Capture(0);

        Assert.Null(result);
    }

    [Fact]
    public void Capture_WithHandleThatDoesNotExist_ReturnsNull()
    {
        var provider = new WindowsAmbientContextProvider();

        // Un handle grande y arbitrario que casi con certeza no corresponde a ninguna ventana viva.
        var result = provider.Capture(0x7FFFFFFF);

        Assert.Null(result);
    }
}
