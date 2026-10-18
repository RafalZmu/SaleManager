using SaleManeger.Models;

namespace SaleManegerTests;

/// <summary>The shared reader for the amount of an order line.</summary>
internal class OrderValueTests
{
    [TestCase("3", 3)]
    [TestCase("2,5", 2.5)]
    [TestCase("2.5", 2.5)]
    [TestCase("2.5 kg", 2.5)]
    [TestCase("  4  szt bez skórki", 4)]
    [TestCase("0", 0)]
    public void Parse_ReadsFirstWordWithEitherSeparator(string text, double expected) =>
        Assert.That(OrderValue.Parse(text), Is.EqualTo(expected));

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("abc")]
    [TestCase("bring a bag")]
    public void Parse_UnreadableText_IsZeroAndTryParseFails(string? text)
    {
        Assert.That(OrderValue.Parse(text!), Is.EqualTo(0));
        Assert.That(OrderValue.TryParse(text!, out _), Is.False);
    }

    [Test]
    public void Sum_AddsAllLinesAndIgnoresUnreadableOnes() =>
        Assert.That(OrderValue.Sum(new[] { "1,5", "2 kg", "x", "" }), Is.EqualTo(3.5));

    [Test]
    public void IsComment_MatchesOnlyTheCommentMarker()
    {
        Assert.That(OrderValue.IsComment(new ClientOrder { ProductID = "Comment" }), Is.True);
        Assert.That(OrderValue.IsComment(new ClientOrder { ProductID = "abc" }), Is.False);
        Assert.That(OrderValue.IsComment(new ClientOrder { ProductID = null }), Is.False);
    }
}
