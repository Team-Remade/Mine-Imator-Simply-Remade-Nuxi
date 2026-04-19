using System;
using System.Collections.Generic;
using System.Linq;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using MineImatorSimplyRemadeNuxi.core.mdl;
using MineImatorSimplyRemadeNuxi.core.objs;

namespace MineImatorSimplyRemadeNuxi.ui;

public class SpawnMenu
{
    // ── State ────────────────────────────────────────────────────────────────
    private bool _isOpen = false;

    private string _selectedCategory = "Primitives";
    private int _selectedObjectIndex = -1;
    private int _selectedVariantIndex = -1;

    private string _searchQuery = "";
    private string _searchBuffer = "";

    // Category → list of object names
    private readonly Dictionary<string, List<string>> _categories;

    // Variant data per-object (populated for Blocks when that system is added)
    private List<string> _currentVariants = new();

    // Custom-model history  (in-memory; load/save not yet implemented)
    private readonly List<string> _customModelHistory = new();
    private readonly Dictionary<string, string> _customModelPaths = new(); // displayName → full path

    // ── References set by the owner ─────────────────────────────────────────
    /// <summary>The viewport whose child list receives newly spawned objects.</summary>
    public AppViewport Viewport { get; set; }

    // ── Constructor ──────────────────────────────────────────────────────────
    public SpawnMenu()
    {
        _categories = new Dictionary<string, List<string>>
        {
            {
                "Camera", new List<string> { "Camera" }
            },
            {
                "Light", new List<string> { "Point Light" }
            },
            {
                "Primitives", new List<string>
                {
                    "Cube",
                    "Sphere",
                    "Cylinder",
                    "Cone",
                    "Torus",
                    "Plane",
                    "Capsule"
                }
            },
            // Blocks / Items / Characters / Custom Models are not yet implemented —
            // they depend on Minecraft loaders and asset systems that don't exist yet.
            {
                "Custom Models", new List<string> { "Load..." }
            }
        };

        // Sync the Custom Models category with in-memory history
        UpdateCustomModelsCategory();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles the spawn menu. When opening, positions the window so its
    /// top-left corner sits at <paramref name="screenPos"/> (pass the
    /// bottom-right of the button that triggered it).  If <paramref name="screenPos"/>
    /// is null the window is centred on screen instead.
    /// </summary>
    public void Toggle(System.Numerics.Vector2? screenPos = null)
    {
        if (_isOpen)
        {
            _isOpen = false;
            return;
        }

        _isOpen = true;
        _nextWindowPos = screenPos;
    }

    private System.Numerics.Vector2? _nextWindowPos;

    public void Render()
    {
        if (!_isOpen) return;

        if (_nextWindowPos.HasValue)
        {
            // Clamp so the window doesn't fall off the right / bottom edge
            var io = ImGui.GetIO();
            float wx = Math.Min(_nextWindowPos.Value.X, io.DisplaySize.X - 910f);
            float wy = Math.Min(_nextWindowPos.Value.Y, io.DisplaySize.Y - 450f);
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(wx, wy), ImGuiCond.Always);
            _nextWindowPos = null; // only force position on the first frame
        }
        else
        {
            // Centre on screen the very first time (no explicit anchor given)
            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(
                new System.Numerics.Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.Appearing,
                new System.Numerics.Vector2(0.5f, 0.5f));
        }

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(900, 440), ImGuiCond.Appearing);

        if (ImGui.Begin("Spawn Menu##SpawnMenuWindow", ref _isOpen))
        {
            RenderSearchBar();
            ImGui.Separator();
            RenderMainColumns();
            ImGui.Separator();
            RenderBottomBar();
        }

