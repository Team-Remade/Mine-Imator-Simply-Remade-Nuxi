using System;
using System.Collections.Generic;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MineImatorSimplyRemadeNuxi.core;
using MineImatorSimplyRemadeNuxi.core.mdl;
using MineImatorSimplyRemadeNuxi.core.objs;
using MineImatorSimplyRemadeNuxi.core.objs.nodes;
using MineImatorSimplyRemadeNuxi.gizmo;
using Numerics = System.Numerics;

namespace MineImatorSimplyRemadeNuxi.ui;

public class AppViewport
{
    public WorkCamera camera;
    GraphicsDevice graphicsDevice;
    BasicEffect basicEffect;
    RenderTarget2D renderTarget;
    RenderTarget2D _pickTarget;
    Effect _pickEffect;
    ImTextureRef textureHandle;
    Texture2D whiteTexture;
    Texture2D _benchTexture;
    ImTextureRef _benchTextureHandle;
    PlaneMesh _xzPlaneMesh;
    
    private MouseState _lastMouseState;
    private MouseState _prevFrameMouse;    // mouse state at end of previous frame
    private int _lastScrollWheelValue;
    private bool _isActive;               // true while right-button fly mode is active
    private Vector2 _imageMin;
    private Vector2 _imageMax;

    // ── Orbit drag state ──────────────────────────────────────────────────────
    private bool _orbitDragging;          // left-drag orbit is active
    private Vector2 _orbitClickPos;       // screen pos where left button was pressed
    private const float OrbitDeadzone = 5f;

    // ── Gizmo ─────────────────────────────────────────────────────────────────
    private Gizmo3D _gizmo;
    private bool    _gizmoEditing;
    private KeyboardState _prevKeyboard;

    /// <summary>Set by App after both objects are created.</summary>
    public SpawnMenu SpawnMenu { get; set; }

    /// <summary>Set by App to allow viewport selection to notify the scene tree.</summary>
    public SceneTree SceneTree { get; set; }

    /// <summary>All scene objects currently in the viewport.</summary>
    public List<SceneObject> SceneObjects { get; } = new();

    /// <summary>
    /// The primary selected scene object (null = nothing selected).
    /// Getting returns the first entry in <see cref="SelectionManager.SelectedObjects"/>.
    /// Setting delegates to SelectionManager so that all listeners stay in sync.
    /// </summary>
    public SceneObject SelectedObject
    {
        get => SelectionManager.Instance?.SelectedObjects.Count > 0
            ? SelectionManager.Instance.SelectedObjects[0]
            : null;
        set
        {
            if (SelectionManager.Instance == null) return;
            SelectionManager.Instance.ClearSelection();
            if (value != null)
                SelectionManager.Instance.SelectObject(value);
        }
    }
    
    public AppViewport(WorkCamera camera, GraphicsDevice graphicsDevice)
    {
        this.camera = camera;
        this.graphicsDevice = graphicsDevice;

        // ── Gizmo ──────────────────────────────────────────────────────────────
        _gizmo = new Gizmo3D(graphicsDevice);
        SelectionManager.Instance.Gizmo = _gizmo;

        basicEffect = new BasicEffect(graphicsDevice);
        basicEffect.TextureEnabled = true;
        
        _xzPlaneMesh = new PlaneMesh(64f, 64f, PlaneOrientation.XZ, graphicsDevice);
        
        renderTarget = new RenderTarget2D(graphicsDevice, 512, 512,
            false, SurfaceFormat.Color, DepthFormat.Depth24);
        _pickTarget = new RenderTarget2D(graphicsDevice, 512, 512,
            false, SurfaceFormat.Color, DepthFormat.Depth24);
        
        whiteTexture = new Texture2D(graphicsDevice, 1, 1);
        whiteTexture.SetData([Color.White]);
        basicEffect.Texture = whiteTexture;
        
        textureHandle = App.GuiRenderer.BindTexture(renderTarget);

        // Load the pick shader compiled by the content pipeline
        _pickEffect = Program.App.Content.Load<Effect>("assets/shaders/PickShader");

        _benchTexture = Program.App.Content.Load<Texture2D>("assets/img/bench");
        _benchTextureHandle = App.GuiRenderer.BindTexture(_benchTexture);
    }

    public void LoadTerrainTexture()
    {
        if (TerrainAtlas.Textures.TryGetValue("8,2", out var terrainTexture))
        {
            basicEffect.Texture = terrainTexture;
        }
    }

