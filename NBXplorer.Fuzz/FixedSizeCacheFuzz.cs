// Coverage-guided fuzz target for NBXplorer's FixedSizeCache<TValue, TKey>
// using SharpFuzz (AFL/libFuzzer for .NET).
//
// Usage:
//
//   1. Build:
//      dotnet build -c Release
//
//   2. Instrument with SharpFuzz:
//      dotnet tool install --global SharpFuzz.CommandLine
//      sharpfuzz bin/Release/net10.0/NBXplorer.Fuzz.dll
//
//   3. Create seed corpus:
//      mkdir -p corpus findings
//      printf '\x01\x00\x00\x00' > corpus/seed
//
//   4. Fuzz with AFL++:
//      afl-fuzz -i corpus/ -o findings/ -t 5000 \
//        -- dotnet bin/Release/net10.0/NBXplorer.Fuzz.dll
//
// Single-input replay (crash triage):
//   dotnet run -c Release --no-build -- <input-file>

using System;
using System.IO;
using System.Linq;
using System.Text;
using NBXplorer;
using SharpFuzz;

namespace NBXplorer.Fuzz;

// ---------------------------------------------------------------------------
// FuzzableKey: a key whose GetHashCode() is directly controlled by the
// fuzzer. Simulates uint256.GetHashCode() which mixes all 32 bytes of a
// txid and can return any int32 value, including int.MinValue.
// ---------------------------------------------------------------------------

public class FuzzableKey
{
	public readonly int HashCode;
	public readonly byte[] Data;

	public FuzzableKey(int hashCode, byte[] data)
	{
		HashCode = hashCode;
		Data = data ?? [];
	}

	public override int GetHashCode() => HashCode;

	public override bool Equals(object obj) =>
		obj is FuzzableKey other && other.HashCode == HashCode && other.Data.SequenceEqual(Data);
}

// ---------------------------------------------------------------------------
// Fuzz target
// ---------------------------------------------------------------------------

public static class FixedSizeCacheFuzz
{
	private static readonly int[] EdgeCaseHashCodes =
	[
		int.MinValue,
		int.MinValue + 1,
		int.MaxValue,
		int.MaxValue - 1,
		0,
		-1,
		1,
	];

	public static void Run(ReadOnlySpan<byte> fuzzData)
	{
		if (fuzzData.Length < 12)
			return;

		var data = fuzzData.ToArray();
		int offset = 0;

		int ReadByte()
		{
			if (offset >= data.Length)
				return 0;
			return data[offset++];
		}

		int ReadInt32()
		{
			if (offset + 4 > data.Length)
				return 0;
			int val = BitConverter.ToInt32(data, offset);
			offset += 4;
			return val;
		}

		byte[] ReadBytes(int maxLen)
		{
			int avail = data.Length - offset;
			int len = Math.Min(maxLen, avail);
			if (len <= 0)
				return [];
			var buf = new byte[len];
			Array.Copy(data, offset, buf, 0, len);
			offset += len;
			return buf;
		}

		// Cache size: 1 to 10000 (unsigned arithmetic, no Math.Abs)
		int cacheSize = (int)((uint)ReadInt32() % 10000) + 1;

		// Number of operations: 1 to 500
		int numOps = (int)((uint)ReadInt32() % 500) + 1;

		var cache = new FixedSizeCache<string, FuzzableKey>(
			cacheSize,
			s => new FuzzableKey(StableHash(s), Encoding.UTF8.GetBytes(s ?? ""))
		);

		for (int i = 0; i < numOps; i++)
		{
			if (offset >= data.Length)
				break;

			int op = ReadByte() % 3;

			FuzzableKey key;
			bool useEdgeCase = (ReadByte() % 4) == 0;

			if (useEdgeCase)
			{
				int idx = ReadByte() % EdgeCaseHashCodes.Length;
				int hc = EdgeCaseHashCodes[idx];
				var keyData = ReadBytes(ReadByte() % 32);
				key = new FuzzableKey(hc, keyData);
			}
			else
			{
				int hc = ReadInt32();
				var keyData = ReadBytes(ReadByte() % 28);
				key = new FuzzableKey(hc, keyData);
			}

			string value = $"fuzz_{i}_{key.HashCode}";

			switch (op)
			{
				case 0:
					cache.Add(value);
					break;
				case 1:
					cache.Contains(value);
					break;
				case 2:
					cache.Remove(value);
					break;
			}
		}
	}

	private static int StableHash(string s)
	{
		if (string.IsNullOrEmpty(s))
			return 0;
		unchecked
		{
			int hash = 17;
			foreach (char c in s)
				hash = hash * 31 + c;
			return hash;
		}
	}
}

public class Program
{
	public static void Main(string[] args)
	{
		if (args.Length > 0 && !args[0].StartsWith("--"))
		{
			// Replay a single input file (for crash triage)
			var data = File.ReadAllBytes(args[0]);
			FixedSizeCacheFuzz.Run(data);
			Console.WriteLine("Input completed without crash.");
			return;
		}

		// SharpFuzz OutOfProcess mode
		Fuzzer.OutOfProcess.Run(stream =>
		{
			try
			{
				using var memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				FixedSizeCacheFuzz.Run(memoryStream.ToArray());
			}
			catch (ArgumentNullException)
			{
				// Expected for null inputs
			}
			catch (ArgumentOutOfRangeException)
			{
				// Expected for edge-case parameters
			}
		});
	}
}
