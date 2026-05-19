using ESB.Messaging;

namespace ESBTests.Messaging;

public class Test_RoutingContextId
{
    // -------------------------------------------------------------------------
    // Widths: Machine = 5, Lobby/Game = 8 (eyeball-distinguishable in a topic)
    // -------------------------------------------------------------------------

    [Fact]
    public void Machine_Id_Is5Chars()
        => Assert.Equal(5, RoutingContextId.Machine("token-xyz").Id.Length);

    [Fact]
    public void Lobby_Id_Is8Chars()
        => Assert.Equal(8, RoutingContextId.Lobby("machine01").Id.Length);

    [Fact]
    public void Game_Id_Is8Chars()
        => Assert.Equal(8, RoutingContextId.Game("C:/saves/Empyrion", "machine01").Id.Length);

    // -------------------------------------------------------------------------
    // Kind reflects the factory used
    // -------------------------------------------------------------------------

    [Fact]
    public void Machine_Kind_IsMachine()
        => Assert.Equal(RoutingContextKind.Machine, RoutingContextId.Machine("t").Kind);

    [Fact]
    public void Lobby_Kind_IsLobby()
        => Assert.Equal(RoutingContextKind.Lobby, RoutingContextId.Lobby("m").Kind);

    [Fact]
    public void Game_Kind_IsGame()
        => Assert.Equal(RoutingContextKind.Game, RoutingContextId.Game("p", "m").Kind);

    // -------------------------------------------------------------------------
    // Determinism
    // -------------------------------------------------------------------------

    [Fact]
    public void Machine_SameToken_ProducesSameId()
    {
        var a = RoutingContextId.Machine("token-xyz").Id;
        var b = RoutingContextId.Machine("token-xyz").Id;
        Assert.Equal(a, b);
    }

    [Fact]
    public void Machine_DifferentToken_ProducesDifferentId()
    {
        var a = RoutingContextId.Machine("token-xyz").Id;
        var b = RoutingContextId.Machine("token-abc").Id;
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Lobby_SameMachine_ProducesSameId()
    {
        var a = RoutingContextId.Lobby("machine01").Id;
        var b = RoutingContextId.Lobby("machine01").Id;
        Assert.Equal(a, b);
    }

    [Fact]
    public void Lobby_DifferentMachines_ProduceDifferentIds()
    {
        var a = RoutingContextId.Lobby("machine01").Id;
        var b = RoutingContextId.Lobby("machine02").Id;
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Game_SameInputs_ProducesSameId()
    {
        var a = RoutingContextId.Game("C:/saves/Empyrion", "machine01").Id;
        var b = RoutingContextId.Game("C:/saves/Empyrion", "machine01").Id;
        Assert.Equal(a, b);
    }

    [Fact]
    public void Game_DifferentMachine_ProducesDifferentId()
    {
        var a = RoutingContextId.Game("C:/saves/Empyrion", "machine01").Id;
        var b = RoutingContextId.Game("C:/saves/Empyrion", "machine02").Id;
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Game_DifferentSavePath_ProducesDifferentId()
    {
        var a = RoutingContextId.Game("C:/saves/Empyrion", "machine01").Id;
        var b = RoutingContextId.Game("C:/saves/Other",    "machine01").Id;
        Assert.NotEqual(a, b);
    }

    // -------------------------------------------------------------------------
    // Lobby and Game are wire-shape identical but seed-distinct: a lobby on
    // machine X must not collide with a real game whose other seed input happens
    // to be the lobby sentinel.
    // -------------------------------------------------------------------------

    [Fact]
    public void Lobby_AndGame_OnSameMachine_AreDifferent()
    {
        var lobby = RoutingContextId.Lobby("machine01").Id;
        var game  = RoutingContextId.Game("C:/saves/Empyrion", "machine01").Id;
        Assert.NotEqual(lobby, game);
    }

    // -------------------------------------------------------------------------
    // Output alphabet is base-36 lowercase
    // -------------------------------------------------------------------------

    [Fact]
    public void Machine_Id_IsBase36Lowercase()
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var id = RoutingContextId.Machine("some-token").Id;
        Assert.All(id, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void Lobby_Id_IsBase36Lowercase()
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var id = RoutingContextId.Lobby("some-machine").Id;
        Assert.All(id, c => Assert.Contains(c, alphabet));
    }

    // -------------------------------------------------------------------------
    // Equality and implicit string conversion
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_SameIdAndKind_IsTrue()
    {
        var a = RoutingContextId.Game("p", "m");
        var b = RoutingContextId.Game("p", "m");
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void ImplicitStringConversion_YieldsIdValue()
    {
        var rc = RoutingContextId.Game("p", "m");
        string s = rc;
        Assert.Equal(rc.Id, s);
    }
}