        ImGui.End();
    }

    // ── Private rendering helpers ────────────────────────────────────────────

    private void RenderSearchBar()
    {
        ImGui.Text("Search:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-80);
        if (ImGui.InputText("##search", ref _searchBuffer, 256))
        {
            _searchQuery = _searchBuffer;
            _selectedObjectIndex = -1;
            _selectedVariantIndex = -1;
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            _searchBuffer = "";
            _searchQuery = "";
            _selectedObjectIndex = -1;
            _selectedVariantIndex = -1;
        }
    }

    private void RenderMainColumns()
    {
        float totalHeight = ImGui.GetContentRegionAvail().Y - 40; // leave room for bottom bar

        // Three columns: Categories | Objects | Variants
        ImGui.BeginChild("##cols", new System.Numerics.Vector2(0, totalHeight));

        float columnWidth = ImGui.GetContentRegionAvail().X / 3f;

        // ── Categories ──────────────────────────────────────────────────────
        ImGui.BeginChild("##categories", new System.Numerics.Vector2(columnWidth, 0), ImGuiChildFlags.Borders);
        ImGui.TextDisabled("Categories");
        ImGui.Separator();

        foreach (var category in _categories.Keys)
        {
            bool selected = _selectedCategory == category;
            if (ImGui.Selectable(category, selected))
            {
                if (_selectedCategory != category)
                {
                    _selectedCategory = category;
                    _selectedObjectIndex = -1;
                    _selectedVariantIndex = -1;
                    _currentVariants.Clear();
                }
            }
        }

        ImGui.EndChild();
        ImGui.SameLine();

        // ── Objects ─────────────────────────────────────────────────────────
        ImGui.BeginChild("##objects", new System.Numerics.Vector2(columnWidth, 0), ImGuiChildFlags.Borders);
        ImGui.TextDisabled("Objects");
        ImGui.Separator();

        if (_categories.TryGetValue(_selectedCategory, out var objectList))
        {
            var filtered = string.IsNullOrEmpty(_searchQuery)
                ? objectList
                : objectList
                    .Where(o => o.Contains(_searchQuery, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

            for (int i = 0; i < filtered.Count; i++)
            {
                bool selected = _selectedObjectIndex == i;
                if (ImGui.Selectable(filtered[i] + "##obj" + i, selected))
                {
                    _selectedObjectIndex = i;
                    _selectedVariantIndex = -1;
                    OnObjectSelected(filtered[i]);
                }

                // Double-click spawns immediately (except "Load...")
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    _selectedObjectIndex = i;
                    OnObjectDoubleClicked(filtered[i]);
                }
            }
        }

        ImGui.EndChild();
        ImGui.SameLine();

        // ── Variants ────────────────────────────────────────────────────────
        ImGui.BeginChild("##variants", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.Borders);
        ImGui.TextDisabled("Variants");
        ImGui.Separator();

        if (_currentVariants.Count > 0)
        {
            for (int i = 0; i < _currentVariants.Count; i++)
            {
                bool selected = _selectedVariantIndex == i;
                if (ImGui.Selectable(_currentVariants[i] + "##var" + i, selected))
                {
                    _selectedVariantIndex = i;
                }

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    _selectedVariantIndex = i;
                    TrySpawn();
                }
            }
        }
        else
        {
            ImGui.TextDisabled("(not available)");
        }

        ImGui.EndChild();

        ImGui.EndChild(); // ##cols
    }

    private void RenderBottomBar()
    {
        // Push the spawn button to the right
        float buttonWidth = 110f;
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - buttonWidth + ImGui.GetCursorPosX());

        bool canSpawn = CanSpawn();

        if (!canSpawn) ImGui.BeginDisabled();
        if (ImGui.Button("Spawn", new System.Numerics.Vector2(buttonWidth, 28)))
        {
            TrySpawn();
        }

        if (!canSpawn) ImGui.EndDisabled();
    }

    private bool CanSpawn()
    {
        if (_selectedObjectIndex < 0) return false;

        var filtered = GetFilteredObjects();
        if (_selectedObjectIndex >= filtered.Count) return false;

        var objectName = filtered[_selectedObjectIndex];

        if (_selectedCategory == "Custom Models")
            return objectName != "Load..." && _customModelPaths.ContainsKey(objectName);

        // Blocks require a variant selection (no variants yet = not spawnable)
        if (_selectedCategory == "Blocks")
            return _currentVariants.Count > 0 && _selectedVariantIndex >= 0;

        return true;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnObjectSelected(string objectName)
    {
        if (_selectedCategory == "Custom Models" && objectName == "Load...")
        {
            // File dialog not yet implemented — skip
            _currentVariants.Clear();
            return;
        }

        // For Blocks: populate variant list (not yet implemented, would call MinecraftModelHelper)
        if (_selectedCategory == "Blocks")
        {
            // TODO: Populate _currentVariants from block-state JSON when that system exists
            _currentVariants.Clear();
            return;
        }

        _currentVariants.Clear();
    }

    private void OnObjectDoubleClicked(string objectName)
    {
        if (_selectedCategory == "Custom Models" && objectName == "Load...")
        {
            // File dialog not yet implemented — skip
            return;
        }

        if (_selectedCategory == "Blocks")
        {
            // Blocks need a variant chosen first; double-click on object list not enough
            return;
        }

        TrySpawn();
    }

    // ── Spawn logic ───────────────────────────────────────────────────────────

    private void TrySpawn()
    {
        var filtered = GetFilteredObjects();
        if (_selectedObjectIndex < 0 || _selectedObjectIndex >= filtered.Count) return;

        var objectName = filtered[_selectedObjectIndex];
        SpawnObject(objectName);
        _isOpen = false;
    }

    private void SpawnObject(string objectName)
    {
        if (Viewport == null) return;

        int nextNum = GetNextAvailableObjectNumber(objectName);
        string fullObjectName = nextNum > 1 ? $"{objectName}{nextNum}" : objectName;

        switch (_selectedCategory)
        {
            case "Camera":
                SpawnCameraObject(fullObjectName);
                break;

            case "Light":
                SpawnLightObject(fullObjectName);
                break;

            case "Custom Models":
                if (_customModelPaths.TryGetValue(objectName, out var modelPath))
                {
                    // GLB/GLTF loading not yet implemented
                }
                break;

            default:
                // Primitives (and any future categories that use SceneObject)
                SpawnPrimitiveObject(objectName, fullObjectName);
                break;
        }

        // Notify scene tree to refresh after spawn
        Viewport.SceneTree?.Refresh();
    }

    // ── Public spawn helpers (usable by external systems) ────────────────────

    /// <summary>Creates and registers a CameraSceneObject in the viewport.</summary>
    public CameraSceneObject SpawnCameraObject(string objectName)
    {
        if (Viewport == null) return null;

        var cameraObject = new CameraSceneObject
        {
            Name = objectName,
            ObjectType = "Camera",
            SpawnCategory = "Camera",
            Position = Vector3.Zero
        };
        cameraObject.AssignObjectId();

        Viewport.SceneObjects.Add(cameraObject);
        return cameraObject;
    }

    /// <summary>Creates and registers a LightSceneObject in the viewport.</summary>
    public LightSceneObject SpawnLightObject(string objectName)
    {
        if (Viewport == null) return null;

        var lightObject = new LightSceneObject
        {
            Name = objectName,
            ObjectType = "Point Light",
            SpawnCategory = "Light",
            Position = Vector3.Zero
        };
        lightObject.AssignObjectId();

        Viewport.SceneObjects.Add(lightObject);
        return lightObject;
    }

    /// <summary>Creates and registers a primitive SceneObject in the viewport.</summary>
    public SceneObject SpawnPrimitiveObject(string primitiveType, string objectName)
    {
        if (Viewport == null) return null;

        var sceneObject = new SceneObject
        {
            Name = objectName,
            ObjectType = primitiveType,
            SpawnCategory = "Primitives",
            Position = Vector3.Zero,
            PivotOffset = new Vector3(0, -0.5f, 0)
        };
        sceneObject.AssignObjectId();

        // Create mesh geometry for supported primitive types
        if (primitiveType == "Plane")
        {
            var graphicsDevice = Program.App.GraphicsDevice;
            // 1 metre × 1 metre vertical (XY) plane
            sceneObject.AddMesh(new PlaneMesh(1f, 1f, PlaneOrientation.XY, graphicsDevice));
        }

        Viewport.SceneObjects.Add(sceneObject);
        return sceneObject;
    }

    // ── Utility helpers ───────────────────────────────────────────────────────

    private List<string> GetFilteredObjects()
    {
        if (!_categories.TryGetValue(_selectedCategory, out var all)) return new List<string>();

        return string.IsNullOrEmpty(_searchQuery)
            ? all
            : all.Where(o => o.Contains(_searchQuery, System.StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Returns the next available instance number for the given object type name.
    /// Finds the lowest positive integer N such that neither "<paramref name="objectType"/>"
    /// (treated as N=1) nor "<paramref name="objectType"/>N" (for N≥2) is already used.
    /// </summary>
    private int GetNextAvailableObjectNumber(string objectType)
    {
        var usedNumbers = new HashSet<int>();

        if (Viewport != null)
        {
            foreach (var root in Viewport.SceneObjects)
                ScanNode(root);
        }

        int next = 1;
        while (usedNumbers.Contains(next)) next++;
        return next;

        void ScanNode(SceneObject node)
        {
            var name = node.GetDisplayName();
            if (name == objectType)
            {
                usedNumbers.Add(1);
            }
            else if (name.StartsWith(objectType) && name.Length > objectType.Length)
            {
                var suffix = name[objectType.Length..];
                if (int.TryParse(suffix, out int num))
                    usedNumbers.Add(num);
            }

            foreach (var child in node.Children)
                ScanNode(child);
        }
    }

    private string CleanBlockName(string fileName)
    {
        var words = fileName.Replace("_", " ").Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                words[i] = char.ToUpper(words[i][0]) + words[i][1..];
        }

        return string.Join(" ", words);
    }

    private void UpdateCustomModelsCategory()
    {
        var list = new List<string> { "Load..." };
        list.AddRange(_customModelPaths.Select(kvp => kvp.Key));
        _categories["Custom Models"] = list;
    }

    private void AddToCustomModelHistory(string modelPath, string displayName)
    {
        if (_customModelHistory.Contains(modelPath))
        {
            _customModelHistory.Remove(modelPath);
            var oldKey = _customModelPaths.FirstOrDefault(x => x.Value == modelPath).Key;
            if (!string.IsNullOrEmpty(oldKey))
                _customModelPaths.Remove(oldKey);
        }

        _customModelHistory.Insert(0, modelPath);
        _customModelPaths[displayName] = modelPath;
        UpdateCustomModelsCategory();

        if (_selectedCategory == "Custom Models")
            _selectedObjectIndex = -1;
    }
}
