using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MineImatorSimplyRemadeNuxi;

public static class TerrainAtlas
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
        string basePath = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string terrainPath = Path.Combine(basePath, "data", "minecraft", "versions", "1.3.2", "terrain.png");

        if (!File.Exists(terrainPath))
        {
            Console.WriteLine($"Terrain atlas not found at: {terrainPath}");
            return;
        }

        using var stream = File.OpenRead(terrainPath);
        var atlasTexture = Texture2D.FromStream(_graphicsDevice, stream);

        int atlasSize = AtlasTiles * TileSize;
        if (atlasTexture.Width != atlasSize || atlasTexture.Height != atlasSize)
        {
            Console.WriteLine($"Terrain atlas size mismatch. Expected {atlasSize}x{atlasSize}, got {atlasTexture.Width}x{atlasTexture.Height}");
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