using System;
using System.Collections.Generic;
using System.Linq;
using Hexa.NET.ImGui;
using MineImatorSimplyRemadeNuxi.core;
using MineImatorSimplyRemadeNuxi.core.objs;

namespace MineImatorSimplyRemadeNuxi.ui;

/// <summary>
/// ImGui scene-tree panel.  Mirrors ExampleSceneTree (Godot reference) as closely as
/// possible given the MonoGame + ImGui stack.
///
/// Supported features
/// ──────────────────
///  • Recursive tree built from AppViewport.SceneObjects + SceneObject.Children
///  • Single-click selection (synced bidirectionally with AppViewport.SelectedObject)
///  • Inline rename (F2 or double-click label area)
///  • Right-click context menu: Duplicate / Delete
///  • Drag-and-drop reparenting (shift held = preserve world position stub)
///
/// Skipped / not yet implemented
/// ──────────────────────────────
///  • EditorCommandHistory (undo/redo) — no command system exists yet
///  • SelectionManager multi-selection — viewport has single selection for now
///  • Icons per object type
///  • Keyframe deep-copy in Duplicate
/// </summary>
public class SceneTree
{
    // ── Owner references ────────────────────────────────────────────────────
    public AppViewport Viewport { get; set; }

    // ── State ───────────────────────────────────────────────────────────────

    /// <summary>Maps an ImGui tree-node stable ID (int) to the SceneObject it represents.</summary>
    private readonly Dictionary<int, SceneObject> _objectMap = new();

    /// <summary>Reverse lookup: object → its stable id used this frame.</summary>
    private readonly Dictionary<SceneObject, int> _idMap = new();

    /// <summary>
    /// Mirror of the first entry in SelectionManager.SelectedObjects, kept in sync via
    /// the SelectionChanged event for efficient per-frame highlight lookups.
    /// </summary>
    private SceneObject _selectedObject;
    private SceneObject _renamingObject;      // non-null while an inline rename is open
    private string _renameBuffer = "";

    // ── Constructor ─────────────────────────────────────────────────────────

    public SceneTree()
    {
        // Subscribe to SelectionManager so the tree highlight stays in sync
        // when the selection is changed externally (e.g. from AppViewport).
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionChanged += OnSelectionChanged;
    }

    // Context-menu state
    private SceneObject _contextMenuTarget;
    private bool _openContextMenu;

    // Drag-and-drop state
    private SceneObject _draggingObject;

    // Running ID counter reset each frame
    private int _nodeIdCounter;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Rebuilds internal state and redraws the panel.</summary>
    public void Render()
    {
        ImGui.Begin("Scene Tree");

        if (Viewport == null)
        {
            ImGui.TextDisabled("(no viewport)");
            ImGui.End();
            return;
        }

        // Reset per-frame id counter and maps
        _nodeIdCounter = 0;
        _objectMap.Clear();
        _idMap.Clear();
        _openContextMenu = false;

        // Draw each top-level object — snapshot the list first so that
        // a reparent/delete triggered during the same frame (e.g. via drag-drop)
        // doesn't mutate the collection while we're iterating it.
        foreach (var obj in Viewport.SceneObjects.ToList())
        {
            RenderNode(obj);
        }

        // ── Root-level drop target ───────────────────────────────────────────
        // Cover all remaining empty space in the panel so that dropping onto
        // blank area unparents the object back to the viewport root.
        var remaining = ImGui.GetContentRegionAvail();
        // Need at least a small height so the invisible item exists even when
        // the tree is full; clamp to a minimum of 8 px.
        float dropHeight = Math.Max(remaining.Y, 8f);
        ImGui.InvisibleButton("##root_drop_target", new System.Numerics.Vector2(-1, dropHeight));

        if (ImGui.BeginDragDropTarget())
        {
            unsafe
            {
                var payload = ImGui.AcceptDragDropPayload("SCENE_OBJECT");
                bool delivered = payload.Handle != null && ImGui.IsDelivery(payload);
                if (delivered && _draggingObject != null)
                {
                    // Only reparent if the object isn't already at root level
                    if (_draggingObject.Parent != null)
                    {
                        ReparentObject(_draggingObject, newParent: null, preserveWorldPosition: false);
                    }
                    _draggingObject = null;
                }
            }
            ImGui.EndDragDropTarget();
        }

        // Context menu (opened deferred so it doesn't conflict with tree click handling)
        if (_openContextMenu && _contextMenuTarget != null)
        {
            ImGui.OpenPopup("##SceneTreeContextMenu");
        }

        if (ImGui.BeginPopup("##SceneTreeContextMenu"))
        {
            if (_contextMenuTarget != null)
            {
                ImGui.TextDisabled(_contextMenuTarget.GetDisplayName());
                ImGui.Separator();

                if (ImGui.MenuItem("Duplicate"))
                {
                    DuplicateObject(_contextMenuTarget);
                    _contextMenuTarget = null;
                }

                if (ImGui.MenuItem("Delete"))
                {
                    DeleteObject(_contextMenuTarget);
                    _contextMenuTarget = null;
                }
            }

            ImGui.EndPopup();
        }

        // Handle drag-drop: if mouse released while dragging but no target received the
        // drop, cancel the drag.
        if (_draggingObject != null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _draggingObject = null;
        }

        ImGui.End();
    }

