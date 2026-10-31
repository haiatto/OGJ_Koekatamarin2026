using NUnit.Framework;

namespace Koekatamarin.Tests
{
    public sealed class LanAddressTests
    {
        [TestCase("192.168.1.10")]
        [TestCase("10.0.0.2")]
        [TestCase("172.16.2.3")]
        [TestCase("127.0.0.1")]
        public void AcceptsLiteralIPv4(string text)
        { Assert.That(LanAddress.TryParse(text, out var address), Is.True); Assert.That(address.ToString(), Is.EqualTo(text)); }

        [TestCase("localhost")]
        [TestCase("192.168.1.10:47726")]
        [TestCase("1")]
        [TestCase("0x7f.0.0.1")]
        [TestCase("192.168.01.10")]
        [TestCase("192.168.1.256")]
        [TestCase("224.0.0.1")]
        [TestCase("255.255.255.255")]
        [TestCase("0.0.0.0")]
        [TestCase("192.168.1.1 ")]
        public void RejectsNonliteralOrNonunicastInput(string text)
        { Assert.That(LanAddress.TryParse(text, out _), Is.False); }
    }
}
