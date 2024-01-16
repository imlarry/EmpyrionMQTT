using Microsoft.VisualStudio.TestPlatform.TestHost;
using YamlDotNet.Serialization;
using ESB.Common;

namespace ESBTests;
public class ESBConfigTests
{
    [Fact]
    public void ESBConfig_Deserialize_ShouldReturnBasicData()
    {
        var yaml = @"
        Name: Test
        Description: Test Description
        Author: Test Author
        Version: 1.0
        GIThub: https://github.com/test
        ModTargets: Client, Dedi, PfServer
        MQTThost: { WithTcpServer: ""localhost"", Username: ""imlarry"", Password: ""impassword"" }
        ";

        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var esbConfig = deserializer.Deserialize<ESBConfig>(new StringReader(yaml));

        Assert.NotNull(esbConfig);
        Assert.Equal("Test", esbConfig.Name);
        Assert.Equal("Test Description", esbConfig.Description);
        Assert.Equal("Test Author", esbConfig.Author);
        Assert.Equal("1.0", esbConfig.Version);
        Assert.Equal("https://github.com/test", esbConfig.GIThub);
        Assert.Equal("Client, Dedi, PfServer", esbConfig.ModTargets);
        Assert.NotNull(esbConfig.MQTThost);
        Assert.Equal("localhost", esbConfig.MQTThost.WithTcpServer);
        Assert.Equal("imlarry", esbConfig.MQTThost.Username);
        Assert.Equal("impassword", esbConfig.MQTThost.Password);

    }

    [Fact]
    public void ESBConfig_Deserialize_BadYamlShouldFail()
    {
        var yaml = @"
        Name: Test
        Description: Test Description
        Author: Test Author
        Version: 1.0
        GIThub: https://github.com/test
        ModTargets: Client, Dedi, PfServer
        MQTThost: { Froboz: ""fail sailor"", WithTcpServer: ""localhost"", Username: ""imlarry"", Password: ""impassword"" }
        ";

        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var esbConfig = deserializer.Deserialize<ESBConfig>(new StringReader(yaml));

        Assert.NotNull(esbConfig);
        Assert.Equal("Test", esbConfig.Name);
        Assert.Equal("Test Description", esbConfig.Description);
        Assert.Equal("Test Author", esbConfig.Author);
        Assert.Equal("1.0", esbConfig.Version);
        Assert.Equal("https://github.com/test", esbConfig.GIThub);
        Assert.Equal("Client, Dedi, PfServer", esbConfig.ModTargets);
        Assert.NotNull(esbConfig.MQTThost);
        Assert.Equal("localhost", esbConfig.MQTThost.WithTcpServer);
        Assert.Equal("imlarry", esbConfig.MQTThost.Username);
        Assert.Equal("impassword", esbConfig.MQTThost.Password);

    }

    [Fact]
    public void ESBConfig_Deserialize_ShouldReturnMQTTConfig()
    {
        var yaml = @"
        MQTThost: { WithTcpServer: ""localhost"", Port: 1883, Username: ""imlarry"", Password: ""impassword"", KeepAlivePeriod: ""00:02:00"", CAFilePath: ""/path/to/cafile"" }
        ";

        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var esbConfig = deserializer.Deserialize<ESBConfig>(new StringReader(yaml));

        Assert.NotNull(esbConfig.MQTThost);
        Assert.Equal("localhost", esbConfig.MQTThost.WithTcpServer);
        Assert.Equal(1883, esbConfig.MQTThost.Port);
        Assert.Equal("imlarry", esbConfig.MQTThost.Username);
        Assert.Equal("impassword", esbConfig.MQTThost.Password);
        Assert.Equal(TimeSpan.FromMinutes(2), esbConfig.MQTThost.KeepAlivePeriod);
        Assert.Equal("/path/to/cafile", esbConfig.MQTThost.CAFilePath);
    }

    [Fact]
    public void ESBConfig_Deserialize_ShouldReturnEmptyStringWhenPropertyIsMissing()
    {
        var yaml = @"
        MQTThost: { WithTcpServer: ""localhost"", Port: 1883, Username: ""imlarry"", Password: ""impassword"", KeepAlivePeriod: ""00:02:00"", CAFilePath: ""/path/to/cafile"" }
        ";

        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var esbConfig = deserializer.Deserialize<ESBConfig>(new StringReader(yaml));

        Assert.Equal(string.Empty, esbConfig.Name);
        Assert.Equal(string.Empty, esbConfig.Description);
        Assert.Equal(string.Empty, esbConfig.Author);
        Assert.Equal(string.Empty, esbConfig.Version);
        Assert.Equal(string.Empty, esbConfig.GIThub);
        Assert.Equal(string.Empty, esbConfig.ModTargets);
    }

}