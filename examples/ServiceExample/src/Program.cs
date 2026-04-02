using static Roblox.Globals;

var players = game.GetService<Players>();
var replicatedStorage = game.GetService("ReplicatedStorage");
var replicatedStorageDirect = game.ReplicatedStorage;

print(players.Name);
print(replicatedStorage.Name);
print(replicatedStorageDirect.Name);
