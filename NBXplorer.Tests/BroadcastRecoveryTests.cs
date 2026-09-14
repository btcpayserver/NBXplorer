using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBXplorer.Backend;
using NBXplorer.Controllers;
using NBXplorer.Models;
using Xunit;

namespace NBXplorer.Tests
{
	public class BroadcastRecoveryTests
	{
		[Fact]
		public void MissingInputRecoveryRequiresTheMissingInputsMessage()
		{
			Assert.True(MainController.ShouldRecoverMissingInputs("Missing inputs"));
			Assert.True(MainController.ShouldRecoverMissingInputs("missing inputs: parent transaction not found"));
			Assert.False(MainController.ShouldRecoverMissingInputs("Max fee exceeded"));
		}

		[Fact]
		public void RecoveryOnlySelectsDistinctDirectParentsUpToTheLimit()
		{
			var tx = Network.RegTest.CreateTransaction();
			var parents = Enumerable.Range(0, MainController.MaxBroadcastParents + 1)
				.Select(i => new uint256((ulong)(i + 1)))
				.ToArray();

			foreach (var parent in parents)
				tx.Inputs.Add(new TxIn(new OutPoint(parent, 0)));
			tx.Inputs.Add(new TxIn(new OutPoint(parents[0], 1)));

			var selected = MainController.GetBroadcastParentIds(tx);

			Assert.Equal(MainController.MaxBroadcastParents, selected.Count);
			Assert.All(selected, parent => Assert.Contains(tx.Inputs, input => input.PrevOut.Hash == parent));
			Assert.DoesNotContain(parents[^1], selected);
		}

		[Fact]
		public void RecoveryOrdersAncestorsBeforeTheirChildren()
		{
			var grandparent = Network.RegTest.CreateTransaction();
			grandparent.Outputs.Add(Money.Satoshis(3_000), Script.Empty);
			var parent = Network.RegTest.CreateTransaction();
			parent.Inputs.Add(new TxIn(new OutPoint(grandparent.GetHash(), 0)));
			parent.Outputs.Add(Money.Satoshis(2_000), Script.Empty);

			var ordered = MainController.OrderBroadcastParents(new[] { parent, grandparent });

			Assert.Equal(new[] { grandparent.GetHash(), parent.GetHash() }, ordered.Select(t => t.GetHash()));
		}

		[Fact]
		public async Task RecoveryWalksTheBoundedAncestorClosure()
		{
			var grandparent = Network.RegTest.CreateTransaction();
			grandparent.Outputs.Add(Money.Satoshis(3_000), Script.Empty);
			var parent = Network.RegTest.CreateTransaction();
			parent.Inputs.Add(new TxIn(new OutPoint(grandparent.GetHash(), 0)));
			parent.Outputs.Add(Money.Satoshis(2_000), Script.Empty);
			var child = Network.RegTest.CreateTransaction();
			child.Inputs.Add(new TxIn(new OutPoint(parent.GetHash(), 0)));
			var available = new[] { grandparent, parent }.ToDictionary(t => t.GetHash());

			var ordered = await MainController.GetBroadcastParents(child, ids =>
				Task.FromResult(ids.Where(available.ContainsKey).Select(id => available[id])));

			Assert.Equal(new[] { grandparent.GetHash(), parent.GetHash() }, ordered.Select(t => t.GetHash()));
		}

	}

	public partial class UnitTest1
	{
		[FactWithTimeout]
		public async Task BroadcastParentLookupOnlyReturnsRequestedWalletTransactions()
		{
			using var tester = CreateTester();
			tester.Client.WaitServerStarted();
			var key = new BitcoinExtKey(new ExtKey(), tester.Network);
			var strategy = tester.CreateDerivationStrategy(key.Neuter());
			await tester.Client.TrackAsync(strategy);
			var firstId = tester.SendToAddress(tester.AddressOf(key, "0/0"), Money.Satoshis(10_000));
			var secondId = tester.SendToAddress(tester.AddressOf(key, "0/1"), Money.Satoshis(20_000));
			tester.Notifications.WaitForTransaction(strategy, secondId);

			var repository = tester.GetService<RepositoryProvider>().GetRepository(tester.Network.NetworkSet.CryptoCode);
			var trackedSource = new DerivationSchemeTrackedSource(strategy);
			var selected = await repository.GetTransactions(
				GetTransactionQuery.Create(trackedSource, new[] { firstId, RandomUtils.GetUInt256() }));

			Assert.Single(selected);
			Assert.Equal(firstId, selected[0].TransactionHash);
		}
	}
}
