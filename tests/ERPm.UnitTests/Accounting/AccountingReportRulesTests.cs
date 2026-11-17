using ERPm.Infrastructure.Services.Accounting;
using Xunit;

namespace ERPm.UnitTests.Accounting;

public sealed class AccountingReportRulesTests
{
    [Theory]
    [InlineData(-10, "Current")]
    [InlineData(0, "Current")]
    [InlineData(1, "1-30")]
    [InlineData(30, "1-30")]
    [InlineData(31, "31-60")]
    [InlineData(60, "31-60")]
    [InlineData(61, "61-90")]
    [InlineData(90, "61-90")]
    [InlineData(91, ">90")]
    [InlineData(180, ">90")]
    public void ClassifyAgingBucket_MapsDaysOverdueToExpectedTier(int daysOverdue, string expectedBucket)
    {
        Assert.Equal(expectedBucket, AccountingReportService.ClassifyAgingBucket(daysOverdue));
    }
}
