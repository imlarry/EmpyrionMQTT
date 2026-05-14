using System;
using ESB.Messaging;

namespace ESBTests.Bus;

public class Test_BusRequestException
{
    [Fact]
    public void BusError_MatchesConstructorArgument()
    {
        var ex = new BusRequestException("entity not found");
        Assert.Equal("entity not found", ex.BusError);
    }

    [Fact]
    public void Message_ContainsBusError()
    {
        var ex = new BusRequestException("entity not found");
        Assert.Contains("entity not found", ex.Message);
    }

    [Fact]
    public void IsException()
    {
        Assert.IsAssignableFrom<Exception>(new BusRequestException("x"));
    }
}