    /// <summary>
    /// Forces the tree selection to match an externally-selected object
    /// (e.g. when the user clicks an object in the viewport).
    /// Delegates to SelectionManager so that all listeners stay in sync.
    /// </summary>
    public void SetSelection(SceneObject obj)
    {
        if (SelectionManager.Instance == null) return;

        SelectionManager.Instance.ClearSelection();
        if (obj != null)
            SelectionManager.Instance.SelectObject(obj);
        // _selectedObject is updated via OnSelectionChanged callback.
    }

    /// <summary>Triggers a full rebuild on the next Render() call (no-op: tree is rebuilt every frame).</summary>
    public void Refresh() { /* tree is rebuilt every frame */ }

    /// <summary>Refreshes the display name of a single object (no-op: rebuilt every frame).</summary>
    public void RefreshObject(SceneObject obj) { /* rebuilt every frame */ }

    // ── Rendering helpers ───────────────────────────────────────────────────

    private void RenderNode(SceneObject obj)
    {
        int nodeId = ++_nodeIdCounter;
        _objectMap[nodeId] = obj;
        _idMap[obj] = nodeId;

        bool hasChildren = obj.Children.Count > 0;
        bool isSelected = SelectionManager.Instance != null
            ? SelectionManager.Instance.IsSelected(obj)
            : _selectedObject == obj;
        bool isRenaming = _renamingObject == obj;

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth;

        if (!hasChildren)
            flags |= ImGuiTreeNodeFlags.Leaf;

        if (isSelected)
            flags |= ImGuiTreeNodeFlags.Selected;

        // ── Tree node ───────────────────────────────────────────────────────
        ImGui.PushID(nodeId);

        bool nodeOpen;

        if (isRenaming)
        {
            // Show an InputText in place of the label while renaming
            nodeOpen = ImGui.TreeNodeEx("##renaming", flags);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.SetKeyboardFocusHere();
            if (ImGui.InputText("##rename_input", ref _renameBuffer, 128,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                CommitRename(obj);
            }
            else if (!ImGui.IsItemActive() && !ImGui.IsItemFocused())
            {
                // Clicked away — commit rename
                CommitRename(obj);
            }
        }
        else
        {
            nodeOpen = ImGui.TreeNodeEx(obj.GetDisplayName() + "##node", flags);
        }

        // ── Interaction ─────────────────────────────────────────────────────

        // Single click → select
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !ImGui.IsItemToggledOpen())
        {
            SelectObject(obj);
        }

        // Double click → begin inline rename
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            BeginRename(obj);
        }