    private bool IsMouseOverImage()
    {
        Vector2 mousePos = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);
        return mousePos.X >= _imageMin.X && mousePos.X <= _imageMax.X &&
               mousePos.Y >= _imageMin.Y && mousePos.Y <= _imageMax.Y;
    }

    public void Update(GameTime gameTime)
    {
        if (!Program.App.IsActive) return;
        
        MouseState currentMouse = Mouse.GetState();

        // ── Right-button fly mode ─────────────────────────────────────────────
        if (currentMouse.RightButton == ButtonState.Pressed && IsMouseOverImage())
        {
            if (!_isActive)
            {
                _isActive = true;
                Program.App.IsMouseVisible = false;
                Mouse.SetPosition(Program.App.Window.ClientBounds.Width / 2, Program.App.Window.ClientBounds.Height / 2);
                _lastMouseState = Mouse.GetState();
            }

            MouseState currentCentered = Mouse.GetState();
            int deltaX = currentCentered.X - _lastMouseState.X;
            int deltaY = currentCentered.Y - _lastMouseState.Y;
            int deltaWheel = currentCentered.ScrollWheelValue - _lastScrollWheelValue;

            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, deltaX, deltaY, deltaWheel, true);

            Mouse.SetPosition(Program.App.Window.ClientBounds.Width / 2, Program.App.Window.ClientBounds.Height / 2);
            _lastMouseState = Mouse.GetState();
            _lastScrollWheelValue = _lastMouseState.ScrollWheelValue;
        }
        else
        {
            if (_isActive)
            {
                _isActive = false;
                Program.App.IsMouseVisible = true;
            }
            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, 0, 0, 0, false);
        }
        
        if (_isActive && !IsMouseOverImage() && currentMouse.RightButton == ButtonState.Released)
        {
            _isActive = false;
            Program.App.IsMouseVisible = true;
        }

        // ── Gizmo modifier keys (updated every frame) ────────────────────────
        {
            var kb = Keyboard.GetState();
            _gizmo.Snapping  = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
            _gizmo.ShiftSnap = kb.IsKeyDown(Keys.LeftShift)   || kb.IsKeyDown(Keys.RightShift);

            if (kb.IsKeyDown(Keys.G) && !_prevKeyboard.IsKeyDown(Keys.G) &&
                !_gizmoEditing && _gizmo.GetSelectedCount() > 0)
            {
                _gizmo.UseLocalSpace = !_gizmo.UseLocalSpace;
            }
            _prevKeyboard = kb;
        }

        // ── Left-button orbit drag (only when NOT in fly mode) ────────────────
        if (!_isActive && IsMouseOverImage())
        {
            var mousePos2D = new Vector2(currentMouse.X, currentMouse.Y);
            var imageSize  = new Vector2(_imageMax.X - _imageMin.X, _imageMax.Y - _imageMin.Y);

            // Gizmo hover – only while no button held
            if (currentMouse.LeftButton == ButtonState.Released && !_gizmoEditing)
                _gizmo.UpdateHover(mousePos2D, camera, _imageMin, imageSize);

            // Gizmo interacting guard
            bool gizmoInteracting = _gizmo.Hovering || _gizmoEditing;

            // Left button pressed this frame
            if (currentMouse.LeftButton == ButtonState.Pressed &&
                _prevFrameMouse.LeftButton == ButtonState.Released)
            {
                // Try gizmo first
                if (_gizmo.TryBeginEdit(mousePos2D, camera, _imageMin, imageSize))
                {
                    _gizmoEditing  = true;
                    _orbitDragging = false;
                }
                else
                {
                    _orbitClickPos = mousePos2D;
                    _orbitDragging = false;
                }
            }

            // Left button held
            if (currentMouse.LeftButton == ButtonState.Pressed &&
                _prevFrameMouse.LeftButton == ButtonState.Pressed)
            {
                if (_gizmoEditing)
                {
                    _gizmo.ContinueEdit(mousePos2D);
                }
                else if (!gizmoInteracting)
                {
                    var delta = new Vector2(currentMouse.X - _prevFrameMouse.X,
                                            currentMouse.Y - _prevFrameMouse.Y);
                    var clickDelta = new Vector2(currentMouse.X - _orbitClickPos.X,
                                                 currentMouse.Y - _orbitClickPos.Y);

                    if (!_orbitDragging && clickDelta.Length() > OrbitDeadzone)
                        _orbitDragging = true;

                    if (_orbitDragging)
                        camera.OrbitBy(delta.X, delta.Y);
                }
            }

            // Left button released
            if (currentMouse.LeftButton == ButtonState.Released &&
                _prevFrameMouse.LeftButton == ButtonState.Pressed)
            {
                if (_gizmoEditing)
                {
                    _gizmo.EndEdit();
                    _gizmoEditing = false;
                }
                else if (!_orbitDragging && !gizmoInteracting)
                {
                    PerformPickAtMouse(_prevFrameMouse.X, _prevFrameMouse.Y);
                }
            }

            if (currentMouse.LeftButton == ButtonState.Released)
                _orbitDragging = false;

            // Scroll wheel zoom (orbit) when not flying
            int scrollDelta = currentMouse.ScrollWheelValue - _prevFrameMouse.ScrollWheelValue;
            if (scrollDelta != 0)
                camera.OrbitZoom(scrollDelta / 120f); // 120 units = one notch
        }
        else if (!_isActive)
        {
            // Mouse is not over the viewport and not flying — reset drag state
            if (currentMouse.LeftButton == ButtonState.Released)
                _orbitDragging = false;
        }

        _prevFrameMouse = currentMouse;
    }

    public SceneObject[] GetChildren()
    {
        return SceneObjects.ToArray();
    }

    public Node GetNodeOrNull<t>(string id)
    {
        return null;
    }

    // ── Colour-pick pass ──────────────────────────────────────────────────────

    /// <summary>
    /// Renders the ID-colour pick pass into <see cref="_pickTarget"/> and reads
    /// back the pixel at the given screen-space position to determine which
    /// object was clicked.
    /// </summary>
    private void PerformPickAtMouse(int screenX, int screenY)
    {
        if (_pickTarget == null || _pickEffect == null) return;

        // ── Render pick pass ──────────────────────────────────────────────────
        graphicsDevice.SetRenderTarget(_pickTarget);
        graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer,
            new Vector4(0, 0, 0, 0), 1f, 0);

        // Apply camera matrices to the pick shader
        _pickEffect.Parameters["View"]?.SetValue(camera.View);
        _pickEffect.Parameters["Projection"]?.SetValue(camera.Projection);

        graphicsDevice.RasterizerState   = RasterizerState.CullNone;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;

        RenderPickObjects(SceneObjects);

        graphicsDevice.SetRenderTarget(null);

        // ── Map screen pos → render-target pixel ──────────────────────────────
        float rtW = _pickTarget.Width;
        float rtH = _pickTarget.Height;
        float imgW = _imageMax.X - _imageMin.X;
        float imgH = _imageMax.Y - _imageMin.Y;

        if (imgW <= 0 || imgH <= 0) return;

        int pixelX = (int)((screenX - _imageMin.X) / imgW * rtW);
        int pixelY = (int)((screenY - _imageMin.Y) / imgH * rtH);

        pixelX = MathHelper.Clamp(pixelX, 0, _pickTarget.Width  - 1);
        pixelY = MathHelper.Clamp(pixelY, 0, _pickTarget.Height - 1);

        // Read back single pixel
        Color[] pixels = new Color[_pickTarget.Width * _pickTarget.Height];
        _pickTarget.GetData(pixels);
        Color hit = pixels[pixelY * _pickTarget.Width + pixelX];

        // ── Decode colour → pick ID ────────────────────────────────────────────
        int pickId = hit.R | (hit.G << 8) | (hit.B << 16);

        // ── Dispatch to SelectionManager ──────────────────────────────────────
        bool ctrlHeld = Keyboard.GetState().IsKeyDown(Keys.LeftControl) ||
                        Keyboard.GetState().IsKeyDown(Keys.RightControl);

        if (hit.A < 5) // alpha ≈ 0 → empty space
        {
            if (!ctrlHeld)
                SelectionManager.Instance?.ClearSelection();
            return;
        }

        SceneObject hitObj = FindObjectByPickId(SceneObjects, pickId);
        if (hitObj == null)
        {
            if (!ctrlHeld)
                SelectionManager.Instance?.ClearSelection();
            return;
        }

        if (ctrlHeld)
        {
            SelectionManager.Instance?.ToggleSelection(hitObj);
        }
        else
        {
            SelectionManager.Instance?.ClearSelection();
            SelectionManager.Instance?.SelectObject(hitObj);
        }
        SceneTree?.Refresh();
    }

    /// <summary>Renders all scene objects (and their children) using <see cref="basicEffect"/>.</summary>
    private void RenderObjects(IEnumerable<SceneObject> objects)
    {
        foreach (var obj in objects)
        {
            if (obj.GetEffectiveVisibility() && obj.Visuals.Count > 0)
            {
                basicEffect.World = obj.GetWorldMatrix();

                foreach (var mesh in obj.Visuals)
                    mesh.Render(graphicsDevice, basicEffect);
            }

            if (obj.Children.Count > 0)
                RenderObjects(obj.Children);
        }
    }

    /// <summary>Renders all selectable objects with their pick colour using <see cref="_pickEffect"/>.</summary>
    private void RenderPickObjects(IEnumerable<SceneObject> objects)
    {
        foreach (var obj in objects)
        {
            if (obj.IsSelectable && obj.Visuals.Count > 0)
            {
                // Build the same world transform as the normal render pass
                var world = obj.GetWorldMatrix();

                _pickEffect.Parameters["World"]?.SetValue(world);
                _pickEffect.Parameters["pick_color"]?.SetValue(obj.PickColor);

                foreach (var mesh in obj.Visuals)
                    mesh.Render(graphicsDevice, _pickEffect);
            }

            // Recurse into children
            if (obj.Children.Count > 0)
                RenderPickObjects(obj.Children);
        }
    }

    /// <summary>
    /// Recursively walks <paramref name="objects"/> and returns the first
    /// <see cref="SceneObject"/> whose <see cref="SceneObject.PickColorId"/> matches
    /// <paramref name="pickId"/>, or <c>null</c> if none is found.
    /// </summary>
    private static SceneObject FindObjectByPickId(IEnumerable<SceneObject> objects, int pickId)
    {
        foreach (var obj in objects)
        {
            if (obj.PickColorId == pickId) return obj;
            var found = FindObjectByPickId(obj.Children, pickId);
            if (found != null) return found;
        }
        return null;
    }

    // ── Main render ───────────────────────────────────────────────────────────

    public void Render()
    {
        RasterizerState rasterizerState = new RasterizerState();
        
        Color bgColor = new Color(Program.App.Properties.BackgroundColor[0], Program.App.Properties.BackgroundColor[1], Program.App.Properties.BackgroundColor[2]);
        
        ImGui.Begin("Viewport");

        var size = ImGui.GetContentRegionAvail();

        // Resize render targets if viewport size changes
        if ((renderTarget.Width != (int)MathF.Floor(size.X) || renderTarget.Height != (int)MathF.Floor(size.Y)) && size.X > 0 && size.Y > 0)
        {
            renderTarget.Dispose();
            renderTarget = new RenderTarget2D(graphicsDevice, (int)size.X, (int)size.Y,
                false, SurfaceFormat.Color, DepthFormat.Depth24);
            textureHandle = App.GuiRenderer.BindTexture(renderTarget);

            _pickTarget?.Dispose();
            _pickTarget = new RenderTarget2D(graphicsDevice, (int)size.X, (int)size.Y,
                false, SurfaceFormat.Color, DepthFormat.Depth24);
        }

        if (size.X > 0 && size.Y > 0)
            camera.UpdateProjectionMatrix(45, size.X / size.Y);

        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, bgColor.ToVector4(), 1f, 0);
        graphicsDevice.RasterizerState = rasterizerState;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.SamplerStates[0] = new SamplerState
        {
            Filter = TextureFilter.Point,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap
        };
        camera.ApplyToEffect(basicEffect);
        _xzPlaneMesh.Render(graphicsDevice, basicEffect);

        // Render spawned scene objects
        var savedTexture = basicEffect.Texture;
        basicEffect.Texture = whiteTexture;
        RenderObjects(SceneObjects);
        basicEffect.World = Matrix.Identity;
        basicEffect.Texture = savedTexture;

        // ── Gizmo 3D pass (into the same render target) ────────────────────────
        _gizmo.Render(graphicsDevice, camera);

        graphicsDevice.SetRenderTarget(null);
        
        ImGui.Image(textureHandle, size);
        _imageMin = ImGui.GetItemRectMin();
        _imageMax = ImGui.GetItemRectMax();

        // ── Gizmo 2D overlay (rotation arc / line, drawn over the image) ──────
        {
            var imgMin  = _imageMin;
            var imgSize = new Vector2(_imageMax.X - imgMin.X, _imageMax.Y - imgMin.Y);
            _gizmo.RenderOverlay(camera, imgMin, imgSize);
        }
        
        // Texture button overlaid at the top-left corner of the viewport image
        float padding = 8f;
        ImGui.SetCursorPos(new Numerics.Vector2(padding, ImGui.GetFrameHeight() + padding));
        ImGui.PushStyleColor(ImGuiCol.Button, new Numerics.Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Numerics.Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Numerics.Vector4(0, 0, 0, 0));
        bool benchClicked = ImGui.ImageButton("##benchBtn", _benchTextureHandle, new Numerics.Vector2(64, 64));
        ImGui.PopStyleColor(3);

        if (benchClicked && SpawnMenu != null)
        {
            var btnMax = ImGui.GetItemRectMax();
            var btnMin = ImGui.GetItemRectMin();
            SpawnMenu.Toggle(new Numerics.Vector2(btnMin.X, btnMax.Y + 4f));
        }
        
        ImGui.End();
    }
}
