using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MineImatorSimplyRemadeNuxi;

public static class ItemsAtlas
{
    public const int TileSize = 16;
    public const int AtlasTiles = 16;
    public static readonly Dictionary<string, Texture2D> Textures = new();

    private static GraphicsDevice _graphicsDevice;

    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        LoadAtlas();
    }

    private static void LoadAtlas()
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string itemsPath = Path.Combine(basePath, "data", "minecraft", "versions", "1.3.2", "gui", "items.png");

        if (!File.Exists(itemsPath))
        {
            Console.WriteLine($"Items atlas not found at: {itemsPath}");
            return;
        }

        using var stream = File.OpenRead(itemsPath);
        var atlasTexture = Texture2D.FromStream(_graphicsDevice, stream);

        int atlasSize = AtlasTiles * TileSize;
        if (atlasTexture.Width != atlasSize || atlasTexture.Height != atlasSize)
        {
            Console.WriteLine($"Items atlas size mismatch. Expected {atlasSize}x{atlasSize}, got {atlasTexture.Width}x{atlasTexture.Height}");
            return;
        }

        var pixels = new Color[atlasSize * atlasSize];
        atlasTexture.GetData(pixels);

        for (int y = 0; y < AtlasTiles; y++)
        {
            for (int x = 0; x < AtlasTiles; x++)
            {
                var tilePixels = new Color[TileSize * TileSize];
                int startX = x * TileSize;
                int startY = y * TileSize;

                for (int ty = 0; ty < TileSize; ty++)
                {
                    for (int tx = 0; tx < TileSize; tx++)
                    {
                        tilePixels[ty * TileSize + tx] = pixels[(startY + ty) * atlasSize + (startX + tx)];
                    }
                }

                var tileTexture = new Texture2D(_graphicsDevice, TileSize, TileSize);
                tileTexture.SetData(tilePixels);

                string key = $"{x},{y}";
                Textures[key] = tileTexture;
            }
        }

        atlasTexture.Dispose();
    }
}