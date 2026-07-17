////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using SDG.NetTransport;
using System;
using System.Collections.Generic;

namespace SDG.Unturned
{
	/// <summary>
	/// Lightweight admin item browser. Uses a fixed button pool rather than one UI element per asset.
	/// Item delivery and permissions are always validated by the server.
	/// </summary>
	public class PlayerAdminInventoryUI
	{
		private const int RowsPerPage = 16;
		private const string PermissionCommand = "/give";

		private struct CatalogEntry
		{
			public ItemAsset asset;
			public string searchText;

			public CatalogEntry(ItemAsset asset)
			{
				this.asset = asset;
				searchText = string.Concat(asset.id.ToString(), " ", asset.GUID.ToString("N"), " ", asset.name, " ", asset.FriendlyName);
			}
		}

		private static readonly List<CatalogEntry> catalog = new List<CatalogEntry>();
		private static readonly List<CatalogEntry> filteredCatalog = new List<CatalogEntry>();
		private static readonly List<ItemAsset> itemAssets = new List<ItemAsset>();

		private static SleekFullscreenBox container;
		private static ISleekField searchField;
		private static ISleekButton[] itemButtons;
		private static ISleekButton previousButton;
		private static ISleekButton nextButton;
		private static ISleekLabel pageLabel;
		private static int pageIndex;
		private static bool hasConfirmedAccess;
		private static bool isCatalogDirty = true;

		public static bool active { get; private set; }

		/// <summary>
		/// True after server has confirmed native or plugin permission. Used to avoid F3 vehicle-seat conflict.
		/// </summary>
		public static bool HasConfirmedAccess => hasConfirmedAccess;

		public static bool CanUseHotkeyLocally
		{
			get
			{
				if (hasConfirmedAccess || Provider.isServer)
					return true;

				return Player.LocalPlayer != null && Player.LocalPlayer.channel.owner.isAdmin;
			}
		}

		public static void HandleHotkey()
		{
			if (active)
			{
				closeAndRestoreHud();
				return;
			}

			SendOpenRequest?.Invoke(ENetReliability.Reliable);
		}

		public static void close()
		{
			if (!active)
				return;

			active = false;
			searchField.ClearFocus();
			container.AnimateOutOfView(0, 1);
		}

		private static void closeAndRestoreHud()
		{
			close();
			if (Player.LocalPlayer != null && Player.LocalPlayer.life.IsAlive)
			{
				PlayerLifeUI.open();
			}
		}

		private static void open()
		{
			if (active || Player.LocalPlayer == null || !Player.LocalPlayer.life.IsAlive || PlayerUI.window.showCursor)
				return;

			hasConfirmedAccess = true;
			refreshCatalog();
			PlayerLifeUI.close();
			PlayerPauseUI.close();
			active = true;
			container.AnimateIntoView();
			searchField.FocusControl();
		}

		private static void refreshCatalog()
		{
			if (isCatalogDirty)
			{
				itemAssets.Clear();
				Assets.find(itemAssets);
				for (int index = itemAssets.Count - 1; index >= 0; --index)
				{
					if (itemAssets[index] == null)
					{
						itemAssets.RemoveAt(index);
					}
				}
				itemAssets.Sort(compareAssets);

				catalog.Clear();
				foreach (ItemAsset asset in itemAssets)
				{
					catalog.Add(new CatalogEntry(asset));
				}
				isCatalogDirty = false;
			}

			applyFilter(searchField.Text);
		}

		private static void onAssetsRefreshed()
		{
			isCatalogDirty = true;
		}

		private static int compareAssets(ItemAsset lhs, ItemAsset rhs)
		{
			int nameComparison = string.Compare(lhs?.FriendlyName, rhs?.FriendlyName, StringComparison.InvariantCultureIgnoreCase);
			if (nameComparison != 0)
				return nameComparison;

			return (lhs?.id ?? 0).CompareTo(rhs?.id ?? 0);
		}

		private static bool matchesSearch(string searchText, string query)
		{
			return string.IsNullOrEmpty(query) || searchText.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0;
		}

		private static void applyFilter(string query)
		{
			query = query?.Trim();
			filteredCatalog.Clear();
			foreach (CatalogEntry entry in catalog)
			{
				if (matchesSearch(entry.searchText, query))
				{
					filteredCatalog.Add(entry);
				}
			}

			pageIndex = 0;
			refreshRows();
		}

