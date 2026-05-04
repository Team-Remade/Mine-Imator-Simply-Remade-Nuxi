using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Numerics = System.Numerics;
using MineImatorSimplyRemadeNuxi.core;
using MineImatorSimplyRemadeNuxi.core.dialogBox;
using MineImatorSimplyRemadeNuxi.core.objs.nodes;
using MineImatorSimplyRemadeNuxi.Gui;
using MineImatorSimplyRemadeNuxi.mineImator;
using MineImatorSimplyRemadeNuxi.ui;
using SDL2;

namespace MineImatorSimplyRemadeNuxi;

public class App : Game
{
    public DialogBox MessageBox;

    private GraphicsDeviceManager _graphics;
    public static ImGuiRenderer GuiRenderer;

    private static readonly string
        LocalPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string ApplicationLocalDirectory = "SimplyRemadeNuxi";
    private static readonly string ImGuiIniPath = "imgui.ini";

    WorkCamera camera;
    public AppViewport Viewport;
    MenuBar _menuBar;
    SceneTree _sceneTree;
    public PropertiesPanel Properties;
    Timeline _timeline;
    private SpawnMenu _spawnMenu;

    public static LegacyV1Save LegacySave;

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
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.ViewportsEnable;

        SelectionManager.Initialize();

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
        Properties.Initialize();
        _timeline = new Timeline();
        _spawnMenu = new SpawnMenu();
        Viewport.SpawnMenu = _spawnMenu;
        Viewport.SceneTree = _sceneTree;
        _spawnMenu.Viewport = Viewport;
        _sceneTree.Viewport = Viewport;

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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            LegacySave = new LegacyV1Save();
            
            return;
            File.Create(Path.Combine(GetUserDataPath(), ApplicationLocalDirectory)).Close();

            //var writeFile = legacySave.OpenFileWrite("D:\\Dev\\mineImators\\Mine-Imator 0.1\\file.dat");
            //legacySave.WriteByte(writeFile, 100);
            //legacySave.CloseFile(writeFile);

            var readFile = LegacySave.OpenFileRead("D:\\Dev\\mineImators\\Mine-Imator 0.1\\bruh.mani");
            var a = LegacySave.ReadByte(readFile);
            var chars = LegacySave.ReadByte(readFile);

            string text = "";

            Console.WriteLine("Version: " + a);
            text += "Version: " + a + "\n";
            Console.WriteLine("Characters: " + chars);
            text += "Characters: " + chars + "\n";
            
            for (int c = 0; c < chars; c++)
            {
                var charName = LegacySave.ReadString(readFile);
                var charSkin = LegacySave.ReadShort(readFile);
                var charVis = LegacySave.ReadByte(readFile);
                var charModel = LegacySave.ReadByte(readFile);
                var charCol = LegacySave.ReadInt(readFile);
                var posAmount = LegacySave.ReadShort(readFile);

                Console.WriteLine();

                Console.WriteLine($"Char Name: {charName}");
                Console.WriteLine($"Char Skin: {charSkin}");
                Console.WriteLine($"Char Vis: {charVis}");
                Console.WriteLine($"Char Model: {charModel}");
                Console.WriteLine($"Char Col: {charCol}");
                Console.WriteLine($"PosAmount: {posAmount}");
                text += "Char Name: "+ charName + "\n";
                text += "Char Skin: " + charSkin + "\n";
                text += "Char Model: " + charModel + "\n";
                text += "Char Col: " + charCol + "\n";
                text += "PosAmount: " + posAmount + "\n";

                for (int z = 0; z < posAmount; z++)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Pos: {z}");
                    text += $"Pos: {z}" + "\n";
                    var posePos = LegacySave.ReadShort(readFile);
                    var poseTransition = LegacySave.ReadByte(readFile);

                    Console.WriteLine($"Pose Frame: {posePos}");
                    Console.WriteLine($"Pose Transition: {poseTransition}");
                    text += $"Pose Frame: {posePos}" + "\n";
                    text += "Pose Transition: " + poseTransition + "\n";

                    for (int r = 0; r < 64; r++)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"R: {r}");

