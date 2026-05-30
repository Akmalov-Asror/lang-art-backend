using LangArt.Api.Features.Gamification;

namespace LangArt.Api.Tests;

public class LevelCalculatorTests
{
    [Theory]
    [InlineData(1, 50)]
    [InlineData(2, 141)]
    [InlineData(5, 559)]
    [InlineData(25, 6250)]
    [InlineData(50, 17677)]
    public void GetXpForLevel_matches_reference_table(int level, int expectedXp)
    {
        Assert.Equal(expectedXp, LevelCalculator.GetXpForLevel(level));
    }

    [Fact]
    public void GetXpForLevel_level_10_yields_1581_not_the_brief_typo_of_1580()
    {
        // The sprint brief lists 1580 but floor(50 * 10^1.5) = floor(1581.13…) = 1581.
        // We follow the formula; brief typo noted in docs/sprint-1-completed.md.
        Assert.Equal(1581, LevelCalculator.GetXpForLevel(10));
    }

    [Fact]
    public void GetXpForLevel_zero_or_negative_is_zero()
    {
        Assert.Equal(0, LevelCalculator.GetXpForLevel(0));
        Assert.Equal(0, LevelCalculator.GetXpForLevel(-1));
        Assert.Equal(0, LevelCalculator.GetXpForLevel(-100));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(49, 0)]
    [InlineData(50, 1)]
    [InlineData(140, 1)]
    [InlineData(141, 2)]
    [InlineData(558, 4)]
    [InlineData(559, 5)]
    [InlineData(6249, 24)]
    [InlineData(6250, 25)]
    public void GetLevelFromXp_lands_on_canonical_levels(int totalXp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, LevelCalculator.GetLevelFromXp(totalXp));
    }

    [Fact]
    public void GetLevelFromXp_is_consistent_with_GetXpForLevel_for_first_30_levels()
    {
        for (int n = 1; n <= 30; n++)
        {
            var floor = LevelCalculator.GetXpForLevel(n);
            // Exactly the floor → level n.
            Assert.Equal(n, LevelCalculator.GetLevelFromXp(floor));
            // One XP below floor → still level n-1.
            Assert.Equal(n - 1, LevelCalculator.GetLevelFromXp(floor - 1));
        }
    }

    [Fact]
    public void Progress_returns_xp_in_level_and_xp_for_next_level()
    {
        // 200 XP: level 2 (floor 141), 59 XP into level 2, next level is 3 (floor 259), so 118 XP wide.
        var (level, inLevel, nextLevelWidth) = LevelCalculator.Progress(200);
        Assert.Equal(2, level);
        Assert.Equal(200 - 141, inLevel);
        Assert.Equal(LevelCalculator.GetXpForLevel(3) - 141, nextLevelWidth);
    }
}
