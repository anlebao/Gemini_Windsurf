#r "nuget: Microsoft.Data.Sqlite, 8.0.0"
using Microsoft.Data.Sqlite;

var conn = new SqliteConnection("Data Source=C:/VibeCoding/Gemini_Windsurf/5_WebApps/ShopERP/vanan_shoperp.db");
conn.Open();
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Id, TenantId, Name FROM Tenants LIMIT 10;";
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"Id: {reader["Id"]} | TenantId: {reader["TenantId"]} | Name: {reader["Name"]}");
}
conn.Close();