		private static void refreshRows()
		{
			int pageCount = Math.Max(1, (filteredCatalog.Count + RowsPerPage - 1) / RowsPerPage);
			pageIndex = Math.Min(pageIndex, pageCount - 1);
			int firstIndex = pageIndex * RowsPerPage;

			for (int rowIndex = 0; rowIndex < RowsPerPage; ++rowIndex)
			{
				ISleekButton button = itemButtons[rowIndex];
				int itemIndex = firstIndex + rowIndex;
				bool hasItem = itemIndex < filteredCatalog.Count;
				button.IsVisible = hasItem;
				if (!hasItem)
					continue;

				ItemAsset asset = filteredCatalog[itemIndex].asset;
				button.Text = string.Format("{0}  |  {1}", asset.id, asset.FriendlyName);
				button.TooltipText = string.Format("Click to receive one\nAsset: {0}\nGUID: {1:N}", asset.name, asset.GUID);
				button.IsClickable = asset.id != 0;
			}

			previousButton.IsClickable = pageIndex > 0;
			nextButton.IsClickable = pageIndex + 1 < pageCount;
			pageLabel.Text = string.Format("Page {0}/{1}  |  {2} items", pageIndex + 1, pageCount, filteredCatalog.Count);
		}

		private static void onSearchChanged(ISleekField field, string text)
		{
			applyFilter(text);
		}

		private static void onSearchSubmitted(ISleekField field)
		{
			field.ClearFocus();
		}

		private static void onSearchEscaped(ISleekField field)
		{
			closeAndRestoreHud();
		}

		private static void onItemClicked(ISleekElement clickedElement)
		{
			int firstIndex = pageIndex * RowsPerPage;
			for (int rowIndex = 0; rowIndex < itemButtons.Length; ++rowIndex)
			{
				if (itemButtons[rowIndex] != clickedElement)
					continue;

				int itemIndex = firstIndex + rowIndex;
				if (itemIndex < filteredCatalog.Count)
				{
					ItemAsset asset = filteredCatalog[itemIndex].asset;
					if (asset.id != 0)
					{
						SendGiveRequest?.Invoke(ENetReliability.Reliable, asset.GUID);
					}
				}
				return;
			}
		}

		private static void onPreviousClicked(ISleekElement button)
		{
			if (pageIndex > 0)
			{
				--pageIndex;
				refreshRows();
			}
		}

		private static void onNextClicked(ISleekElement button)
		{
			int pageCount = Math.Max(1, (filteredCatalog.Count + RowsPerPage - 1) / RowsPerPage);
			if (pageIndex + 1 < pageCount)
			{
				++pageIndex;
				refreshRows();
			}
		}

		private static void onCloseClicked(ISleekElement button)
		{
			closeAndRestoreHud();
		}

		private static bool hasPermission(SteamPlayer player)
		{
			return player != null && player.player != null && ChatManager.hasCommandPermission(player, PermissionCommand);
		}

		private static readonly ServerStaticMethod SendAccessRequest = ServerStaticMethod.Get(ReceiveAccessRequest);
		[SteamCall(ESteamCallValidation.SERVERSIDE, ratelimitHz = 1)]
		public static void ReceiveAccessRequest(in ServerInvocationContext context)
		{
			SteamPlayer player = context.GetCallingPlayer();
			if (hasPermission(player))
			{
				SendAccessGranted?.Invoke(ENetReliability.Reliable, player.transportConnection);
			}
		}

