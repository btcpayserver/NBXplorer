using NBitcoin;

namespace NBXplorer
{
    public partial class NBXplorerNetworkProvider
    {
        private void InitLitecoinCash(ChainName networkType)
        {
            Add(new NBXplorerNetwork(NBitcoin.Altcoins.LitecoinCash.Instance, networkType)
            {
                MinRPCVersion = 160400
            });
        }

        public NBXplorerNetwork GetLCC()
        {
            return GetFromCryptoCode(NBitcoin.Altcoins.LitecoinCash.Instance.CryptoCode);
        }
    }
}
