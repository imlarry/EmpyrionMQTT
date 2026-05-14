using ESB.Messaging;

namespace ESBTests.Messaging;

public class Test_IdentifierHelper
{
    [Fact]
    public void GenerateIdentifier_SameInput_ReturnsSameResult()
    {
        var a = IdentifierHelper.GenerateIdentifier("machine-hostname", 6);
        var b = IdentifierHelper.GenerateIdentifier("machine-hostname", 6);
        Assert.Equal(a, b);
    }

    [Fact]
    public void GenerateIdentifier_DifferentInputs_ReturnsDifferentResults()
    {
        var a = IdentifierHelper.GenerateIdentifier("host-a", 6);
        var b = IdentifierHelper.GenerateIdentifier("host-b", 6);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GenerateIdentifier_NullInput_DoesNotThrow()
    {
        var result = IdentifierHelper.GenerateIdentifier(null, 6);
        Assert.NotNull(result);
        Assert.Equal(6, result.Length);
    }

    [Fact]
    public void GenerateIdentifier_Width_IsRespected()
    {
        Assert.Equal(4, IdentifierHelper.GenerateIdentifier("x", 4).Length);
        Assert.Equal(8, IdentifierHelper.GenerateIdentifier("x", 8).Length);
    }

    [Fact]
    public void GenerateIdentifier_OutputIsBase36()
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var result = IdentifierHelper.GenerateIdentifier("some-machine", 6);
        Assert.All(result, c => Assert.Contains(c, alphabet));
    }
}
