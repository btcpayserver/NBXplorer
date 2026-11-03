using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBXplorer.Backend;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NBXplorer.HostedServices
{
	public class DatabaseSetupHostedService : IHostedService
	{
		public DatabaseSetupHostedService(ILoggerFactory loggerFactory,  DbConnectionFactory connectionFactory)
		{
			Logger = loggerFactory.CreateLogger("NBXplorer.DatabaseSetup");
			ConnectionFactory = connectionFactory;
		}

		public ILogger Logger { get; }
		public DbConnectionFactory ConnectionFactory { get; }

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			Logger.LogInformation("Postgres services activated");
			var dsBuilder = ConnectionFactory.CreateDataSourceBuilder(b => b.CommandTimeout = Constants.TenHours);
			var retryDelay = 0;
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					try
					{
						await using var ds = dsBuilder.Build();
						await using var conn = await ds.OpenConnectionAsync(cancellationToken);
						await RunScripts(conn, cancellationToken);
						return;
					}
					catch (NpgsqlException pgex) when (pgex.SqlState == PostgresErrorCodes.InvalidCatalogName)
					{
						string dbname = string.Empty;
						await using var ds = ConnectionFactory.CreateDataSourceBuilder(b =>
						{
							dbname = b.Database;
							Logger.LogInformation($"Database '{dbname}' doesn't exists, creating it...");
							b.Database = null;
						}).Build();
						await using var conn = await ds.OpenConnectionAsync(cancellationToken);
						await conn.ExecuteAsync(new CommandDefinition($"CREATE DATABASE {dbname} TEMPLATE 'template0' LC_CTYPE 'C' LC_COLLATE 'C' ENCODING 'UTF8'", cancellationToken: cancellationToken));
					}
				}
				catch (NpgsqlException ex) when (ex.IsTransient && !cancellationToken.IsCancellationRequested)
				{
					retryDelay = Math.Min(retryDelay + 1, 5);
					Logger.LogWarning("Waiting for Postgres ({Host}:{Port}): {Message}. Retrying in {Delay} seconds",
						dsBuilder.ConnectionStringBuilder.Host, dsBuilder.ConnectionStringBuilder.Port,
						ex.GetBaseException().Message, retryDelay);
					await Task.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken);
				}
			}
		}

		private async Task RunScripts(NpgsqlConnection conn, CancellationToken cancellationToken)
		{
			await using (conn)
			{
				if (conn.PostgreSqlVersion.Major <= 11)
					Logger.LogWarning($"You are using postgres {conn.PostgreSqlVersion.Major}, this major release reached end-of-life (EOL) and is no longer supported, use at your own risks. (https://www.postgresql.org/support/versioning/)");

				HashSet<string> executed;
				try
				{
					executed = (await conn.QueryAsync<string>(new CommandDefinition("SELECT script_name FROM nbxv1_migrations", cancellationToken: cancellationToken))).ToHashSet();
				}
				catch (Npgsql.NpgsqlException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
				{
					executed = new HashSet<string>();
				}
				foreach (var resource in System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames()
																 .Where(n => n.EndsWith(".sql", System.StringComparison.InvariantCulture))
																 .OrderBy(n => n))
				{
					var parts = resource.Split('.');
					if (!int.TryParse(parts[^3], NumberStyles.Any, CultureInfo.InvariantCulture, out _))
						continue;
					var scriptName = $"{parts[^3]}.{parts[^2]}";
					if (executed.Contains(scriptName))
						continue;
					if (scriptName == "018.FastWalletRecent" && conn.PostgreSqlVersion.Major <= 11)
					{
						Logger.LogWarning($"Skipping script {scriptName} because it is not supported by postgres {conn.PostgreSqlVersion.Major}");
						continue;
					}

					var stream = System.Reflection.Assembly.GetExecutingAssembly()
														   .GetManifestResourceStream(resource);
					string content = null;
					using (var reader = new StreamReader(stream, Encoding.UTF8))
					{
						content = reader.ReadToEnd();
					}
					Logger.LogInformation($"Execute script {scriptName}...");
					await conn.ExecuteAsync(new CommandDefinition($"{content}; INSERT INTO nbxv1_migrations VALUES (@scriptName)", new { scriptName }, cancellationToken: cancellationToken));
				}
				await conn.ReloadTypesAsync(cancellationToken);
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			Npgsql.NpgsqlConnection.ClearAllPools();
			return Task.CompletedTask;
		}
	}
}
