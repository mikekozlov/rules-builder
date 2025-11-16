using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.NUnit3;

namespace Prosperity.Api.Infrastructure.Storages.Tests;

public sealed class AutoMoqDataAttribute : AutoDataAttribute
{
    public AutoMoqDataAttribute()
        : base(() => new Fixture().Customize(new AutoMoqCustomization
        {
            ConfigureMembers = true
        }))
    {
    }
}
