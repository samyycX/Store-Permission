using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using StoreApi;

namespace StorePermission;

public class StorePermission : BasePlugin, IPluginConfig<StorePermissionConfig>
{
  public override string ModuleName => "Store Module [Permissions]";
  public override string ModuleVersion => "0.0.3";
  public override string ModuleAuthor => "samyyc";

  public required IStoreApi StoreApi { get; set; }
  public StorePermissionConfig Config { get; set; } = new();
  public required StorePermissionStorage Storage { get; set; }
  public required ModelMenuManager MenuManager { get; set; } = new();
  private static StorePermission? _Instance { get; set; }

  public static StorePermission getInstance()
  {
    return _Instance!;
  }

  public override void Load(bool hotReload)
  {
    _Instance = this;
    RegisterEventHandler<EventPlayerActivate>((@event, info) =>
        {
          if (@event.Userid != null)
          {
            MenuManager.AddPlayer(@event.Userid.Slot, new ModelMenuPlayer { Player = @event.Userid, Buttons = 0 });
          }
          return HookResult.Continue;
        });

    RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
    {
      if (@event.Userid != null)
      {
        MenuManager.RemovePlayer(@event.Userid.Slot);
      }
      return HookResult.Continue;
    });
    RegisterListener<Listeners.OnTick>(() =>
    {
      MenuManager.Update();
    });
    if (hotReload)
    {
      MenuManager.ReloadPlayer();
    }
  }

  public void OnConfigParsed(StorePermissionConfig config)
  {
    Config = config;
    Storage = new StorePermissionStorage(Config.MySQL_DatabaseHost, Config.MySQL_DatabasePort, Config.MySQL_DatabaseUsername, Config.MySQL_DatabasePassword, Config.MySQL_DatabaseName);
    config.Commands.ForEach(command =>
    {
      AddCommand($"css_{command}", "Store - buy permissions", BuyCommand);
    });
  }

  public override void OnAllPluginsLoaded(bool hotReload)
  {
    StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");
  }
  public void GivePlayerPermission(CCSPlayerController player, List<string> permissions)
  {
    permissions.ForEach(permission =>
    {
      if (permission.StartsWith("@"))
      {
        AdminManager.AddPlayerPermissions(player, [permission]);
      }
      else
      {
        AdminManager.AddPlayerToGroup(player, [permission]);
      }
    });
  }
  public void GivePlayerPermissionItem(CCSPlayerController player, string itemName)
  {
    PermissionItem item = Config.Items[itemName];
    GivePlayerPermission(player, item.Permissions);
    Storage.AddPermissionItem(player.AuthorizedSteamID!.SteamId64, itemName);
  }

  public void SellPlayerPermissionItem(CCSPlayerController player, string itemName)
  {
    if (!Storage.HasPermissionItem(player.AuthorizedSteamID!.SteamId64, itemName))
      return;
    if (!Config.Items.ContainsKey(itemName))
      return;
    var item = Config.Items[itemName];
    var ratio = Config.SellRatio;
    if (ratio < 0) ratio = 0;
    if (ratio > 1) ratio = 1;
    int refund = (int)Math.Floor(item.Price * ratio);
    if (refund < 0) refund = 0;

    StoreApi.GivePlayerCredits(player, refund);
    Storage.RemovePermissionItem(player.AuthorizedSteamID!.SteamId64, itemName);
    player.PrintToChat(Localizer["sell.success", itemName, refund]);
  }

  [GameEventHandler]
  public HookResult OnPlayerJoin(EventPlayerTeam @event, GameEventInfo info)
  {
    var player = @event.Userid;
    if (player == null)
    {
      return HookResult.Continue;
    }
    var items = Storage.GetPermissionItems(player.AuthorizedSteamID!.SteamId64);
    items.ForEach(item =>
    {
      if (Config.Items.ContainsKey(item))
        GivePlayerPermission(player, Config.Items[item].Permissions);
    });
    return HookResult.Continue;
  }

  private WasdModelMenu BuildMainMenu(CCSPlayerController player)
  {
    var menu = new WasdModelMenu { Title = Localizer["menu.title"] };
    var playerAlreadyPurchases = Storage.GetPermissionItems(player.AuthorizedSteamID!.SteamId64);

    foreach (var item in Config.Items.ToList())
    {
      if (playerAlreadyPurchases.Contains(item.Key))
      {
        int refund = (int)Math.Floor(item.Value.Price * Math.Clamp(Config.SellRatio, 0, 1));
        menu.AddOption(new SubMenuOption
        {
          Text = $"{item.Key} [{Localizer["menu.bought"]}]",
          NextMenu = new WasdModelMenu
          {
            Title = Localizer["menu.manage", item.Key],
            Options = new List<MenuOption>
            {
              new SelectOption
              {
                Text = Localizer["menu.sell", refund],
                Select = (player, option, sub) =>
                {
                  SellPlayerPermissionItem(player, item.Key);
                  // Re-open refreshed main menu
                  MenuManager.OpenMainMenu(player, BuildMainMenu(player));
                }
              },
              new SelectOption
              {
                Text = Localizer["menu.back"],
                Select = (player, option, sub) =>
                {
                  MenuManager.GetPlayer(player.Slot).Prev();
                }
              }
            }
          }
        });
        continue;
      }

      // Not owned -> purchase flow
      menu.AddOption(new SubMenuOption
      {
        Text = $"{item.Key} [{item.Value.Price}]",
        NextMenu = new WasdModelMenu
        {
          Title = Localizer["menu.confirmtitle", item.Key],
          Options = new List<MenuOption>
          {
            new SelectOption
            {
              Text = Localizer["menu.confirm", item.Value.Price],
              Select = (player, option, sub) =>
              {
                if (StoreApi!.GetPlayerCredits(player) < item.Value.Price)
                {
                  player.PrintToChat(Localizer["buy.insufficientcredits", item.Key]);
                  return;
                }
                StoreApi.GivePlayerCredits(player, -item.Value.Price);
                GivePlayerPermissionItem(player, item.Key);
                player.PrintToChat(Localizer["buy.success", item.Key]);
                // Refresh main menu instead of closing
                MenuManager.OpenMainMenu(player, BuildMainMenu(player));
              }
            },
            new SelectOption
            {
              Text = Localizer["menu.cancel"],
              Select = (player, option, sub) =>
              {
                MenuManager.GetPlayer(player.Slot).Prev();
              }
            }
          }
        }
      });
    }

    return menu;
  }

  [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_ONLY)]
  public void BuyCommand(CCSPlayerController? player, CommandInfo commandInfo)
  {
    if (player == null)
    {
      return;
    }
    MenuManager.OpenMainMenu(player, BuildMainMenu(player));
  }
}