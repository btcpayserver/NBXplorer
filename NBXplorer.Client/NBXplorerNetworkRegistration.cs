using NBitcoin;
using NBXplorer.DerivationStrategy;
using System;

namespace NBXplorer
{
	/// <summary>
	/// Describes a conventional <see cref="INetworkSet"/>-based external network to register with an
	/// <see cref="NBXplorerNetworkProvider"/>. Networks that require a specialized
	/// <see cref="NBXplorerNetwork"/> subclass are not supported by this registration contract.
	/// Registration supplies client-side metadata only and does not configure an NBXplorer server/indexer.
	/// </summary>
	public sealed class NBXplorerNetworkRegistration
	{
		/// <summary>
		/// Creates a registration for a network set. Its crypto code must contain only uppercase ASCII letters and digits.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="networkSet"/> is null.</exception>
		public NBXplorerNetworkRegistration(INetworkSet networkSet)
		{
			NetworkSet = networkSet ?? throw new ArgumentNullException(nameof(networkSet));
		}

		/// <summary>The NBitcoin network set to register.</summary>
		public INetworkSet NetworkSet { get; }

		/// <summary>The minimum supported node RPC version. The default is 0.</summary>
		public int MinRPCVersion { get; set; }

		/// <summary>The BIP44 coin type, or null when no coin type is defined.</summary>
		public KeyPath CoinType { get; set; }

		/// <summary>Whether cookie authentication is supported. The default is true.</summary>
		public bool SupportCookieAuthentication { get; set; } = true;

		/// <summary>The maximum initial chain-loading duration, or null to use the client default.</summary>
		public TimeSpan? ChainLoadingTimeout { get; set; }

		/// <summary>The maximum chain-cache loading duration, or null to use the client default.</summary>
		public TimeSpan? ChainCacheLoadingTimeout { get; set; }

		/// <summary>The minimum number of blocks to retain, or null to use the client default.</summary>
		public int? MinBlocksToKeep { get; set; }

		/// <summary>
		/// Optionally configures the derivation strategy factory before the network is published.
		/// </summary>
		public Action<DerivationStrategyFactory> ConfigureDerivationStrategyFactory { get; set; }
	}
}
