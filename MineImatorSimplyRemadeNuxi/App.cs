using System;
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
    public static extern void SDL_MaximizeWindow(IntPtr window);
    
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    public static ImGuiRenderer GuiRenderer;
    
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
        
        GuiRenderer = new ImGuiRenderer(this);
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        
        camera = new Camera();
        camera.Initialize(GraphicsDevice);
        
        Window.AllowUserResizing = true;
        
        _graphics.PreferredBackBufferWidth = 1920;
        _graphics.PreferredBackBufferHeight = 1080;
        _graphics.ApplyChanges();
        
        SDL_MaximizeWindow(Window.Handle);
        
        _viewport = new AppViewport(camera, GraphicsDevice);

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
}
