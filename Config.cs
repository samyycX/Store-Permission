using CounterStrikeSharp.API.Core;

namespace StorePermission;

public class StorePermissionConfig : BasePluginConfig
{
    public string MySQL_DatabaseHost { get; set; } = "localhost";
    public string MySQL_DatabaseUsername { get; set; } = "";
    public string MySQL_DatabasePassword { get; set; } = "";
    public int MySQL_DatabasePort { get; set; } = 3306;
    public string MySQL_DatabaseName { get; set; } = "";

    public List<string> Commands { get; set; } = ["bp"];

    public Dictionary<string, PermissionItem> Items = new() {
        {
            "item1", new PermissionItem {
                Price = 100,
                Permissions = new List<string> {
                    "@css/flag1",
                    "#css/group1"
                }
            }
        },
        {
            "item2", new PermissionItem {
                Price = 200,
                Permissions = new List<string> {
                    "@css/flag2"
                }
            }
        }
    };

    public PermissionItem? GetItem(string item)
    {
        return Items.ContainsKey(item) ? Items[item] : null;
    }

}