                        if (r < 8)
                        {
                            text += $"Root: {r}" + "\n";
                        }

                        else if (r < 16)
                        {
                            text += $"Body: {r}" + "\n";
                        }

                        else if (r < 24)
                        {
                            text += $"Right Arm: {r}" + "\n";
                        }
                        
                        else if (r < 32)
                        {
                            text += $"Left Arm: {r}" + "\n";
                        }
                        
                        else if (r < 40)
                        {
                            text += $"Right Leg: {r}" + "\n";
                        }
                        
                        else if (r < 48)
                        {
                            text += $"Left Leg: {r}" + "\n";
                        }

                        else if (r < 56)
                        {
                            text += $"Head: {r}" + "\n";
                        }

                        else
                        {
                            text += $"Hat: {r}" + "\n";
                        }
                        
                        var bruh = LegacySave.ReadDouble(readFile);
                        if (r % 8 == 0)
                        {
                            text += "Alpha: " + bruh + "\n";
                        }
                        else if (r % 8 == 1)
                        {
                            text += "Scale: " + bruh + "\n";
                        }
                        else if (r % 8 == 2)
                        {
                            text += "Position X: " + bruh + "\n";
                        }
                        else if (r % 8 == 3)
                        {
                            text += "Position Y: " + bruh + "\n";
                        }
                        else if (r % 8 == 4)
                        {
                            text += "Position Z: " + bruh + "\n";
                        }
                        else if (r % 8 == 5)
                        {
                            text += "Rotation X: " + bruh + "\n";
                        }
                        else if (r % 8 == 6)
                        {
                            text += "Rotation Y: " + bruh + "\n";
                        }
                        else if (r % 8 == 7)
                        {
                            text += "Rotation Z: " + bruh + "\n";
                        }
                        else
                        {
                            text += "Bruh: " + bruh + "\n";
                        }
                        Console.WriteLine($"Bruh: {bruh}");
                    }
                }
            }

            var b = LegacySave.ReadShort(readFile);

            Console.WriteLine();
            Console.WriteLine($"Skin thing: {b}");
            text += "Skin thing: " + b + "\n";
            for (int i = 1; i < b; i++)
            {
                Console.WriteLine();
                Console.WriteLine($"B: {i}");
                text += "B: " + i + "\n";
                var cr = LegacySave.ReadString(readFile);
                Console.WriteLine($"Cr: {cr}");
                text += "Cr: " + cr + "\n";
            }

            var bgselect = LegacySave.ReadShort(readFile);
            var br = LegacySave.ReadShort(readFile);

            Console.WriteLine();
            Console.WriteLine($"Bg select: {bgselect}");
            Console.WriteLine($"Br: {br}");
            
            text += "Bg select: " + bgselect + "\n";
            text += "Br: " + br + "\n";

            for (int i = 0; i < br; i++)
            {
                Console.WriteLine();
                Console.WriteLine($"B: {i}");
                text += "B: " + i + "\n";
                var cr = LegacySave.ReadString(readFile);
                Console.WriteLine($"Cr: {cr}");
                text += "Cr: " + cr + "\n";
            }

            var bgshow = LegacySave.ReadByte(readFile);
            var bgstretch = LegacySave.ReadByte(readFile);
            var bgcolor = LegacySave.ReadInt(readFile);

            Console.WriteLine();
            Console.WriteLine($"Bg show: {bgshow}");
            Console.WriteLine($"Bg stretch: {bgstretch}");
            Console.WriteLine($"Bg color: {bgcolor}");
            
            text += "Bg show: " + bgshow + "\n";
            text += "Bg stretch: " + bgstretch + "\n";
            text += "Bg color: " + bgcolor + "\n";

            //gridshow skipped

            var lightsEnabled = LegacySave.ReadByte(readFile);
            var lightsAmount = LegacySave.ReadShort(readFile);

            Console.WriteLine();
            Console.WriteLine($"Lights enabled: {lightsEnabled}");
            Console.WriteLine($"Lights amount: {lightsAmount}");
            
            text += "Lights enabled: " + lightsEnabled + "\n";
            text += "Lights amount: " + lightsAmount + "\n";
            for (int i = 0; i < lightsAmount; i++)
            {
                Console.WriteLine();
                Console.WriteLine($"L: {i}");
                var lightX = LegacySave.ReadShort(readFile);
                var lightY = LegacySave.ReadShort(readFile);
                var lightZ = LegacySave.ReadShort(readFile);
                var lightR = LegacySave.ReadShort(readFile);
                var lightC = LegacySave.ReadShort(readFile);

                Console.WriteLine($"L X: {lightX}");
                Console.WriteLine($"L Y: {lightY}");
                Console.WriteLine($"L Z: {lightZ}");
                Console.WriteLine($"L R: {lightR}");
                Console.WriteLine($"L C: {lightC}");
                
                text += "L X: " + lightX + "\n";
                text += "L Y: " + lightY + "\n";
                text += "L Z: " + lightZ + "\n";
                text += "L R: " + lightR + "\n";
                text += "L C: " + lightC + "\n";
            }

            var tempo = LegacySave.ReadByte(readFile);
            var loop = LegacySave.ReadByte(readFile);
            Console.WriteLine();
            Console.WriteLine($"tempo: {tempo}");
            Console.WriteLine($"loop: {loop}");
            
            text += "tempo: " + tempo + "\n";
            text += "loop: " + loop;
            
            File.WriteAllText(Path.Combine(GetUserDataPath(), ApplicationLocalDirectory), text);

            LegacySave.CloseFile(readFile);
        }
    }

    protected override void LoadContent()
    {
        
    }

    protected override void Update(GameTime gameTime)
    {
        Viewport.Update(gameTime);
        
        var k = Keyboard.GetState();

        if (k.IsKeyDown(Keys.L))
        {
            MessageBox = new DialogBox("Bruh", "Test Dialog");
        }

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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Numerics.Vector2.Zero);
        ImGui.Begin("##DockSpaceWindow", dockWindowFlags);
        ImGui.PopStyleVar(3);

        uint dockspaceId = ImGui.GetID("##MainDockSpace");
        ImGui.DockSpace(dockspaceId, Numerics.Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);

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

        if (MessageBox != null)
        {
            MessageBox.Draw();
        }

        GuiRenderer.AfterLayout();
    }
    
    public IntPtr GetOpenGLContext(GraphicsDevice device)
    {
        // 1. Get the 'PlatformStrategy' from the GraphicsDevice
        var platformStrategyProperty = typeof(GraphicsDevice).GetProperty("Strategy", BindingFlags.NonPublic | BindingFlags.Instance);
        var strategy = platformStrategyProperty.GetValue(device);

        // 2. Get the OpenGL Context from the Strategy
        // In DesktopGL, the strategy is 'ConcreteGraphicsDevice'
        var contextField = strategy.GetType().GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
        var context = contextField.GetValue(strategy);

        // 3. The context object itself contains the Handle (IntPtr)
        // It's usually a private 'IntPtr' or a wrapper object with a 'Handle' property
        var handleField = context.GetType().GetField("Handle", BindingFlags.NonPublic | BindingFlags.Instance) 
                          ?? context.GetType().GetField("handle", BindingFlags.NonPublic | BindingFlags.Instance);

        return (IntPtr)handleField.GetValue(context);
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
                IntPtr surface = SDL.SDL_CreateRGBSurfaceFrom((IntPtr)ptr, texture.Width, texture.Height, 32,
                    texture.Width * 4, rmask, gmask, bmask, amask);
                if (surface != IntPtr.Zero)
                {
                    SDL.SDL_SetWindowIcon(Window.Handle, surface);
                    SDL.SDL_FreeSurface(surface);
                }
            }
        }
    }

    public static string GetUserDataPath()
    {
        return Path.Combine(LocalPath, ApplicationLocalDirectory);
    }

    private unsafe void SetupDefaultDockSpace(uint dockspaceId, Numerics.Vector2 size)
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

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (LegacySave != null)
        {
            LegacySave.Dispose();
            LegacySave = null;
        }

        base.OnExiting(sender, args);
    }

    private async Task ShowAssetDownloaderAndWait()
    {
        // TODO:
    }
}