        // Right click → context menu
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            SelectObject(obj);
            _contextMenuTarget = obj;
            _openContextMenu = true;
        }

        // ── Drag source ─────────────────────────────────────────────────────
        if (ImGui.BeginDragDropSource())
        {
            _draggingObject = obj;
            // Payload: single dummy byte; actual dragged object tracked via _draggingObject
            unsafe
            {
                byte dummy = 1;
                ImGui.SetDragDropPayload("SCENE_OBJECT", &dummy, 1);
            }
            ImGui.Text("Move: " + obj.GetDisplayName());
            ImGui.EndDragDropSource();
        }

        // ── Drop target ─────────────────────────────────────────────────────
        if (ImGui.BeginDragDropTarget())
        {
            // AcceptDragDropPayload returns a ptr-wrapper that may be null (no delivery yet).
            // Calling IsDelivery on a null ptr crashes, so guard with an unsafe null check first.
            unsafe
            {
                var payload = ImGui.AcceptDragDropPayload("SCENE_OBJECT");
                bool delivered = payload.Handle != null && ImGui.IsDelivery(payload);
                if (delivered && _draggingObject != null)
                {
                    // Drop ON this item → make dragged object a child of obj
                    if (_draggingObject != obj && !obj.IsDescendantOf(_draggingObject))
                    {
                        ReparentObject(_draggingObject, obj, preserveWorldPosition: false);
                    }
                    _draggingObject = null;
                }
            }
            ImGui.EndDragDropTarget();
        }

        // ── Recurse ─────────────────────────────────────────────────────────
        if (nodeOpen)
        {
            foreach (var child in obj.Children.ToList())
                RenderNode(child);

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    // ── Selection ───────────────────────────────────────────────────────────

    private void SelectObject(SceneObject obj)
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.ClearSelection();
            if (obj != null)
                SelectionManager.Instance.SelectObject(obj);
            // _selectedObject and Viewport.SelectedObject are updated via OnSelectionChanged.
        }
        else
        {
            // Fallback when SelectionManager is not yet initialised (should not happen in practice).
            // Write IsSelected directly — do NOT use Viewport.SelectedObject setter because
            // it routes through SelectionManager and would cause re-entry.
            if (_selectedObject != null)
                _selectedObject.IsSelected = false;
            _selectedObject = obj;
            if (_selectedObject != null)
                _selectedObject.IsSelected = true;
        }
    }

    /// <summary>
    /// Called by <see cref="SelectionManager.SelectionChanged"/> to keep the local
    /// <c>_selectedObject</c> mirror in sync.
    /// AppViewport.SelectedObject is a computed property that reads SelectionManager
    /// directly, so it does NOT need to be written here — doing so would create a
    /// re-entrant loop: setter → ClearSelection → SelectionChanged → OnSelectionChanged.
    /// </summary>
    private void OnSelectionChanged()
    {
        _selectedObject = SelectionManager.Instance?.SelectedObjects.Count > 0
            ? SelectionManager.Instance.SelectedObjects[0]
            : null;
    }

    // ── Rename ──────────────────────────────────────────────────────────────

    private void BeginRename(SceneObject obj)
    {
        _renamingObject = obj;
        _renameBuffer = obj.GetDisplayName();
    }

    private void CommitRename(SceneObject obj)
    {
        var trimmed = _renameBuffer.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            obj.Name = trimmed;

        _renamingObject = null;
        _renameBuffer = "";
    }

    // ── Duplicate ───────────────────────────────────────────────────────────

    private void DuplicateObject(SceneObject original)
    {
        var duplicate = CreateSceneObjectDuplicate(original);
        if (duplicate == null) return;

        // Recursively duplicate children
        DuplicateChildrenRecursive(original, duplicate);

        // Add to the same parent as the original
        if (original.Parent != null)
        {
            original.Parent.AddChild(duplicate);
        }
        else
        {
            Viewport?.SceneObjects.Add(duplicate);
        }

        // Select the new duplicate via SelectionManager (fires SelectionChanged → OnSelectionChanged)
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.ClearSelection();
            SelectionManager.Instance.SelectObject(duplicate);
        }
        else
        {
            SelectObject(duplicate);
        }
    }

    private void DuplicateChildrenRecursive(SceneObject original, SceneObject duplicateParent)
    {
        foreach (var child in original.Children)
        {
            // Skip CharacterSceneObject children (same rule as Example)
            if (child is CharacterSceneObject) continue;

            var childDup = CreateSceneObjectDuplicate(child);
            if (childDup == null) continue;

            duplicateParent.AddChild(childDup);
            DuplicateChildrenRecursive(child, childDup);
        }
    }

    /// <summary>Shallow-copies a SceneObject without children.</summary>
    private SceneObject CreateSceneObjectDuplicate(SceneObject original)
    {
        if (original is CharacterSceneObject)
            return null; // Not supported (same as example)

        SceneObject dup;

        switch (original)
        {
            case LightSceneObject light:
            {
                dup = new LightSceneObject
                {
                    LightColor = light.LightColor,
                    LightEnergy = light.LightEnergy,
                    LightRange = light.LightRange,
                    LightIndirectEnergy = light.LightIndirectEnergy,
                    LightSpecular = light.LightSpecular,
                    LightShadowEnabled = light.LightShadowEnabled
                };
                break;
            }
            case CameraSceneObject cam:
            {
                dup = new CameraSceneObject
                {
                    Fov = cam.Fov,
                    Near = cam.Near,
                    Far = cam.Far
                };
                break;
            }
            default:
                dup = new SceneObject();
                break;
        }

        // Determine name: strip trailing number, find next available
        var baseName = GetBaseName(original.GetDisplayName());
        int nextNum = GetNextAvailableNameNumber(baseName);
        dup.Name = nextNum > 1 ? $"{baseName}{nextNum}" : baseName;

        // Copy base properties
        dup.ObjectType = original.ObjectType;
        dup.IsSelectable = original.IsSelectable;
        dup.Position = original.Position;
        dup.Rotation = original.Rotation;
        dup.Scale = original.Scale;
        dup.PivotOffset = original.PivotOffset;
        dup.ObjectVisible = original.ObjectVisible;
        dup.SpawnCategory = original.SpawnCategory;
        dup.BlockVariant = original.BlockVariant;
        dup.TextureType = original.TextureType;
        dup.SourceAssetPath = original.SourceAssetPath;

        // Visual: copy the reference (shared mesh is fine for now — deep copy not possible without a mesh API)
        dup.Visual = original.Visual;

        return dup;
    }

    // ── Delete ──────────────────────────────────────────────────────────────

    private void DeleteObject(SceneObject obj)
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.DeselectObject(obj);
        else if (_selectedObject == obj)
            SelectObject(null);

        if (obj.Parent != null)
        {
            obj.Parent.RemoveChild(obj);
        }
        else
        {
            Viewport?.SceneObjects.Remove(obj);
        }

        // Also remove any descendant from the viewport's flat list if they were added there
        RemoveDescendantsFromViewport(obj);
    }

    private void RemoveDescendantsFromViewport(SceneObject obj)
    {
        foreach (var child in obj.Children.ToList())
        {
            Viewport?.SceneObjects.Remove(child);
            RemoveDescendantsFromViewport(child);
        }
    }

    // ── Reparent ────────────────────────────────────────────────────────────

    private void ReparentObject(SceneObject obj, SceneObject newParent, bool preserveWorldPosition)
    {
        // preserveWorldPosition: would offset position by parent world transform —
        // transform composition is not yet implemented, so it's a no-op flag for now.

        // Remove from current parent or viewport root
        if (obj.Parent != null)
        {
            obj.Parent.RemoveChild(obj);
        }
        else
        {
            Viewport?.SceneObjects.Remove(obj);
        }

        if (newParent != null)
        {
            newParent.AddChild(obj);
        }
        else
        {
            Viewport?.SceneObjects.Add(obj);
        }
    }

    // ── Naming helpers ──────────────────────────────────────────────────────

    private static string GetBaseName(string name)
    {
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i]))
            i--;
        if (i >= 0 && i < name.Length - 1)
            return name[..( i + 1)];
        return name;
    }

    private int GetNextAvailableNameNumber(string baseName)
    {
        var used = new HashSet<int>();

        if (Viewport != null)
        {
            foreach (var root in Viewport.SceneObjects)
                ScanNode(root);
        }

        int next = 1;
        while (used.Contains(next)) next++;
        return next;

        void ScanNode(SceneObject node)
        {
            var n = node.GetDisplayName();
            if (n == baseName)
                used.Add(1);
            else if (n.StartsWith(baseName) && n.Length > baseName.Length)
            {
                var suffix = n[baseName.Length..];
                if (int.TryParse(suffix, out int num))
                    used.Add(num);
            }

            foreach (var child in node.Children)
                ScanNode(child);
        }
    }

    // HashSet shorthand (avoid full namespace for readability)
    private static System.Collections.Generic.HashSet<int> HashSet<T>()
        => new System.Collections.Generic.HashSet<int>();
}
