using Jellyfin.Plugin.FilenameTitlePlugin;
using Xunit;

namespace FilenameTitlePlugin.Tests;

public class FilenameCleanerServiceTests
{
    private readonly FilenameCleanerService _sut = new();

    [Theory]
    [InlineData("The.Dark.Knight.mkv", "The Dark Knight")]
    [InlineData("Breaking_Bad_S01E01.mp4", "Breaking Bad S01E01")]
    [InlineData("Movie.2024.1080p.BluRay.x264.mkv", "Movie 2024")]
    [InlineData("www.EzTvX.to_The.Dark.Knight.2008.1080p.BluRay.x264.mkv", "The Dark Knight 2008")]
    [InlineData("The.Office.S03E05.720p.WEB-DL.AAC.x264.mp4", "The Office S03E05")]
    [InlineData("Inception.2010.4K.UHD.BluRay.HEVC.TrueHD.Atmos.mkv", "Inception 2010")]
    [InlineData("The.Dark.Knight", "The Dark Knight")]
    public void Clean_ReturnsExpectedTitle(string input, string expected)
    {
        Assert.Equal(expected, _sut.Clean(input));
    }

    [Fact]
    public void Clean_ExtensionOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.Clean(".mkv"));
    }

    [Fact]
    public void Clean_YearInBrackets_UnwrappedNotRemoved()
    {
        Assert.Equal("Interstellar 2014", _sut.Clean("Interstellar.(2014).mkv"));
    }

    [Fact]
    public void Clean_YearInSquareBrackets_UnwrappedNotRemoved()
    {
        Assert.Equal("Dune 2021", _sut.Clean("Dune.[2021].2160p.mkv"));
    }

    [Fact]
    public void Clean_MultipleSpacesCollapsed()
    {
        Assert.Equal("Some Movie", _sut.Clean("Some...Movie.mkv"));
    }
}
