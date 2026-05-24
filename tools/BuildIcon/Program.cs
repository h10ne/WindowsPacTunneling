var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var pngPath = Path.Combine(root, "WPT.WinForms", "Resources", "source-icon.png");
var icoPath = Path.Combine(root, "WPT.WinForms", "Resources", "app.ico");

if (!File.Exists(pngPath))
{
    Console.Error.WriteLine($"PNG not found: {pngPath}");
    return 1;
}

WPT.WinForms.Resources.AppIconBuilder.BuildFromPng(pngPath, icoPath);
Console.WriteLine($"Icon written: {icoPath}");
return 0;
