using CounterStrikeSharp.API.Core;

namespace StorePermission;

public class Utils
{
  public static Dictionary<string, PermissionItem> GetPlayerAvailablePermissionItems(CCSPlayerController player)
  {
    var items = StorePermission.getInstance().Storage.GetPermissionItems(player.AuthorizedSteamID!.SteamId64);
    if (items.Count == 0)
    {
      return StorePermission.getInstance().Config.Items;
    }
    return StorePermission.getInstance().Config.Items.Where(item => !items.Contains(item.Key)).ToDictionary();
  }

}