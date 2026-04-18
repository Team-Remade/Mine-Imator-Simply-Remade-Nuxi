using System;
using System.IO;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MineImatorSimplyRemadeNuxi.ui;
using MonoGame.ImGuiNet;

namespace MineImatorSimplyRemadeNuxi;

public class App : Game
{
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowIcon(IntPtr window, IntPtr icon);

    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_CreateRGBSurfaceFrom(IntPtr pixels, int width, int height, int depth, int pitch, uint rmask, uint gmask, uint bmask, uint amask);

    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_FreeSurface(IntPtr surface);

    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_MaximizeWindow(IntPtr window);
    
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    public static ImGuiRenderer GuiRenderer;
    private static readonly string LocalPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string ApplicationLocalDirectory = "SimplyRemadeNuxi";
    
    Camera camera;
    AppViewport _viewport;

    public App()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        Window.Title = "Mine Imator Simply Remade: Nuxi";
        
        if (!Directory.Exists(GetUserDataPath()))
        {
            Directory.CreateDirectory(GetUserDataPath());
        }
        
        GuiRenderer = new ImGuiRenderer(this);
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        
        camera = new Camera();
        camera.Initialize(GraphicsDevice);
        
        Window.AllowUserResizing = true;
        
        _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width - 200;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height - 200;
        _graphics.ApplyChanges();
        
        // SDL_MaximizeWindow(Window.Handle);
        
        _viewport = new AppViewport(camera, GraphicsDevice);

        if (new Random().Next(1000) == 777)
        {
            Texture2D texture = Content.Load<Texture2D>("assets/img/chegg");
            SetWindowIcon(texture);
        }
        else
        {
            Texture2D texture = Content.Load<Texture2D>("assets/img/tamari");
            SetWindowIcon(texture);
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        
        GuiRenderer.RebuildFontAtlas();
    }

    protected override void Update(GameTime gameTime)
    {
        _viewport.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        base.Draw(gameTime);
        
        GuiRenderer.BeginLayout(gameTime);

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport());
        
        _viewport.Render();
        
        GuiRenderer.EndLayout();
    }

    private void SetWindowIcon(Texture2D texture)
    {
        var pixels = new byte[texture.Width * texture.Height * 4];
        texture.GetData(pixels);

        bool isLittleEndian = BitConverter.IsLittleEndian;
        uint rmask = isLittleEndian ? 0x000000FFu : 0xFF000000u;
        uint gmask = isLittleEndian ? 0x0000FF00u : 0x00FF0000u;
        uint bmask = isLittleEndian ? 0x00FF0000u : 0x0000FF00u;
        uint amask = isLittleEndian ? 0xFF000000u : 0x000000FFu;

        unsafe
        {
            fixed (byte* ptr = pixels)
            {
                IntPtr surface = SDL_CreateRGBSurfaceFrom((IntPtr)ptr, texture.Width, texture.Height, 32, texture.Width * 4, rmask, gmask, bmask, amask);
                if (surface != IntPtr.Zero)
                {
                    SDL_SetWindowIcon(Window.Handle, surface);
                    SDL_FreeSurface(surface);
                }
            }
        }
    }
    
    public static string GetUserDataPath()
    {
        return Path.Combine(LocalPath, ApplicationLocalDirectory);
    }
}
