using static Roblox.Globals;

var part = Instance.New<Part>();
part.Touched.Connect(hit =>
{
    print(hit.Name);
});
