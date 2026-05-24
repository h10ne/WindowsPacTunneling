var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var pngPath = Path.Combine(root, "WindowPacTunneling", "Resources", "source-icon.png");
var icoPath = Path.Combine(root, "WindowPacTunneling", "Resources", "app.ico");

if (!File.Exists(pngPath))
{
    Console.Error.WriteLine($"PNG not found: {pngPath}");
    return 1;
}

WindowPacTunneling.Resources.AppIconBuilder.BuildFromPng(pngPath, icoPath);
Console.WriteLine($"Icon written: {icoPath}");
return 0;
