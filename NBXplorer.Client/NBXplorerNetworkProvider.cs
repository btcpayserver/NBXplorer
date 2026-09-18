using NBitcoin;
using System;
using System.Collections.Generic;

namespace NBXplorer
{
	/// <summary>
	/// Provides the client-side network metadata used to communicate with NBXplorer instances.
	/// Registering a network here does not configure or enable that network in an NBXplorer server/indexer.
	/// </summary>
    public partial class NBXplorerNetworkProvider
    {
		public NBXplorerNetworkProvider(ChainName networkType)
		{
			NetworkType = networkType;
			InitArgoneum(networkType);
			InitBitcoin(networkType);
			InitBitcore(networkType);
			InitLitecoin(networkType);
			InitDogecoin(networkType);
			InitPepecoin(networkType);
			InitBCash(networkType);
			InitGroestlcoin(networkType);
			InitBGold(networkType);
			InitDash(networkType);
			InitTerracoin(networkType);
			InitPolis(networkType);
			InitMonacoin(networkType);
			InitFeathercoin(networkType);
			InitUfo(networkType);
			InitViacoin(networkType);
			InitMonoeci(networkType);
			InitGobyte(networkType);
			InitColossus(networkType);
			InitChaincoin(networkType);
			InitLiquid(networkType);
			InitQtum(networkType);
			InitAlthash(networkType);
			InitMonetaryUnit(networkType);
			foreach (var chain in _Networks.Values)
			{
				chain.DerivationStrategyFactory ??= chain.CreateStrategyFactory();
			}
		}

		public ChainName NetworkType
		{
			get;
			private set;
		}

		public NBXplorerNetwork GetFromCryptoCode(string cryptoCode)
		{
			lock (_NetworksLock)
			{
				_Networks.TryGetValue(cryptoCode, out NBXplorerNetwork network);
				return network;
			}
		}

		public IEnumerable<NBXplorerNetwork> GetAll()
		{
			lock (_NetworksLock)
			{
				return new List<NBXplorerNetwork>(_Networks.Values);
			}
		}

		/// <summary>
		/// Closes the startup registration window. The application host should call this after every
		/// startup component has had an opportunity to register its networks. Calling this method more
		/// than once has no effect. A registration whose configuration callback is still running when
		/// this method is called is rejected instead of being published. The host owns this lifecycle
		/// boundary; reading from the provider does not close registration.
		/// </summary>
		public void CompleteRegistration()
		{
			lock (_NetworksLock)
			{
				_RegistrationCompleted = true;
			}
		}

		/// <summary>
		/// Registers an additional network during application startup.
		/// Registration must complete before services, clients, or serializers cache the provider's network set.
		/// </summary>
		/// <param name="registration">The complete network registration.</param>
		/// <returns>The initialized network.</returns>
		/// <remarks>
		/// Registration values are captured when this method begins. The optional configuration callback
		/// runs outside the provider lock. Exceptions from the network set or callback propagate without
		/// publishing a partial network, and the crypto code may be retried unless registration has completed.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="registration"/> is null.</exception>
		/// <exception cref="ArgumentException">The registration has an invalid crypto code.</exception>
		/// <exception cref="InvalidOperationException">Registration has completed, or the crypto code is already registered or being registered.</exception>
		/// <exception cref="NotSupportedException">The network set does not support this provider's chain.</exception>
		public NBXplorerNetwork RegisterNetwork(NBXplorerNetworkRegistration registration)
		{
			if (registration == null)
				throw new ArgumentNullException(nameof(registration));
			var networkSet = registration.NetworkSet;
			var minRPCVersion = registration.MinRPCVersion;
			var coinType = registration.CoinType;
			var supportCookieAuthentication = registration.SupportCookieAuthentication;
			var chainLoadingTimeout = registration.ChainLoadingTimeout;
			var chainCacheLoadingTimeout = registration.ChainCacheLoadingTimeout;
			var minBlocksToKeep = registration.MinBlocksToKeep;
			var configureDerivationStrategyFactory = registration.ConfigureDerivationStrategyFactory;
			var cryptoCode = networkSet.CryptoCode;
			if (!IsValidCryptoCode(cryptoCode))
				throw new ArgumentException("The network crypto code must contain only uppercase ASCII letters and digits.", nameof(registration));

			lock (_NetworksLock)
			{
				ThrowIfRegistrationCompleted();
				if (_Networks.ContainsKey(cryptoCode) || !_RegistrationsInProgress.Add(cryptoCode))
					throw new InvalidOperationException($"The network '{cryptoCode}' is already registered or being registered.");
			}

			try
			{
				var network = new NBXplorerNetwork(networkSet, NetworkType);
				if (!StringComparer.Ordinal.Equals(cryptoCode, network.CryptoCode))
					throw new ArgumentException("The network crypto code must remain stable during registration.", nameof(registration));
				if (network.NBitcoinNetwork == null)
					throw new NotSupportedException($"The network set '{cryptoCode}' does not support chain '{NetworkType}'.");

				network.MinRPCVersion = minRPCVersion;
				network.CoinType = coinType;
				network.SupportCookieAuthentication = supportCookieAuthentication;
				if (chainLoadingTimeout is TimeSpan configuredChainLoadingTimeout)
					network.ChainLoadingTimeout = configuredChainLoadingTimeout;
				if (chainCacheLoadingTimeout is TimeSpan configuredChainCacheLoadingTimeout)
					network.ChainCacheLoadingTimeout = configuredChainCacheLoadingTimeout;
				if (minBlocksToKeep is int configuredMinBlocksToKeep)
					network.MinBlocksToKeep = configuredMinBlocksToKeep;

				network.DerivationStrategyFactory = network.CreateStrategyFactory();
				configureDerivationStrategyFactory?.Invoke(network.DerivationStrategyFactory);

				lock (_NetworksLock)
				{
					ThrowIfRegistrationCompleted();
					if (_Networks.ContainsKey(cryptoCode))
						throw new InvalidOperationException($"The network '{cryptoCode}' is already registered.");
					_Networks.Add(cryptoCode, network);
					return network;
				}
			}
			finally
			{
				lock (_NetworksLock)
				{
					_RegistrationsInProgress.Remove(cryptoCode);
				}
			}
		}

		static bool IsValidCryptoCode(string cryptoCode)
		{
			if (string.IsNullOrEmpty(cryptoCode))
				return false;
			foreach (var character in cryptoCode)
			{
				if ((character < 'A' || character > 'Z') && (character < '0' || character > '9'))
					return false;
			}
			return true;
		}

		readonly object _NetworksLock = new object();
		readonly Dictionary<string, NBXplorerNetwork> _Networks = new Dictionary<string, NBXplorerNetwork>(StringComparer.OrdinalIgnoreCase);
		readonly HashSet<string> _RegistrationsInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		bool _RegistrationCompleted;

		void ThrowIfRegistrationCompleted()
		{
			if (_RegistrationCompleted)
				throw new InvalidOperationException("Network registration has already completed.");
		}

		private void Add(NBXplorerNetwork network)
		{
			if (network.NBitcoinNetwork == null)
				return;
			lock (_NetworksLock)
			{
				_Networks.Add(network.CryptoCode, network);
			}
		}
	}
}
