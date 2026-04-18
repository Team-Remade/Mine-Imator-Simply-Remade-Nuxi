using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MineImatorSimplyRemadeNuxi.core.objs.nodes;
using MineImatorSimplyRemadeNuxi.Gui;
using MineImatorSimplyRemadeNuxi.ui;

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
    private static readonly string ImGuiIniPath = "imgui.ini";
    
    WorkCamera camera;
    public AppViewport Viewport;
    MenuBar _menuBar;
    SceneTree _sceneTree;
    public PropertiesPanel Properties;
    Timeline _timeline;
    private SpawnMenu _spawnMenu;
    
    // Performance optimization
    private int _fpsUpdateCounter = 0;
    private const int FPS_UPDATE_INTERVAL = 10; // Update FPS every 10 frames
    
    private bool _renderModeEnabled = false;
    
    // Debug toggle: set to true to skip the asset downloader on startup
    private const bool DebugSkipAssetDownloader = true;
    
    public const string ViewportDockId = "Viewport";
    public const string SceneTreeDockId = "Scene Tree";
    public const string PropertiesDockId = "Properties";
    public const string TimelineDockId = "Timeline";
    
    private bool _dockSpaceInitialized = false;

    public App()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override async void Initialize()
    {
        Window.Title = "Mine Imator Simply Remade: Nuxi";
        
        if (!Directory.Exists(GetUserDataPath()))
        {
            Directory.CreateDirectory(GetUserDataPath());
        }
        
        GuiRenderer = new ImGuiRenderer(this);
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        
        camera = new WorkCamera();
        camera.Initialize(GraphicsDevice);
        
        Window.AllowUserResizing = true;
        
        _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width - 200;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height - 160;
        _graphics.ApplyChanges();
        
        // SDL_MaximizeWindow(Window.Handle);
        
        Viewport = new AppViewport(camera, GraphicsDevice);
        _menuBar = new MenuBar();
        _sceneTree = new SceneTree();
        Properties = new PropertiesPanel();
        _timeline = new Timeline();
        _spawnMenu = new SpawnMenu();
        Viewport.SpawnMenu = _spawnMenu;

        TerrainAtlas.Initialize(GraphicsDevice);
        ItemsAtlas.Initialize(GraphicsDevice);

        Viewport.LoadTerrainTexture();

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

        await EnsureFFMpegAsync();

        if (!DebugSkipAssetDownloader)
            await ShowAssetDownloaderAndWait();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        Viewport.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        base.Draw(gameTime);
        
        GuiRenderer.BeforeLayout(gameTime);

        _menuBar.Render();

        ImGuiViewportPtr mainViewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(mainViewport.WorkPos);
        ImGui.SetNextWindowSize(mainViewport.WorkSize);
        ImGui.SetNextWindowViewport(mainViewport.ID);

        ImGuiWindowFlags dockWindowFlags =
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoBackground;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
        ImGui.Begin("##DockSpaceWindow", dockWindowFlags);
        ImGui.PopStyleVar(3);

        uint dockspaceId = ImGui.GetID("##MainDockSpace");
        ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);

        if (!_dockSpaceInitialized && !File.Exists(ImGuiIniPath))
        {
            SetupDefaultDockSpace(dockspaceId, mainViewport.WorkSize);
            _dockSpaceInitialized = true;
        }

        ImGui.End();
        Viewport.Render();
        _sceneTree.Render();
        Properties.Render();
        _timeline.Render();
        _spawnMenu.Render();
        
        GuiRenderer.AfterLayout();
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
    
    private unsafe void SetupDefaultDockSpace(uint dockspaceId, System.Numerics.Vector2 size)
    {
        ImGuiP.DockBuilderRemoveNode(dockspaceId);
        ImGuiP.DockBuilderAddNode(dockspaceId, ImGuiDockNodeFlags.None);
        ImGuiP.DockBuilderSetNodeSize(dockspaceId, size);
        
        // Split root into left (viewport+timeline) and right (scene tree+properties)
        uint rightId = 0;
        uint leftId = ImGuiP.DockBuilderSplitNode(dockspaceId, ImGuiDir.Left, 0.70f, null, &rightId);

        // Split left column: top = viewport, bottom = timeline
        uint timelineDockId = 0;
        uint viewportDockId = ImGuiP.DockBuilderSplitNode(leftId, ImGuiDir.Up, 0.75f, null, &timelineDockId);

        // Split right column: top = scene tree, bottom = properties
        uint propertiesDockId = 0;
        uint sceneTreeDockId = ImGuiP.DockBuilderSplitNode(rightId, ImGuiDir.Up, 0.30f, null, &propertiesDockId);

        ImGuiP.DockBuilderDockWindow(ViewportDockId, viewportDockId);
        ImGuiP.DockBuilderDockWindow(TimelineDockId, timelineDockId);
        ImGuiP.DockBuilderDockWindow(SceneTreeDockId, sceneTreeDockId);
        ImGuiP.DockBuilderDockWindow(PropertiesDockId, propertiesDockId);
        
        ImGuiP.DockBuilderFinish(dockspaceId);
    }

    private async Task EnsureFFMpegAsync()
    {
        try
        {
            var ffmpegPath = Path.Combine(GetUserDataPath(), "ffmpeg");
            Directory.CreateDirectory(ffmpegPath);
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);

            bool ffmpegAvailable = false;
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = GlobalFFOptions.GetFFMpegBinaryPath();
                process.StartInfo.Arguments = "-version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
                ffmpegAvailable = process.ExitCode == 0;
            }
            catch
            {
                ffmpegAvailable = false;
            }

            if (!ffmpegAvailable)
            {
                // Show loading UI while downloading FFmpeg
                //ShowFFmpegLoadingWindow("Downloading FFmpeg binaries...");
                Console.WriteLine("Downloading FFMpeg binaries...");
                await FFMpegDownloader.DownloadBinaries();
                //CloseFFmpegLoadingWindow();
            }
        }
        catch (Exception ex)
        {
            //CloseFFmpegLoadingWindow();
            Console.WriteLine($"Failed to ensure FFMpeg: {ex.Message}");
        }
    }

    private async Task ShowAssetDownloaderAndWait()
    {
        // TODO:
    }
}
