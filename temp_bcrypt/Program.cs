using Npgsql;

var connStr = "Host=localhost;Port=5432;Database=aurafarm;Username=aurafarm;Password=aurafarm_dev_pw";
var pass = args.Length > 0 ? args[0] : "Setup1234!";
var user = args.Length > 1 ? args[1] : "eliasauinger452";
var hash = BCrypt.Net.BCrypt.HashPassword(pass);
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("UPDATE Members SET password_hash = @hash WHERE username = @user", conn);
cmd.Parameters.AddWithValue("hash", hash);
cmd.Parameters.AddWithValue("user", user);
Console.WriteLine(await cmd.ExecuteNonQueryAsync());
Console.WriteLine($"Set {user} password to: {pass}");
