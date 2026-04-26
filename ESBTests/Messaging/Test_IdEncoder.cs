using ESB.Messaging;
using System;
using System.Linq;

namespace ESBTests.Messaging;

public class Test_IdEncoder
{
    [Fact]
    public void ToBase36_ZeroBytes_ReturnsAllZeros()
    {
        var result = IdEncoder.ToBase36(new byte[16], 25);
        Assert.Equal(new string('0', 25), result);
    }

    [Fact]
    public void ToBase36_ClientId_Is4Chars()
    {
        var result = IdEncoder.ToBase36(Guid.NewGuid().ToByteArray(), 4);
        Assert.Equal(4, result.Length);
    }

    [Fact]
    public void ToBase36_MachineId_Is6Chars()
    {
        var bytes = new byte[32];
        new Random().NextBytes(bytes);
        var result = IdEncoder.ToBase36(bytes, 6);
        Assert.Equal(6, result.Length);
    }

    [Fact]
    public void ToBase36_OutputContainsOnlyAlphabetChars()
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var result = IdEncoder.ToBase36(Guid.NewGuid().ToByteArray(), 4);
        Assert.All(result, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void ToBase36_DifferentInputs_ProduceDifferentOutputs()
    {
        var a = IdEncoder.ToBase36(Guid.NewGuid().ToByteArray(), 4);
        var b = IdEncoder.ToBase36(Guid.NewGuid().ToByteArray(), 4);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ToBase36_NullBytes_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => IdEncoder.ToBase36(null, 25));
    }

    [Fact]
    public void ToBase36_ZeroWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IdEncoder.ToBase36(new byte[16], 0));
    }
}
