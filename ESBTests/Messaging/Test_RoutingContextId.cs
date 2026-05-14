using ESB.Messaging;

namespace ESBTests.Messaging;

public class Test_RoutingContextId
{
    // -------------------------------------------------------------------------
    // Broadcast sentinel
    // -------------------------------------------------------------------------

    [Fact]
    public void Broadcast_Id_IsFixedSentinel()
    {
        var rc = RoutingContextId.Broadcast();
        Assert.Equal("00000000", rc.Id);
        Assert.Equal(RoutingContextKind.Broadcast, rc.Kind);
    }

    [Fact]
    public void Broadcast_Value_MatchesSentinelConstant()
    {
        Assert.Equal("00000000", RoutingContextId.BroadcastValue);
    }

    // -------------------------------------------------------------------------
    // Width: every kind hashes to 8 chars
    // -------------------------------------------------------------------------

    [Fact]
    public void Machine_Id_Is8Chars()
        => Assert.Equal(8, RoutingContextId.Machine("token-xyz").Id.Length);

    [Fact]
    public void Game_Id_Is8Chars()
        => Assert.Equal(8, RoutingContextId.Game("C:/saves/Empyrion", "machine01").Id.Length);

    [Fact]
    public void Playfield_Id_Is8Chars()
        => Assert.Equal(8, RoutingContextId.Playfield("game1234", "Akua", "Base").Id.Length);

    [Fact]
    public void Player_Id_Is8Chars()
        => Assert.Equal(8, RoutingContextId.Player("steam-76561198000000000").Id.Length);

    [Fact]
    public void PlayerInGame_Id_Is8Chars()
        => Assert.Equal(8, RoutingContextId.PlayerInGame("plyr1234", "game5678").Id.Length);

    // -------------------------------------------------------------------------
    // Kind reflects the factory used
    // -------------------------------------------------------------------------

    [Fact]
    public void Machine_Kind_IsMachine()
        => Assert.Equal(RoutingContextKind.Machine, RoutingContextId.Machine("t").Kind);

    [Fact]
    public void Game_Kind_IsGame()
        => Assert.Equal(RoutingContextKind.Game, RoutingContextId.Game("p", "m").Kind);

    [Fact]
    public void Playfield_Kind_IsPlayfield()
        => Assert.Equal(RoutingContextKind.Playfield, RoutingContextId.Playfield("g", "ss", "pf").Kind);

    [Fact]
    public void Player_Kind_IsPlayer()
        => Assert.Equal(RoutingContextKind.Player, RoutingContextId.Player("s").Kind);

    [Fact]
    public void PlayerInGame_Kind_IsPlayerInGame()
        => Assert.Equal(RoutingContextKind.PlayerInGame, RoutingContextId.PlayerInGame("p", "g").Kind);

    // -------------------------------------------------------------------------
    // Determinism
    // -------------------------------------------------------------------------

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

    [Fact]
    public void Playfield_DifferentSolarSystem_ProducesDifferentId()
    {
        var a = RoutingContextId.Playfield("g", "AlphaCentauri", "Akua").Id;
        var b = RoutingContextId.Playfield("g", "BetaHydri",     "Akua").Id;
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Player_DifferentSteamIds_ProducesDifferentIds()
    {
        var a = RoutingContextId.Player("76561198000000001").Id;
        var b = RoutingContextId.Player("76561198000000002").Id;
        Assert.NotEqual(a, b);
    }

    // -------------------------------------------------------------------------
    // PlayerInGame canonical ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void PlayerInGame_ArgumentOrder_DoesNotChangeResult()
    {
        var a = RoutingContextId.PlayerInGame("plyr1234", "game5678").Id;
        var b = RoutingContextId.PlayerInGame("game5678", "plyr1234").Id;
        Assert.Equal(a, b);
    }

    [Fact]
    public void PlayerInGame_DifferentInputs_ProducesDifferentResult()
    {
        var a = RoutingContextId.PlayerInGame("plyr1234", "game5678").Id;
        var b = RoutingContextId.PlayerInGame("plyr9999", "game5678").Id;
        Assert.NotEqual(a, b);
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
