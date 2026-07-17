////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using Steamworks;

namespace SDG.Unturned
{
	public class Command : System.IComparable<Command>
	{
		protected Local localization;

		protected string _command;
		public string command => _command;

		protected string _info;
		public string info => _info;

		protected string _help;
		public string help => _help;

		protected virtual void execute(CSteamID executorID, string parameter)
		{ }

		protected bool TryGetAdminPlayer(CSteamID executorID, out SteamPlayer steamPlayer)
		{
			steamPlayer = PlayerTool.getSteamPlayer(executorID);
			if (steamPlayer == null || steamPlayer.player == null)
			{
				CommandWindow.LogError("Command requires a player executor.");
				return false;
			}

			if (!steamPlayer.isAdmin && executorID != SteamAdminlist.ownerID)
			{
				ChatManager.say(executorID, "Admin or owner permission required.", Palette.SERVER, EChatMode.SAY);
				return false;
			}

			return true;
		}

		protected void SendFeedback(CSteamID executorID, string message)
		{
			if (executorID == CSteamID.Nil)
				CommandWindow.Log(message);
			else
				ChatManager.say(executorID, message, Palette.SERVER, EChatMode.SAY);
		}

		public virtual bool check(CSteamID executorID, string method, string parameter)
		{
			if (method.ToLower() == command.ToLower())
			{
				execute(executorID, parameter);

				return true;
			}

			return false;
		}

		public int CompareTo(Command other)
		{
			return command.CompareTo(other.command);
		}
	}

	public class CommandFly : Command
	{
		protected override void execute(CSteamID executorID, string parameter)
		{
			if (!Provider.isServer || !TryGetAdminPlayer(executorID, out SteamPlayer steamPlayer))
				return;

			bool enabled = !steamPlayer.player.movement.enableFly;
			steamPlayer.player.movement.sendEnableFly(enabled);
			SendFeedback(executorID, enabled ? "Fly enabled." : "Fly disabled.");
		}

		public CommandFly(Local newLocalization)
		{
			localization = newLocalization;
			_command = "fly";
			_info = "/fly or @fly";
			_help = "Toggles flight for admin or owner.";
		}
	}

	public class CommandGod : Command
	{
		protected override void execute(CSteamID executorID, string parameter)
		{
			if (!Provider.isServer || !TryGetAdminPlayer(executorID, out SteamPlayer steamPlayer))
				return;

			PlayerLife life = steamPlayer.player.life;
			life.enableGodMode = !life.enableGodMode;
			if (life.enableGodMode)
			{
				life.serverSetLegsBroken(false);
			}
			SendFeedback(executorID, life.enableGodMode ? "God mode enabled." : "God mode disabled.");
		}

		public CommandGod(Local newLocalization)
		{
			localization = newLocalization;
			_command = "god";
			_info = "/god or @god";
			_help = "Toggles damage immunity for admin or owner.";
		}
	}

	public class CommandHeal : Command
	{
		protected override void execute(CSteamID executorID, string parameter)
		{
			if (!Provider.isServer || !TryGetAdminPlayer(executorID, out SteamPlayer steamPlayer))
				return;

			PlayerLife life = steamPlayer.player.life;
			if (life.isDead)
			{
				SendFeedback(executorID, "Cannot heal while dead.");
				return;
			}

			life.askHeal(100, true, true);
			SendFeedback(executorID, "Health, bleeding, and broken legs restored.");
		}

		public CommandHeal(Local newLocalization)
		{
			localization = newLocalization;
			_command = "heal";
			_info = "/heal or @heal";
			_help = "Restores health, bleeding, and broken legs for admin or owner.";
		}
	}

	public class CommandSpeed : Command
	{
		private static bool TryParseMultiplier(string parameter, out int multiplier)
		{
			return int.TryParse(parameter, out multiplier) && multiplier >= 1 && multiplier <= 50;
		}

		protected override void execute(CSteamID executorID, string parameter)
		{
			if (!Provider.isServer || !TryGetAdminPlayer(executorID, out SteamPlayer steamPlayer))
				return;

			if (!TryParseMultiplier(parameter, out int multiplier))
			{
				SendFeedback(executorID, "Usage: /speed <1-50>");
				return;
			}

			steamPlayer.player.movement.sendPluginSpeedMultiplier(multiplier);
			SendFeedback(executorID, $"Speed multiplier set to {multiplier}x.");
		}

		public CommandSpeed(Local newLocalization)
		{
			localization = newLocalization;
			_command = "speed";
			_info = "/speed <1-50> or @speed <1-50>";
			_help = "Sets movement speed multiplier for admin or owner.";
		}
	}
}
