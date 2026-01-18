using Xunit;

namespace IISFrontGuard.Module.IntegrationTests
{
    /// <summary>
    /// Custom Fact attribute that automatically skips integration tests when the environment is not configured.
    /// Use this instead of [Fact] in integration test classes.
    /// </summary>
    public sealed class IntegrationTestFactAttribute : FactAttribute
    {
        public IntegrationTestFactAttribute()
        {
            if (!string.IsNullOrEmpty(IisIntegrationFixture.SkipReason))
            {
                Skip = IisIntegrationFixture.SkipReason;
            }
        }
    }

    /// <summary>
    /// Custom Theory attribute that automatically skips integration tests when the environment is not configured.
    /// Use this instead of [Theory] in integration test classes.
    /// </summary>
    public sealed class IntegrationTestTheoryAttribute : TheoryAttribute
    {
        public IntegrationTestTheoryAttribute()
        {
            if (!string.IsNullOrEmpty(IisIntegrationFixture.SkipReason))
            {
                Skip = IisIntegrationFixture.SkipReason;
            }
        }
    }
}
