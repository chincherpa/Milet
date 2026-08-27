using Milet.Domain.Services;
using Xunit;

namespace Milet.Domain.Tests;

public class PasswortHasherTests
{
    [Fact]
    public void Hash_ErzeugtSelbstbeschreibendesFormat()
    {
        var hash = PasswortHasher.Hash("MeinPasswort123");

        var teile = hash.Split('.');
        Assert.Equal(3, teile.Length);
        Assert.True(int.Parse(teile[0]) > 0);
    }

    [Fact]
    public void Hash_ZweiAufrufeMitGleichemPasswort_ErzeugenUnterschiedlicheHashes()
    {
        var hash1 = PasswortHasher.Hash("MeinPasswort123");
        var hash2 = PasswortHasher.Hash("MeinPasswort123");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_KorrektesPasswort_LiefertTrue()
    {
        var hash = PasswortHasher.Hash("MeinPasswort123");

        Assert.True(PasswortHasher.Verify("MeinPasswort123", hash));
    }

    [Fact]
    public void Verify_FalschesPasswort_LiefertFalse()
    {
        var hash = PasswortHasher.Hash("MeinPasswort123");

        Assert.False(PasswortHasher.Verify("FalschesPasswort", hash));
    }

    [Fact]
    public void Verify_UngueltigesFormat_LiefertFalseStattException()
    {
        Assert.False(PasswortHasher.Verify("MeinPasswort123", "kaputt"));
        Assert.False(PasswortHasher.Verify("MeinPasswort123", "abc.def.ghi"));
        Assert.False(PasswortHasher.Verify("MeinPasswort123", string.Empty));
    }

    [Fact]
    public void Verify_LeeresPasswort_LiefertFalse()
    {
        var hash = PasswortHasher.Hash("MeinPasswort123");

        Assert.False(PasswortHasher.Verify(string.Empty, hash));
    }
}
