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

  public static StorePermission getInstance() => _Instance!;

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

    RegisterListener<Listeners.OnTick>(() => { MenuManager.Update(); });

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

  // 原路撤销: 与添加时的逻辑对称
  public void RemovePlayerPermission(CCSPlayerController player, List<string> permissions)
  {
    permissions.ForEach(permission =>
    {
      try
      {
        if (permission.StartsWith("@"))
        {
          AdminManager.RemovePlayerPermissions(player, [permission]);
        }
        else
        {
          // removeInheritedFlags=true: 同时撤销该组附带的 flags
          AdminManager.RemovePlayerFromGroup(player, true, permission);
        }
      }
      catch { }
    });
  }

  public void GivePlayerPermissionItem(CCSPlayerController player, string itemName)
  {
    var item = Config.Items[itemName];
    GivePlayerPermission(player, item.Permissions);
    Storage.AddPermissionItem(player.AuthorizedSteamID!.SteamId64, itemName);
  }

  public void RemovePlayerPermissionItem(CCSPlayerController player, string itemName)
  {
    if (!Config.Items.ContainsKey(itemName)) return;
    var item = Config.Items[itemName];
    RemovePlayerPermission(player, item.Permissions);
  }

  public void SellPlayerPermissionItem(CCSPlayerController player, string itemName)
  {
    if (!Storage.HasPermissionItem(player.AuthorizedSteamID!.SteamId64, itemName)) return;
    if (!Config.Items.ContainsKey(itemName)) return;

    var item = Config.Items[itemName];
    var ratio = Math.Clamp(Config.SellRatio, 0, 1);
    int refund = (int)Math.Floor(item.Price * ratio);
    if (refund < 0) refund = 0;

    StoreApi.GivePlayerCredits(player, refund);
    RemovePlayerPermissionItem(player, itemName);
    Storage.RemovePermissionItem(player.AuthorizedSteamID!.SteamId64, itemName);
    player.PrintToChat(Localizer["sell.success", itemName, refund]);
  }

  [GameEventHandler]
  public HookResult OnPlayerJoin(EventPlayerTeam @event, GameEventInfo info)
  {
    var player = @event.Userid;
    if (player == null) return HookResult.Continue;

    var items = Storage.GetPermissionItems(player.AuthorizedSteamID!.SteamId64);
    items.ForEach(itemName =>
    {
      if (Config.Items.ContainsKey(itemName))
        GivePlayerPermission(player, Config.Items[itemName].Permissions);
    });
    return HookResult.Continue;
  }

  private WasdModelMenu BuildMainMenu(CCSPlayerController player)
  {
    var menu = new WasdModelMenu { Title = Localizer["menu.title"] };
    var owned = Storage.GetPermissionItems(player.AuthorizedSteamID!.SteamId64);

    foreach (var kv in Config.Items.ToList())
    {
      if (owned.Contains(kv.Key))
      {
        int refund = (int)Math.Floor(kv.Value.Price * Math.Clamp(Config.SellRatio, 0, 1));
        menu.AddOption(new SubMenuOption
        {
          Text = $"{kv.Key} [{Localizer["menu.bought"]}]",
          NextMenu = new WasdModelMenu
          {
            Title = Localizer["menu.manage", kv.Key],
            Options = new List<MenuOption>
            {
              new SelectOption
              {
                Text = Localizer["menu.sell", refund],
                Select = (p,o,sub)=>
                {
                  SellPlayerPermissionItem(p, kv.Key);
                  MenuManager.OpenMainMenu(p, BuildMainMenu(p));
                }
              },
              new SelectOption
              {
                Text = Localizer["menu.back"],
                Select = (p,o,sub)=> { MenuManager.GetPlayer(p.Slot).Prev(); }
              }
            }
          }
        });
        continue;
      }

      menu.AddOption(new SubMenuOption
      {
        Text = $"{kv.Key} [{kv.Value.Price}]",
        NextMenu = new WasdModelMenu
        {
          Title = Localizer["menu.confirmtitle", kv.Key],
          Options = new List<MenuOption>
          {
            new SelectOption
            {
              Text = Localizer["menu.confirm", kv.Value.Price],
              Select = (p,o,sub)=>
              {
                if (StoreApi!.GetPlayerCredits(p) < kv.Value.Price)
                {
                  p.PrintToChat(Localizer["buy.insufficientcredits", kv.Key]);
                  return;
                }
                StoreApi.GivePlayerCredits(p, -kv.Value.Price);
                GivePlayerPermissionItem(p, kv.Key);
                p.PrintToChat(Localizer["buy.success", kv.Key]);
                MenuManager.OpenMainMenu(p, BuildMainMenu(p));
              }
            },
            new SelectOption
            {
              Text = Localizer["menu.cancel"],
              Select = (p,o,sub)=> { MenuManager.GetPlayer(p.Slot).Prev(); }
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
    if (player == null) return;
    MenuManager.OpenMainMenu(player, BuildMainMenu(player));
  }
}