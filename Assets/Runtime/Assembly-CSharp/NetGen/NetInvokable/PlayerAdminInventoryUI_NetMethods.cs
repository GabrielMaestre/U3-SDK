#if UNITY_EDITOR || DEVELOPMENT_BUILD || DEBUG_NETINVOKABLES
#define LOG_INVOKE_READ_ERRORS
#endif // UNITY_EDITOR || DEVELOPMENT_BUILD || DEBUG_NETINVOKABLES
using SDG.NetPak;
namespace SDG.Unturned
{
	[NetInvokableGeneratedClass(typeof(PlayerAdminInventoryUI))]
	public static class PlayerAdminInventoryUI_NetMethods
	{
		// ReceiveAccessRequest read will be called directly.
		// ReceiveAccessRequest write will be called directly.
		[NetInvokableGeneratedMethod(nameof(PlayerAdminInventoryUI.ReceiveAccessGranted), ENetInvokableGeneratedMethodPurpose.Read)]
		public static void ReceiveAccessGranted_Read(in ClientInvocationContext context)
		{
			NetPakReader reader = context.reader;
			PlayerAdminInventoryUI.ReceiveAccessGranted();
		}
		[NetInvokableGeneratedMethod(nameof(PlayerAdminInventoryUI.ReceiveAccessGranted), ENetInvokableGeneratedMethodPurpose.Write)]
		public static void ReceiveAccessGranted_Write(NetPakWriter writer)
		{
		}
		// ReceiveOpenRequest read will be called directly.
		// ReceiveOpenRequest write will be called directly.
		[NetInvokableGeneratedMethod(nameof(PlayerAdminInventoryUI.ReceiveOpen), ENetInvokableGeneratedMethodPurpose.Read)]
		public static void ReceiveOpen_Read(in ClientInvocationContext context)
		{
			NetPakReader reader = context.reader;
			PlayerAdminInventoryUI.ReceiveOpen();
		}
		[NetInvokableGeneratedMethod(nameof(PlayerAdminInventoryUI.ReceiveOpen), ENetInvokableGeneratedMethodPurpose.Write)]
		public static void ReceiveOpen_Write(NetPakWriter writer)
		{
		}
		[NetInvokableGeneratedMethod(nameof(PlayerAdminInventoryUI.ReceiveGiveRequest), ENetInvokableGeneratedMethodPurpose.Read)]
		public static void ReceiveGiveRequest_Read(in ServerInvocationContext context)
		{
			NetPakReader reader = context.reader;
			System.Guid itemGuid;
#if LOG_INVOKE_READ_ERRORS
			bool itemGuid_ReadSuccess =
#endif // LOG_INVOKE_READ_ERRORS
			reader.ReadGuid(out itemGuid);
#if LOG_INVOKE_READ_ERRORS
			if (!itemGuid_ReadSuccess)
			{
				context.ReadParameterFailed(nameof(itemGuid));
				return;
			}
#endif // LOG_INVOKE_READ_ERRORS
			PlayerAdminInventoryUI.ReceiveGiveRequest(context, itemGuid);
		}
		[NetInvokableGeneratedMethod(nameof(PlayerAdminInventoryUI.ReceiveGiveRequest), ENetInvokableGeneratedMethodPurpose.Write)]
		public static void ReceiveGiveRequest_Write(NetPakWriter writer, System.Guid itemGuid)
		{
			writer.WriteGuid(itemGuid);
		}
	}
}
