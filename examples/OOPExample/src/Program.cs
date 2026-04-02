using static Roblox.Globals;

var spawner = new Spawner();
spawner.Spawn();

public class Spawner
{
    public int Count { get; set; } = 3;

    public void Spawn()
    {
        for (var index = 0; index < Count; index++)
        {
            var part = Instance.New<Part>();
            part.Name = "Spawned";
            part.Parent = workspace;
        }
    }
}
