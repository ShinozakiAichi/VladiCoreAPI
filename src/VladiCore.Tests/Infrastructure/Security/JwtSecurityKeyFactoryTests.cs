using System;
using System.Security.Cryptography;
using NUnit.Framework;
using VladiCore.Api.Infrastructure.Security;

namespace VladiCore.Tests.Infrastructure.Security;

[TestFixture]
public class JwtSecurityKeyFactoryTests
{
    [Test]
    public void Create_ShouldReturnKey_ForPlainTextValueLongEnough()
    {
        var signingKey = new string('a', 32);

        var result = JwtSecurityKeyFactory.Create(signingKey);

        Assert.That(result.Key.Length, Is.EqualTo(32));
    }

    [Test]
    public void Create_ShouldReturnKey_ForBase64EncodedValue()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        var base64Key = Convert.ToBase64String(keyBytes);

        var result = JwtSecurityKeyFactory.Create(base64Key);

        Assert.That(result.Key, Is.EqualTo(keyBytes));
    }

    [Test]
    public void Create_ShouldThrow_WhenKeyTooShort()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => JwtSecurityKeyFactory.Create("short"));

        Assert.That(exception!.Message, Does.Contain("Jwt:SigningKey"));
        Assert.That(exception.Message, Does.Contain("128 bits"));
    }
}
