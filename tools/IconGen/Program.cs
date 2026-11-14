using SkiaSharp;
using Svg.Skia;

// Renders the logo SVGs (one per detail level) into PNG, ICO and ICNS files.
// Usage: dotnet run --project tools/IconGen -- <repo root>
var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var logoDirectory = Path.Combine(root, "assets", "logo");
var outputDirectory = Path.Combine(root, "assets", "icons");
Directory.CreateDirectory(outputDirectory);

// Pick the detail level that fits the target pixel size.
string DetailFor(int size) => size <= 24 ? "small" : size <= 64 ? "medium" : "full";

byte[] RenderPng(int size)
{
    var svgPath = Path.Combine(logoDirectory, $"quicknote-{DetailFor(size)}.svg");
    using var svg = new SKSvg();
    svg.Load(svgPath);
    var picture = svg.Picture ?? throw new InvalidOperationException($"Cannot load {svgPath}");
    using var bitmap = new SKBitmap(size, size);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(size / picture.CullRect.Width, size / picture.CullRect.Height);
    canvas.DrawPicture(picture);
    using var image = SKImage.FromBitmap(bitmap);
    return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
}

foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256, 512, 1024 })
{
    File.WriteAllBytes(Path.Combine(outputDirectory, $"quicknote-{size}.png"), RenderPng(size));
}

// ICO with PNG-compressed entries.
var icoSizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
using (var ico = new MemoryStream())
using (var writer = new BinaryWriter(ico))
{
    var images = icoSizes.Select(RenderPng).ToList();
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)icoSizes.Length);
    var offset = 6 + 16 * icoSizes.Length;
    for (var i = 0; i < icoSizes.Length; i++)
    {
        writer.Write((byte)(icoSizes[i] == 256 ? 0 : icoSizes[i]));
        writer.Write((byte)(icoSizes[i] == 256 ? 0 : icoSizes[i]));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(images[i].Length);
        writer.Write(offset);
        offset += images[i].Length;
    }

    images.ForEach(writer.Write);
    File.WriteAllBytes(Path.Combine(outputDirectory, "quicknote.ico"), ico.ToArray());
}

// ICNS (PNG based chunks) for macOS.
var icnsChunks = new (string Type, int Size)[]
{
    ("icp4", 16), ("icp5", 32), ("icp6", 64), ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024),
};
using (var icns = new MemoryStream())
{
    var chunkData = icnsChunks.Select(chunk => (chunk.Type, Data: RenderPng(chunk.Size))).ToList();
    var totalLength = 8 + chunkData.Sum(chunk => 8 + chunk.Data.Length);
    icns.Write("icns"u8);
    icns.Write(ToBigEndian(totalLength));
    foreach (var (type, data) in chunkData)
    {
        icns.Write(System.Text.Encoding.ASCII.GetBytes(type));
        icns.Write(ToBigEndian(8 + data.Length));
        icns.Write(data);
    }

    File.WriteAllBytes(Path.Combine(outputDirectory, "quicknote.icns"), icns.ToArray());
}

Console.WriteLine($"Icons written to {outputDirectory}");

static byte[] ToBigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
