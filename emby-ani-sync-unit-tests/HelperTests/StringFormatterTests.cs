using System;
using emby_ani_sync.Helpers;
using NUnit.Framework;

namespace emby_ani_sync_unit_tests.HelperTests;

public class StringFormatterTests
{
    [Test]
    [TestCase("t3&s7!-", "t3s7")]
    [TestCase("!\"$%^&*", "")]
    public void RemoveSpecialCharactersFromString(string input, string expected) =>
        Assert.AreEqual(StringFormatter.RemoveSpecialCharacters(input), expected);

    [Test]
    [TestCase(
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit",
        "Loremipsumdolorsitamet,consecteturadipiscingelit"
    )]
    [TestCase("! \"12 3abc%& $^", "!\"123abc%&$^")]
    public void RemoveSpacesFromString(string input, string expected) =>
        Assert.AreEqual(StringFormatter.RemoveSpaces(input), expected);
}
