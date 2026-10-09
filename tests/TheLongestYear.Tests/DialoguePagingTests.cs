using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class DialoguePagingTests
{
    [Fact]
    public void OnTheLastLine_SetsTheLiveFlag()
        => Assert.Equal(DialoguePaging.SetLiveFlag, DialoguePaging.MarkerIndex(currentIndex: 0, lineCount: 1));

    [Fact]
    public void OnAnEarlierPage_MarksTheOldLastLine()
        => Assert.Equal(2, DialoguePaging.MarkerIndex(currentIndex: 0, lineCount: 3));

    [Fact]
    public void OnTheLastOfSeveralPages_SetsTheLiveFlag()
        => Assert.Equal(DialoguePaging.SetLiveFlag, DialoguePaging.MarkerIndex(currentIndex: 2, lineCount: 3));
}