		private static readonly ClientStaticMethod SendAccessGranted = ClientStaticMethod.Get(ReceiveAccessGranted);
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER)]
		public static void ReceiveAccessGranted()
		{
			hasConfirmedAccess = true;
		}

		private static readonly ServerStaticMethod SendOpenRequest = ServerStaticMethod.Get(ReceiveOpenRequest);
		[SteamCall(ESteamCallValidation.SERVERSIDE, ratelimitHz = 2)]
		public static void ReceiveOpenRequest(in ServerInvocationContext context)
		{
			SteamPlayer player = context.GetCallingPlayer();
			if (!hasPermission(player))
			{
				if (player != null)
				{
					ChatManager.say(player.playerID.steamID, "Admin inventory requires permission to use /give.", Palette.SERVER, EChatMode.SAY);
				}
				return;
			}

			SendOpen?.Invoke(ENetReliability.Reliable, player.transportConnection);
		}

		private static readonly ClientStaticMethod SendOpen = ClientStaticMethod.Get(ReceiveOpen);
		[SteamCall(ESteamCallValidation.ONLY_FROM_SERVER)]
		public static void ReceiveOpen()
		{
			open();
		}

		private static readonly ServerStaticMethod<Guid> SendGiveRequest = ServerStaticMethod<Guid>.Get(ReceiveGiveRequest);
		[SteamCall(ESteamCallValidation.SERVERSIDE, ratelimitHz = 10)]
		public static void ReceiveGiveRequest(in ServerInvocationContext context, Guid itemGuid)
		{
			SteamPlayer player = context.GetCallingPlayer();
			if (!hasPermission(player))
				return;

			ItemAsset asset = Assets.find<ItemAsset>(itemGuid);
			if (asset == null || asset.id == 0)
				return;

			ItemTool.tryForceGiveItem(player.player, asset.id, 1);
		}

		public PlayerAdminInventoryUI()
		{
			container = new SleekFullscreenBox();
			container.PositionScale_Y = 1;
			container.SizeScale_X = 1;
			container.SizeScale_Y = 1;
			PlayerUI.container.AddChild(container);
			active = false;
			hasConfirmedAccess = false;
			isCatalogDirty = true;
			Assets.onAssetsRefreshed -= onAssetsRefreshed;
			Assets.onAssetsRefreshed += onAssetsRefreshed;
			SendAccessRequest?.Invoke(ENetReliability.Reliable);

			ISleekBox panel = Glazier.Get().CreateBox();
			panel.PositionOffset_X = -380;
			panel.PositionOffset_Y = -300;
			panel.PositionScale_X = 0.5f;
			panel.PositionScale_Y = 0.5f;
			panel.SizeOffset_X = 760;
			panel.SizeOffset_Y = 600;
			container.AddChild(panel);

			ISleekLabel titleLabel = Glazier.Get().CreateLabel();
			titleLabel.PositionOffset_X = 10;
			titleLabel.PositionOffset_Y = 10;
			titleLabel.SizeOffset_X = 640;
			titleLabel.SizeOffset_Y = 30;
			titleLabel.Text = "Admin Inventory (F3)";
			titleLabel.FontSize = ESleekFontSize.Medium;
			panel.AddChild(titleLabel);

			ISleekButton closeButton = Glazier.Get().CreateButton();
			closeButton.PositionOffset_X = 660;
			closeButton.PositionOffset_Y = 10;
			closeButton.SizeOffset_X = 90;
			closeButton.SizeOffset_Y = 30;
			closeButton.Text = "Close";
			closeButton.OnClicked += onCloseClicked;
			panel.AddChild(closeButton);

			searchField = Glazier.Get().CreateStringField();
			searchField.PositionOffset_X = 10;
			searchField.PositionOffset_Y = 45;
			searchField.SizeOffset_X = 740;
			searchField.SizeOffset_Y = 30;
			searchField.MaxLength = 80;
			searchField.PlaceholderText = "Search name, asset name, ID or GUID";
			searchField.OnTextChanged += onSearchChanged;
			searchField.OnTextSubmitted += onSearchSubmitted;
			searchField.OnTextEscaped += onSearchEscaped;
			panel.AddChild(searchField);

			itemButtons = new ISleekButton[RowsPerPage];
			for (int rowIndex = 0; rowIndex < RowsPerPage; ++rowIndex)
			{
				ISleekButton button = Glazier.Get().CreateButton();
				button.PositionOffset_X = 10;
				button.PositionOffset_Y = 85 + rowIndex * 28;
				button.SizeOffset_X = 740;
				button.SizeOffset_Y = 26;
				button.OnClicked += onItemClicked;
				button.IsVisible = false;
				panel.AddChild(button);
				itemButtons[rowIndex] = button;
			}

			previousButton = Glazier.Get().CreateButton();
			previousButton.PositionOffset_X = 10;
			previousButton.PositionOffset_Y = 545;
			previousButton.SizeOffset_X = 120;
			previousButton.SizeOffset_Y = 35;
			previousButton.Text = "Previous";
			previousButton.OnClicked += onPreviousClicked;
			panel.AddChild(previousButton);

			pageLabel = Glazier.Get().CreateLabel();
			pageLabel.PositionOffset_X = 140;
			pageLabel.PositionOffset_Y = 545;
			pageLabel.SizeOffset_X = 480;
			pageLabel.SizeOffset_Y = 35;
			panel.AddChild(pageLabel);

			nextButton = Glazier.Get().CreateButton();
			nextButton.PositionOffset_X = 630;
			nextButton.PositionOffset_Y = 545;
			nextButton.SizeOffset_X = 120;
			nextButton.SizeOffset_Y = 35;
			nextButton.Text = "Next";
			nextButton.OnClicked += onNextClicked;
			panel.AddChild(nextButton);
		}
	}
}
