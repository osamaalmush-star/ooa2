using OAI.Core.Services;
using Xunit;

namespace OAI.Tests;

public class BulletEnhancerTests
{
    [Fact]
    public void Enhances_plain_duty_into_action_plus_outcome_formula()
    {
        var enhanced = BulletEnhancer.Enhance("responsible for invoices");
        Assert.False(string.IsNullOrWhiteSpace(enhanced));
        Assert.DoesNotContain("responsible for", enhanced, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invoice", enhanced, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leaves_already_strong_bullet_intact()
    {
        var bullet = "Reduced invoice processing time 30% by automating billing APIs.";
        var enhanced = BulletEnhancer.Enhance(bullet);
        Assert.Equal(bullet, enhanced);
    }
}
