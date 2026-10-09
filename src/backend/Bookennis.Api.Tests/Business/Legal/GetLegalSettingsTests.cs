using Bookennis.Api.Business.Legal;
using Bookennis.Api.Config;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Legal;

public class GetLegalSettingsTests
{
    [Fact]
    public async Task ReturnsTheConfiguredOperatorDetails()
    {
        var settings = new AppSettings
        {
            Legal = new LegalSettings
            {
                OperatorName = " Example Operator ",
                Street = "Example Street 1",
                ZipCode = "1010",
                City = "Vienna",
                Country = "Austria",
                Email = "operator@example.com",
                Phone = "+43 1 234567",
                HostingProvider = "Example Hosting",
                EmailProvider = "Example Mail"
            }
        };

        var result = await new GetLegalSettings.Handler(settings).Handle(new GetLegalSettings(), CancellationToken.None);

        result.OperatorName.Should().Be("Example Operator");
        result.Street.Should().Be("Example Street 1");
        result.ZipCode.Should().Be("1010");
        result.City.Should().Be("Vienna");
        result.Country.Should().Be("Austria");
        result.Email.Should().Be("operator@example.com");
        result.Phone.Should().Be("+43 1 234567");
        result.HostingProvider.Should().Be("Example Hosting");
        result.EmailProvider.Should().Be("Example Mail");
    }

    [Fact]
    public async Task NothingConfigured_ReturnsEmptyDetails()
    {
        var result = await new GetLegalSettings.Handler(new AppSettings()).Handle(new GetLegalSettings(), CancellationToken.None);

        result.OperatorName.Should().BeEmpty();
        result.Email.Should().BeEmpty();
        result.Phone.Should().BeNull();
    }
}
