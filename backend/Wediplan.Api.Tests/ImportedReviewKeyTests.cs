using System.Linq;
using Wediplan.Api.Import;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>§Zadatak 16 — <see cref="ImportRules.ReviewKey"/>: stabilan ključ uvezene recenzije.</summary>
public class ImportedReviewKeyTests
{
    private static string Key(string slug = "foto-anic", string? author = "Ana Horvat", string? text = "Odličan fotograf, preporučam!",
        string? source = "Google", int year = 2025) => ImportRules.ReviewKey(slug, author, text, source, year);

    [Fact]
    public void Key_Is32LowercaseHexChars()
    {
        var k = Key();
        Assert.Equal(32, k.Length);
        Assert.True(k.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
    }

    [Fact]
    public void Key_IsDeterministic() => Assert.Equal(Key(), Key());

    [Fact]
    public void Key_IgnoresWhitespaceCaseAndDiacritics()
    {
        var a = Key(author: "Ana Horvat", text: "Odličan fotograf, preporučam!", source: "Google");
        var b = Key(author: "  ana   HORVAT ", text: "odlican  fotograf,\tpreporucam!", source: "GOOGLE ");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Key_DiffersWhenTextChanges() =>
        Assert.NotEqual(Key(), Key(text: "Odličan fotograf, preporučam svima!"));

    [Fact]
    public void Key_DiffersWhenAuthorSourceYearOrSlugChange()
    {
        var baseKey = Key();
        Assert.NotEqual(baseKey, Key(author: "Ivan Horvat"));
        Assert.NotEqual(baseKey, Key(source: "Facebook"));
        Assert.NotEqual(baseKey, Key(year: 2024));
        Assert.NotEqual(baseKey, Key(slug: "drugi-pruzatelj"));
    }

    [Fact]
    public void Key_TreatsNullAndEmptyAuthorTheSame() =>
        Assert.Equal(Key(author: null), Key(author: ""));
}
