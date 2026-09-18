using NBitcoin;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NBXplorer.Tests
{
	public class NBXplorerNetworkRegistrationTests
	{
		[Fact]
		public void RegistersExternalNetworkBeforeLookup()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			var registration = new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
			{
				MinRPCVersion = 123456,
				CoinType = new KeyPath("123'"),
				SupportCookieAuthentication = false,
				ChainLoadingTimeout = TimeSpan.FromMinutes(2),
				ChainCacheLoadingTimeout = TimeSpan.FromSeconds(9),
				MinBlocksToKeep = 42
			};

			var registered = provider.RegisterNetwork(registration);

			Assert.Same(registered, provider.GetFromCryptoCode("SYN"));
			Assert.Same(registered, provider.GetFromCryptoCode("syn"));
			Assert.Contains(registered, provider.GetAll());
			Assert.Equal("SYN", registered.CryptoCode);
			Assert.Same(Network.RegTest, registered.NBitcoinNetwork);
			Assert.Equal(123456, registered.MinRPCVersion);
			Assert.Equal(new KeyPath("123'"), registered.CoinType);
			Assert.False(registered.SupportCookieAuthentication);
			Assert.Equal(TimeSpan.FromMinutes(2), registered.ChainLoadingTimeout);
			Assert.Equal(TimeSpan.FromSeconds(9), registered.ChainCacheLoadingTimeout);
			Assert.Equal(42, registered.MinBlocksToKeep);
			Assert.NotNull(registered.DerivationStrategyFactory);
		}

		[Fact]
		public void PreservesBuiltInNetworksWhenRegisteringExternalNetwork()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			var builtIns = provider.GetAll().ToDictionary(network => network.CryptoCode);

			provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN")));

			foreach (var builtIn in builtIns)
			{
				var preserved = provider.GetFromCryptoCode(builtIn.Key);
				Assert.Same(builtIn.Value, preserved);
				Assert.Same(builtIn.Value.DerivationStrategyFactory, preserved.DerivationStrategyFactory);
			}
			Assert.Equal(builtIns.Count + 1, provider.GetAll().Count());
		}

		[Fact]
		public void EnumerationDoesNotCompleteRegistration()
		{
			var enumeratedProvider = new NBXplorerNetworkProvider(ChainName.Regtest);
			enumeratedProvider.GetAll().ToArray();

			var registered = enumeratedProvider.RegisterNetwork(
				new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN")));

			Assert.Same(registered, enumeratedProvider.GetFromCryptoCode("SYN"));
		}

		[Fact]
		public void RejectsRegistrationAfterExplicitCompletion()
		{
			var completedProvider = new NBXplorerNetworkProvider(ChainName.Regtest);

			completedProvider.CompleteRegistration();
			completedProvider.CompleteRegistration();
			Assert.Throws<InvalidOperationException>(() =>
				completedProvider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))));
		}

		[Fact]
		public void RejectsDuplicateCryptoCodesWithoutReplacingExistingNetwork()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			var bitcoin = provider.GetBTC();

			var callbackExecuted = false;
			var exception = Assert.Throws<InvalidOperationException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("BTC"))
				{
					ConfigureDerivationStrategyFactory = _ => callbackExecuted = true
				}));

			Assert.Contains("BTC", exception.Message, StringComparison.OrdinalIgnoreCase);
			Assert.False(callbackExecuted);
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("btc"))));
			Assert.Same(bitcoin, provider.GetBTC());
		}

		[Fact]
		public void RejectsDuplicateExternalCryptoCodeWithoutReplacingExistingNetwork()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			var registered = provider.RegisterNetwork(
				new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN")));
			var duplicateCallbackExecuted = false;

			var exception = Assert.Throws<InvalidOperationException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
				{
					ConfigureDerivationStrategyFactory = _ => duplicateCallbackExecuted = true
				}));

			Assert.Contains("SYN", exception.Message, StringComparison.OrdinalIgnoreCase);
			Assert.False(duplicateCallbackExecuted);
			Assert.Same(registered, provider.GetFromCryptoCode("SYN"));
		}

		[Fact]
		public async Task RejectsConcurrentDuplicateBeforeExecutingItsCallback()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			using var configuring = new ManualResetEventSlim();
			using var continueConfiguration = new ManualResetEventSlim();
			var duplicateCallbackExecuted = false;
			var firstRegistration = new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
			{
				ConfigureDerivationStrategyFactory = _ =>
				{
					configuring.Set();
					if (!continueConfiguration.Wait(TimeSpan.FromSeconds(10)))
						throw new TimeoutException("Timed out waiting to finish network configuration.");
				}
			};

			var firstRegistrationTask = Task.Run(() => provider.RegisterNetwork(firstRegistration));
			NBXplorerNetwork registered = null;
			try
			{
				Assert.True(configuring.Wait(TimeSpan.FromSeconds(10)));
				Assert.Throws<InvalidOperationException>(() =>
					provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
					{
						ConfigureDerivationStrategyFactory = _ => duplicateCallbackExecuted = true
					}));
				Assert.False(duplicateCallbackExecuted);
				Assert.Null(provider.GetFromCryptoCode("SYN"));
			}
			finally
			{
				continueConfiguration.Set();
				registered = await AwaitWithin(firstRegistrationTask);
			}

			Assert.Same(registered, provider.GetFromCryptoCode("SYN"));
		}

		[Fact]
		public void CallbackFailureDoesNotPublishAndAllowsRetry()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			var callbackException = new InvalidOperationException("Synthetic callback failure.");

			var thrown = Assert.Throws<InvalidOperationException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
				{
					ConfigureDerivationStrategyFactory = _ => throw callbackException
				}));

			Assert.Same(callbackException, thrown);
			Assert.Null(provider.GetFromCryptoCode("SYN"));
			Assert.DoesNotContain(provider.GetAll(), network => network.CryptoCode == "SYN");
			var registered = provider.RegisterNetwork(
				new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN")));
			Assert.Same(registered, provider.GetFromCryptoCode("SYN"));
		}

		[Fact]
		public void RejectsInvalidRegistrationsWithoutChangingNetworks()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			var builtInNetworkCount = new NBXplorerNetworkProvider(ChainName.Regtest).GetAll().Count();

			Assert.Throws<ArgumentNullException>(() => new NBXplorerNetworkRegistration(null));
			Assert.Throws<ArgumentNullException>(() => provider.RegisterNetwork(null));
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet(null))));
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet(String.Empty))));
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet(" "))));
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("syn"))));
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN TEST"))));
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN/TEST"))));
			Assert.Throws<ArgumentException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SÝN"))));
			var unsupportedCallbackExecuted = false;
			Assert.Throws<NotSupportedException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new UnsupportedNetworkSet("NONE"))
				{
					ConfigureDerivationStrategyFactory = _ => unsupportedCallbackExecuted = true
				}));
			Assert.False(unsupportedCallbackExecuted);
			var retried = provider.RegisterNetwork(
				new NBXplorerNetworkRegistration(new SyntheticNetworkSet("NONE")));
			Assert.Same(retried, provider.GetFromCryptoCode("NONE"));
			Assert.Equal(builtInNetworkCount + 1, provider.GetAll().Count());
		}

		[Fact]
		public void CapturesRegistrationValuesBeforeConfigurationCallback()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			var originalCoinType = new KeyPath("123'");
			var replacementCallbackExecuted = false;
			NBXplorerNetworkRegistration registration = null;
			registration = new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
			{
				MinRPCVersion = 123,
				CoinType = originalCoinType,
				ConfigureDerivationStrategyFactory = _ =>
				{
					registration.MinRPCVersion = 456;
					registration.CoinType = new KeyPath("456'");
					registration.ConfigureDerivationStrategyFactory = _ => replacementCallbackExecuted = true;
				}
			};

			var registered = provider.RegisterNetwork(registration);

			Assert.Equal(123, registered.MinRPCVersion);
			Assert.Equal(originalCoinType, registered.CoinType);
			Assert.False(replacementCallbackExecuted);
		}

		[Fact]
		public void DoesNotPublishARegistrationThatSealsTheProviderReentrantly()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			NBXplorerNetwork[] snapshot = null;
			var registration = new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
			{
				ConfigureDerivationStrategyFactory = _ =>
				{
					provider.CompleteRegistration();
					snapshot = provider.GetAll().ToArray();
				}
			};

			Assert.Throws<InvalidOperationException>(() => provider.RegisterNetwork(registration));
			Assert.DoesNotContain(snapshot, network => network.CryptoCode == "SYN");
			Assert.Null(provider.GetFromCryptoCode("SYN"));
		}

		[Fact]
		public async Task CompletionRejectsAConcurrentRegistrationStillBeingConfigured()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			using var configuring = new ManualResetEventSlim();
			using var continueConfiguration = new ManualResetEventSlim();
			var registration = new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
			{
				ConfigureDerivationStrategyFactory = _ =>
				{
					configuring.Set();
					if (!continueConfiguration.Wait(TimeSpan.FromSeconds(10)))
						throw new TimeoutException("Timed out waiting to finish network configuration.");
				}
			};

			var registrationTask = Task.Run(() => provider.RegisterNetwork(registration));
			try
			{
				Assert.True(configuring.Wait(TimeSpan.FromSeconds(10)));
				provider.CompleteRegistration();
			}
			finally
			{
				continueConfiguration.Set();
				await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(registrationTask));
			}

			Assert.Null(provider.GetFromCryptoCode("SYN"));
			Assert.Throws<InvalidOperationException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("OTHER"))));
		}

		[Fact]
		public async Task DoesNotPublishAPartiallyConfiguredNetwork()
		{
			var provider = new NBXplorerNetworkProvider(ChainName.Regtest);
			using var configuring = new ManualResetEventSlim();
			using var continueConfiguration = new ManualResetEventSlim();
			var registration = new NBXplorerNetworkRegistration(new SyntheticNetworkSet("SYN"))
			{
				ConfigureDerivationStrategyFactory = factory =>
				{
					configuring.Set();
					if (!continueConfiguration.Wait(TimeSpan.FromSeconds(10)))
						throw new TimeoutException("Timed out waiting to finish network configuration.");
					factory.AuthorizedOptions.Add("synthetic");
				}
			};

			var registrationTask = Task.Run(() => provider.RegisterNetwork(registration));
			NBXplorerNetwork registered = null;
			try
			{
				Assert.True(configuring.Wait(TimeSpan.FromSeconds(10)));
				var enumerationTask = Task.Run(() => provider.GetAll().ToArray());
				Assert.DoesNotContain(await AwaitWithin(enumerationTask), network => network.CryptoCode == "SYN");
			}
			finally
			{
				continueConfiguration.Set();
				registered = await AwaitWithin(registrationTask);
			}

			Assert.Same(registered, provider.GetFromCryptoCode("SYN"));
			Assert.Contains("synthetic", registered.DerivationStrategyFactory.AuthorizedOptions);
			provider.CompleteRegistration();
			Assert.Throws<InvalidOperationException>(() =>
				provider.RegisterNetwork(new NBXplorerNetworkRegistration(new SyntheticNetworkSet("OTHER"))));
		}

		private static async Task<T> AwaitWithin<T>(Task<T> task)
		{
			Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))));
			return await task;
		}

		private class SyntheticNetworkSet : INetworkSet
		{
			public SyntheticNetworkSet(string cryptoCode)
			{
				CryptoCode = cryptoCode;
			}

			public string CryptoCode { get; }
			public Network Mainnet => Network.Main;
			public Network Testnet => Network.TestNet;
			public Network Regtest => Network.RegTest;

			public virtual Network GetNetwork(ChainName chainName)
			{
				return NBitcoin.Bitcoin.Instance.GetNetwork(chainName);
			}
		}

		private sealed class UnsupportedNetworkSet : SyntheticNetworkSet
		{
			public UnsupportedNetworkSet(string cryptoCode) : base(cryptoCode)
			{
			}

			public override Network GetNetwork(ChainName chainName)
			{
				return null;
			}
		}
	}
}
