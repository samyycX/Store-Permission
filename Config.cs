using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace StorePermission;

public class StorePermissionConfig : BasePluginConfig
{
    public string MySQL_DatabaseHost { get; set; } = "localhost";
    public string MySQL_DatabaseUsername { get; set; } = "";
    public string MySQL_DatabasePassword { get; set; } = "";
    public int MySQL_DatabasePort { get; set; } = 3306;
    public string MySQL_DatabaseName { get; set; } = "";

    public List<string> Commands { get; set; } = ["bp"];

    // Global sell ratio (refund percentage of original price when selling an owned permission item)
    public double SellRatio { get; set; } = 0.5; // default

    // Change from field to property so it can be deserialized from JSON.
    [JsonPropertyName("Items")]
    public Dictionary<string, PermissionItem> Items { get; set; } = new()
    {
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