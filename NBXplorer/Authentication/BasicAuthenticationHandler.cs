using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace NBXplorer.Authentication
{
	public class BasicAuthenticationHandler(
		IOptionsMonitor<BasicAuthenticationOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: AuthenticationHandler<BasicAuthenticationOptions>(options, logger, encoder)
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			if(Options.Username == null)
			{
				var user = new GenericPrincipal(new GenericIdentity("Anonymous", Scheme.Name), null);
				var ticket = new AuthenticationTicket(user, new AuthenticationProperties(), Scheme.Name);
				return Task.FromResult(AuthenticateResult.Success(ticket));
			}

			if(!Request.Headers.TryGetValue("Authorization", out var values))
				return Task.FromResult(AuthenticateResult.NoResult());

			if(values.Count != 1 ||
			   !AuthenticationHeaderValue.TryParse(values[0], out var authorization) ||
			   !authorization.Scheme.Equals(Scheme.Name, StringComparison.OrdinalIgnoreCase) ||
			   string.IsNullOrWhiteSpace(authorization.Parameter))
				return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header."));

			string usernamePassword;
			try
			{
				usernamePassword = Encoding.Latin1.GetString(Convert.FromBase64String(authorization.Parameter));
			}
			catch(FormatException)
			{
				return Task.FromResult(AuthenticateResult.Fail("Invalid Basic credentials."));
			}

			int separatorIndex = usernamePassword.IndexOf(':');
			if(separatorIndex < 0)
				return Task.FromResult(AuthenticateResult.Fail("Invalid Basic credentials."));

			var username = usernamePassword.Substring(0, separatorIndex);
			var password = usernamePassword.Substring(separatorIndex + 1);

			if(username.Equals(Options.Username, StringComparison.Ordinal) && PasswordEquals(password, Options.Password))
			{
				var user = new GenericPrincipal(new GenericIdentity(Options.Username, Scheme.Name), null);
				var ticket = new AuthenticationTicket(user, new AuthenticationProperties(), Scheme.Name);
				return Task.FromResult(AuthenticateResult.Success(ticket));
			}

			return Task.FromResult(AuthenticateResult.Fail("No valid user."));
		}

		protected override Task HandleChallengeAsync(AuthenticationProperties properties)
		{
			Response.Headers.Append("WWW-Authenticate", Scheme.Name);
			return base.HandleChallengeAsync(properties);
		}

		private static bool PasswordEquals(string password, string expectedPassword)
		{
			if(expectedPassword == null)
				return false;

			return CryptographicOperations.FixedTimeEquals(
				SHA256.HashData(Encoding.UTF8.GetBytes(password)),
				SHA256.HashData(Encoding.UTF8.GetBytes(expectedPassword)));
		}
	}
}
