public class IssueLinkTests
{
    /// <summary>
    /// A path with a '#' or an '&amp;' in it, as a failure's message carries, is still the whole
    /// title, and the body still follows it.
    /// </summary>
    [Test]
    public async Task TheTitleAndBodyAreEncoded()
    {
        var link = new IssueLink("https://github.com/Owner/App/");

        var url = new Uri(link.Url(@"Could not read C:\a#b&c.txt", "* Version: 1.2 & more"));
        var query = url.Query.TrimStart('?').Split('&').Select(_ => _.Split('=', 2)).ToDictionary(_ => _[0], _ => WebUtility.UrlDecode(_[1]));

        await Assert.That(url.GetLeftPart(UriPartial.Path)).IsEqualTo("https://github.com/Owner/App/issues/new");
        await Assert.That(url.Fragment).IsEmpty();
        await Assert.That(query["title"]).IsEqualTo(@"Could not read C:\a#b&c.txt");
        await Assert.That(query["body"]).IsEqualTo("* Version: 1.2 & more");
    }

    [Test]
    public async Task AMessageIsRaisedOnceARun()
    {
        var link = new IssueLink("https://github.com/Owner/App");

        await Assert.That(link.FirstTime("poll failed")).IsTrue();
        await Assert.That(link.FirstTime("poll failed")).IsFalse();
        await Assert.That(link.FirstTime("another")).IsTrue();
    }
}
