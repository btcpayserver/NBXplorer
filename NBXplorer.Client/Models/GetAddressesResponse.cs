using NBitcoin;
using System;

namespace NBXplorer.Models
{
	public class GetAddressesResponse
	{
		public BitcoinAddress[] Addresses { get; set; } = Array.Empty<BitcoinAddress>();
		public string Continuation { get; set; }
	}
}
