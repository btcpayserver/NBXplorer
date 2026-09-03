using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NBXplorer.Authentication;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NBXplorer.Tests
{
	public class BasicAuthenticationHandlerTests
	{
		[Theory]
		[InlineData("Basic !!!")]
		[InlineData("Basic bm9jb2xvbg==")]
		[InlineData("BasicWhatever dXNlcjpwYXNzd29yZA==")]
		public async Task MalformedHeadersFailWithoutThrowing(string header)
		{
			var result = await Authenticate(header);

			Assert.False(result.Succeeded);
			Assert.NotNull(result.Failure);
		}

		[Fact]
		public async Task CredentialsAreCaseSensitive()
		{
			var result = await Authenticate(CreateHeader("user", "PASSWORD"));

			Assert.False(result.Succeeded);
		}

		[Fact]
		public async Task ValidCredentialsAuthenticateWithConfiguredScheme()
		{
			var result = await Authenticate(CreateHeader("user", "password"));

			Assert.True(result.Succeeded);
			Assert.Equal("Basic", result.Ticket.AuthenticationScheme);
			Assert.Equal("Basic", result.Principal.Identity.AuthenticationType);
		}

		[Fact]
		public async Task ChallengeAdvertisesBasicAuthentication()
		{
			var context = CreateContext();
			context.Response.Headers.WWWAuthenticate = "Bearer";

			await context.ChallengeAsync("Basic");

			Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
			Assert.Equal(new[] { "Bearer", "Basic" }, context.Response.Headers.WWWAuthenticate);
		}

		private static async Task<AuthenticateResult> Authenticate(string header)
		{
			var context = CreateContext();
			context.Request.Headers.Authorization = header;
			return await context.AuthenticateAsync("Basic");
		}

		private static DefaultHttpContext CreateContext()
		{
			var services = new ServiceCollection()
				.AddLogging()
				.AddAuthentication("Basic")
				.AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>("Basic", options =>
				{
					options.Username = "user";
					options.Password = "password";
				})
				.Services
				.BuildServiceProvider();

			return new DefaultHttpContext { RequestServices = services };
		}

		private static string CreateHeader(string username, string password)
		{
			return "Basic " + Convert.ToBase64String(Encoding.Latin1.GetBytes($"{username}:{password}"));
		}
	}
}
