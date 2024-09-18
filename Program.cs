using NativeFileDialogSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

class FontPacker
{
    private static readonly Rgba32 MagentaColor = new Rgba32(255, 0, 255, 255);

    public static void Main(string[] args)
    {
        var result = NativeFileDialogSharp.Dialog.FileOpen();
        var outputPath = NativeFileDialogSharp.Dialog.FileSave();
        if (result.IsOk && outputPath.IsOk)
        {
            var inputPath = result.Path;
            using (Image<Rgba32> image = Image.Load<Rgba32>(inputPath))
            {
                List<(Rectangle Bounds, Image<Rgba32> Image)> characters = ExtractCharacters(image);
                (int newWidth, int newHeight) = CalculateNewImageSize(characters);
                using (Image<Rgba32> newImage = new Image<Rgba32>(newWidth, newHeight, MagentaColor))
                {
                    PackCharacters(newImage, characters);
                    newImage.Save(outputPath.Path, new PngEncoder
                    {
                        ColorType = PngColorType.Palette,
                        BitDepth = PngBitDepth.Bit2,
                    });
                }
            }
        }



    }

    private static List<(Rectangle Bounds, Image<Rgba32> Image)> ExtractCharacters(Image<Rgba32> image)
    {
        var characters = new List<(Rectangle Bounds, Image<Rgba32> Image)>();
        bool[,] visited = new bool[image.Width, image.Height];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (!visited[x, y] && !image[x, y].Equals(MagentaColor))
                {
                    (Rectangle bounds, Image<Rgba32> charImage) = ExtractCharacter(image, x, y, visited);
                    if (charImage != null)
                    {
                        characters.Add((bounds, charImage));
                    }
                }
            }
        }

        return characters;
    }

    private static (Rectangle, Image<Rgba32>) ExtractCharacter(Image<Rgba32> image, int startX, int startY, bool[,] visited)
    {
        int xMin = startX, xMax = startX, yMin = startY, yMax = startY;
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x);
            yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);

            foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
            {
                if (nx >= 0 && ny >= 0 && nx < image.Width && ny < image.Height &&
                    !visited[nx, ny] && !image[nx, ny].Equals(MagentaColor))
                {
                    visited[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        Rectangle bounds = new Rectangle(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        Image<Rgba32> charImage = new Image<Rgba32>(bounds.Width, bounds.Height);

        for (int y = 0; y < bounds.Height; y++)
        {
            for (int x = 0; x < bounds.Width; x++)
            {
                charImage[x, y] = image[bounds.X + x, bounds.Y + y];
            }
        }

        return (bounds, charImage);
    }

    private static (int width, int height) CalculateNewImageSize(List<(Rectangle Bounds, Image<Rgba32> Image)> characters)
    {
        int totalArea = characters.Sum(c => (c.Bounds.Width + 1) * (c.Bounds.Height + 1));
        int maxHeight = characters.Max(c => c.Bounds.Height);
        int sideLength = (int)Math.Ceiling(Math.Sqrt(totalArea));
        return (RoundUpToPowerOf2(sideLength), RoundUpToPowerOf2(Math.Max(sideLength, maxHeight)));
    }

    private static int RoundUpToPowerOf2(int value)
    {
        return 1 << ((int)Math.Ceiling(Math.Log(value, 2)));
    }

    private static void PackCharacters(Image<Rgba32> newImage, List<(Rectangle Bounds, Image<Rgba32> Image)> characters)
    {
        int x = 1, y = 1, rowHeight = 0;  // Start at (1,1) to ensure top and left borders
        foreach (var (bounds, charImage) in characters.OrderByDescending(c => c.Bounds.Height))
        {
            if (x + bounds.Width + 1 > newImage.Width)  // +1 for right border
            {
                x = 1;  // Reset to left border
                y += rowHeight + 1;  // Move to next row, including bottom border of previous row
                rowHeight = 0;
            }

            // Copy character image to new image, surrounded by magenta border
            for (int cy = -1; cy <= bounds.Height; cy++)
            {
                for (int cx = -1; cx <= bounds.Width; cx++)
                {
                    if (cx == -1 || cy == -1 || cx == bounds.Width || cy == bounds.Height)
                    {
                        newImage[x + cx, y + cy] = MagentaColor;
                    }
                    else
                    {
                        newImage[x + cx, y + cy] = charImage[cx, cy];
                    }
                }
            }

            x += bounds.Width + 1;  // Move to next position, including right border
            rowHeight = Math.Max(rowHeight, bounds.Height);
        }
    }
}