using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.SqlClient;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.FrontGuard
{
    [Collection("IIS Integration Tests")]
    public class GenerateAndSetTokenEndToEndTests
    {
        private readonly IisIntegrationFixture _fixture;

        public GenerateAndSetTokenEndToEndTests(IisIntegrationFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }
    }
}
