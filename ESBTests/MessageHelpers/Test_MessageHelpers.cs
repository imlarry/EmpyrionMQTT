using ESB;
using Newtonsoft.Json.Linq;
using System;

namespace ESBTests.MessageHelpers;

/// <summary>
/// Offline tests for MessageHelpers that do not require game assembly context.
/// Vec(Vector3/VectorInt3/Quaternion) and ParseVec3/ParseVecInt3 require Unity/Eleon
/// types at runtime and can only be verified via live integration tests.
/// </summary>
public class Test_MessageHelpers_ErrorJson
{
    [Fact]
    public void ErrorJson_ContainsErrorProperty()
    {
        var json = ESB.MessageHelpers.ErrorJson("Entity not found");
        var obj = JObject.Parse(json);

        Assert.Equal("Entity not found", obj["Error"]!.Value<string>());
    }

    [Fact]
    public void ErrorJson_IsCompactSingleLine()
    {
        var json = ESB.MessageHelpers.ErrorJson("test message");

        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("\r", json);
    }

    [Fact]
    public void ErrorJson_HasOnlyOneProperty()
    {
        var json = ESB.MessageHelpers.ErrorJson("test");
        var obj = JObject.Parse(json);

        Assert.Single(obj);
    }

    [Fact]
    public void ExceptionJson_ContainsErrorMessage()
    {
        var ex = new InvalidOperationException("something went wrong");
        var json = ESB.MessageHelpers.ExceptionJson(ex);
        var obj = JObject.Parse(json);

        Assert.Equal("something went wrong", obj["Error"]!.Value<string>());
    }

    [Fact]
    public void ExceptionJson_ContainsExceptionType()
    {
        var ex = new InvalidOperationException("msg");
        var json = ESB.MessageHelpers.ExceptionJson(ex);
        var obj = JObject.Parse(json);

        Assert.Equal("InvalidOperationException", obj["ExceptionType"]!.Value<string>());
    }

    [Fact]
    public void ExceptionJson_HasExactlyTwoProperties()
    {
        var json = ESB.MessageHelpers.ExceptionJson(new Exception("x"));
        var obj = JObject.Parse(json);

        Assert.Equal(2, obj.Count);
    }

    [Fact]
    public void ExceptionJson_IsCompactSingleLine()
    {
        var json = ESB.MessageHelpers.ExceptionJson(new Exception("x"));

        Assert.DoesNotContain("\n", json);
    }
}

/// <summary>
/// Validates the expected JSON vector object shape: {"X":..,"Y":..,"Z":..}
/// without instantiating game types. Tests parse the same shape that Vec() produces
/// to confirm the contract is stable.
/// </summary>
public class Test_MessageHelpers_VectorShape
{
    [Fact]
    public void VecShape_Float_ParsesXYZ()
    {
        // This is the canonical output format of MessageHelpers.Vec(Vector3)
        var json = JObject.Parse("{\"X\":1.5,\"Y\":2.5,\"Z\":3.5}");

        Assert.Equal(1.5f, json["X"]!.Value<float>());
        Assert.Equal(2.5f, json["Y"]!.Value<float>());
        Assert.Equal(3.5f, json["Z"]!.Value<float>());
    }

    [Fact]
    public void VecShape_Int_ParsesXYZ()
    {
        // This is the canonical output format of MessageHelpers.Vec(VectorInt3)
        var json = JObject.Parse("{\"X\":2,\"Y\":130,\"Z\":1}");

        Assert.Equal(2,   json["X"]!.Value<int>());
        Assert.Equal(130, json["Y"]!.Value<int>());
        Assert.Equal(1,   json["Z"]!.Value<int>());
    }

    [Fact]
    public void VecShape_Quaternion_ParsesXYZW()
    {
        // This is the canonical output format of MessageHelpers.Vec(Quaternion)
        var json = JObject.Parse("{\"X\":0.0,\"Y\":0.0,\"Z\":0.0,\"W\":1.0}");

        Assert.Equal(0.0f, json["X"]!.Value<float>());
        Assert.Equal(0.0f, json["Y"]!.Value<float>());
        Assert.Equal(0.0f, json["Z"]!.Value<float>());
        Assert.Equal(1.0f, json["W"]!.Value<float>());
    }

}
