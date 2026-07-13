////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using NUnit.Framework;
using SDG.Unturned;
using System;
using System.IO;

internal class RiverTests
{
	[Test]
	public void ReadGuidRoundTripsWithoutAllocating()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dat");
		Guid expected = Guid.NewGuid();
		River writer = new River(path, false);
		for (int index = 0; index < 101; ++index)
		{
			writer.writeGUID(expected);
		}
		writer.closeRiver();

		River reader = new River(path, false);
		try
		{
			Assert.AreEqual(expected, reader.readGUID()); // Warm up JIT before measuring.
			long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
			bool allMatched = true;
			for (int index = 0; index < 100; ++index)
			{
				allMatched &= reader.readGUID() == expected;
			}
			long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

			Assert.IsTrue(allMatched);
			Assert.AreEqual(0, allocatedBytes);
		}
		finally
		{
			reader.closeRiver();
			File.Delete(path);
		}
	}
}
