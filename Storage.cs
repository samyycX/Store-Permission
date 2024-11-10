using Dapper;
using MySqlConnector;

namespace StorePermission;

public class DatabaseRecord
{
  public ulong id { get; set; }
  public ulong steamid { get; set; }
  public string item { get; set; } = "";
}

public class StorePermissionStorage
{
  private Dictionary<ulong, List<string>> _PermissionItems { get; set; } = new();

  private string DbConnString { get; set; } = "";

  public StorePermissionStorage(string ip, int port, string user, string password, string database)
  {
    DbConnString = $"server={ip};port={port};user={user};password={password};database={database};Pooling=true;MinimumPoolSize=0;MaximumPoolsize=640;ConnectionIdleTimeout=30;AllowUserVariables=true";
    var connection = new MySqlConnection(DbConnString);
    connection.Execute($"""
      CREATE TABLE IF NOT EXISTS `store_permissions` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `steamid` BIGINT UNSIGNED NOT NULL,
          `item` VARCHAR(255) NOT NULL,
          INDEX `steamid` (`steamid`)
      );
    """);
    GetAllRecords().ForEach(record =>
    {
      if (_PermissionItems.ContainsKey(record.steamid))
      {
        _PermissionItems[record.steamid].Add(record.item);
      }
      else
      {
        _PermissionItems.Add(record.steamid, new List<string> { record.item });
      }
    });
  }

  public async Task<MySqlConnection> ConnectAsync()
  {
    MySqlConnection connection = new(DbConnString);
    await connection.OpenAsync();
    return connection;
  }

  public void ExecuteAsync(string query, object? parameters)
  {
    Task.Run(async () =>
    {
      using MySqlConnection connection = await ConnectAsync();
      await connection.ExecuteAsync(query, parameters);
    });
  }

  public List<string> GetPermissionItems(ulong steamid)
  {
    if (_PermissionItems.ContainsKey(steamid))
    {
      return _PermissionItems[steamid];
    }
    return new List<string>();
  }

  public void AddPermissionItem(ulong steamid, string item)
  {
    if (_PermissionItems.ContainsKey(steamid))
    {
      _PermissionItems[steamid].Add(item);
    }
    else
    {
      _PermissionItems.Add(steamid, new List<string> { item });
    }
    ExecuteAsync("INSERT INTO store_permissions (steamid, item) VALUES (@steamid, @item)", new { steamid, item });
  }

  public bool HasPermissionItem(ulong steamid, string item)
  {
    if (_PermissionItems.ContainsKey(steamid))
    {
      return _PermissionItems[steamid].Contains(item);
    }
    return false;
  }

  private List<DatabaseRecord> GetAllRecords()
  {
    using MySqlConnection connection = ConnectAsync().Result;
    return (List<DatabaseRecord>)connection.Query<DatabaseRecord>("SELECT * FROM store_permissions");
  }

}