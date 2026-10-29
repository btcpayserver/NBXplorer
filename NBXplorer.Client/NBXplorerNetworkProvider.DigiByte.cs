using NBitcoin;

namespace NBXplorer
{
	public partial class NBXplorerNetworkProvider
	{
		private void InitDigiByte(ChainName networkType)
		{
			Add(new NBXplorerNetwork(NBitcoin.Altcoins.DigiByte.Instance, networkType)
			{
				// DigiByte Core 9.26.6, the first release with the Thaw Day rules (mainnet block 24,490,000).
				// Older nodes may follow a different chain after that height.
				MinRPCVersion = 92606,
				CoinType = networkType == ChainName.Mainnet ? new KeyPath("20'") : new KeyPath("1'"),
			});
		}

		public NBXplorerNetwork GetDGB()
		{
			return GetFromCryptoCode(NBitcoin.Altcoins.DigiByte.Instance.CryptoCode);
		}
	}
}
