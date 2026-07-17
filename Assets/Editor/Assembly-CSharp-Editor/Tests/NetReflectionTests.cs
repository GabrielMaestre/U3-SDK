////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using NUnit.Framework;
using SDG.Unturned;
using System.Reflection;

internal class NetReflectionTests
{
	[Test]
	public void AdminInventoryMethodsAreRegistered()
	{
		BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
		MethodInfo getServerMethod = typeof(NetReflection).GetMethod("GetServerMethodInfo", flags);
		MethodInfo getClientMethod = typeof(NetReflection).GetMethod("GetClientMethodInfo", flags);
		Assert.IsNotNull(getServerMethod);
		Assert.IsNotNull(getClientMethod);

		Assert.IsNotNull(getServerMethod.Invoke(null, new object[] { typeof(PlayerAdminInventoryUI), nameof(PlayerAdminInventoryUI.ReceiveAccessRequest) }));
		Assert.IsNotNull(getServerMethod.Invoke(null, new object[] { typeof(PlayerAdminInventoryUI), nameof(PlayerAdminInventoryUI.ReceiveOpenRequest) }));
		Assert.IsNotNull(getServerMethod.Invoke(null, new object[] { typeof(PlayerAdminInventoryUI), nameof(PlayerAdminInventoryUI.ReceiveGiveRequest) }));
		Assert.IsNotNull(getClientMethod.Invoke(null, new object[] { typeof(PlayerAdminInventoryUI), nameof(PlayerAdminInventoryUI.ReceiveAccessGranted) }));
		Assert.IsNotNull(getClientMethod.Invoke(null, new object[] { typeof(PlayerAdminInventoryUI), nameof(PlayerAdminInventoryUI.ReceiveOpen) }));
	}
}
