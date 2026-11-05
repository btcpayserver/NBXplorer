using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using NBitcoin.Protocol;
using NBXplorer.Backend;
using NBXplorer.Configuration;
using NBXplorer.Events;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NBXplorer.Tests
{
	public class IndexerConnectionTests
	{
		// Accepts TCP connections and never answers, like a node that stopped responding.
		sealed class SilentPeer : IDisposable
		{
			readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
			readonly List<TcpClient> _clients = new List<TcpClient>();
			public SilentPeer()
			{
				_listener.Start();
				_ = AcceptLoop();
			}
			public IPEndPoint EndPoint => (IPEndPoint)_listener.LocalEndpoint;
			async Task AcceptLoop()
			{
				try
				{
					while (true)
					{
						var client = await _listener.AcceptTcpClientAsync();
						lock (_clients)
							_clients.Add(client);
					}
				}
				catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
				{
				}
			}
			public void Dispose()
			{
				_listener.Stop();
				lock (_clients)
					foreach (var client in _clients)
						client.Dispose();
			}
		}

		static Indexer CreateIndexer(EndPoint nodeEndpoint)
		{
			return new Indexer(
				null,
				NullLogger.Instance,
				new NBXplorerNetworkProvider(ChainName.Regtest).GetBTC(),
				null,
				null,
				null,
				new ExplorerConfiguration(),
				new ChainConfiguration() { NodeEndpoint = nodeEndpoint },
				new EventAggregator(NullLoggerFactory.Instance));
		}

		static Task<Node> Connect(SilentPeer peer)
		{
			return Node.ConnectAsync(Network.RegTest, peer.EndPoint, new NodeConnectionParameters());
		}

		static Task WaitDisconnected(Node node, Action<Node> handler)
		{
			var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			node.Disconnected += n =>
			{
				handler(n);
				disconnected.TrySetResult();
			};
			return disconnected.Task.WaitAsync(TimeSpan.FromSeconds(10));
		}

		[Fact]
		public async Task DisconnectOfTheOldNodeDoesNotDisposeTheNewConnection()
		{
			using var peer = new SilentPeer();
			var indexer = CreateIndexer(peer.EndPoint);
			var oldNode = await Connect(peer);
			var newNode = await Connect(peer);
			var oldConnection = new Indexer.Connection(oldNode);
			var newConnection = new Indexer.Connection(newNode);
			indexer.State = BitcoinDWaiterState.NBXplorerSynching;
			var disconnected = WaitDisconnected(oldNode, indexer.Node_Disconnected);

			// ConnectNode swaps the connection while the old node's Disconnected event is still pending.
			indexer._Connection = newConnection;
			oldConnection.Dispose("Creating new connection");
			await disconnected;

			Assert.True(newNode.IsConnected);
			Assert.False(newConnection.Events.Reader.Completion.IsCompleted);
			Assert.Equal(BitcoinDWaiterState.NBXplorerSynching, indexer.State);
			newConnection.Dispose();
		}

		[Fact]
		public async Task DisconnectOfTheCurrentNodeDisposesItsConnection()
		{
			using var peer = new SilentPeer();
			var indexer = CreateIndexer(peer.EndPoint);
			var node = await Connect(peer);
			var connection = new Indexer.Connection(node);
			indexer._Connection = connection;
			indexer.State = BitcoinDWaiterState.Ready;
			var disconnected = WaitDisconnected(node, indexer.Node_Disconnected);

			node.DisconnectAsync("Lost the node");
			await disconnected;

			await connection.Events.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(10));
			Assert.Equal(BitcoinDWaiterState.NotStarted, indexer.State);
		}

		[Fact]
		public async Task ConnectNodeTimesOutWhenTheHandshakeNeverFinishes()
		{
			using var peer = new SilentPeer();
			var indexer = CreateIndexer(peer.EndPoint);
			indexer.HandshakeTimeout = TimeSpan.FromMilliseconds(200);

			var connecting = indexer.ConnectNode(CancellationToken.None);

			var finished = await Task.WhenAny(connecting, Task.Delay(TimeSpan.FromSeconds(10)));
			indexer._Connection?.Dispose();
			Assert.Same(connecting, finished);
			await Assert.ThrowsAsync<TimeoutException>(() => connecting);
		}
	}
}
