using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MineImatorSimplyRemadeNuxi.core.mdl;

namespace MineImatorSimplyRemadeNuxi.core.objs;

public class SceneObject
{
    public object Visual;
    
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale;
    
    public Vector3 TargetPosition;
    public Vector3 TargetRotation;
    
    public Vector3 LocalPosition;
    public Vector3 LocalRotation;
    public Vector3 LocalScale;
    
    public string ObjectId;
    public string Name;
    public string ObjectType;
    public bool ObjectVisible;

    public string SpawnCategory;
    public string BlockVariant;
    public string TextureType = "item";
    public string SourceAssetPath;
    
    public bool CastShadow;
    
    public bool InheritPosition = true;
    public bool InheritRotation = true;
    public bool InheritScale = true;
    public bool InheritVisibility = true;
    public bool InheritPivotOffset;
    
    public Vector3 PivotOffset;

    // ── Selection ────────────────────────────────────────────────────────────
    public bool IsSelected;
    public bool IsSelectable = true;

    // ── Colour picking ───────────────────────────────────────────────────────
    /// <summary>Unique integer pick ID (1-based; 0 means "no object").</summary>
    public int PickColorId { get; private set; }

    /// <summary>
    /// RGB colour encoding of <see cref="PickColorId"/>, in 0-1 range.
    /// R = (id &gt;&gt; 0) &amp; 0xFF, G = (id &gt;&gt; 8) &amp; 0xFF, B = (id &gt;&gt; 16) &amp; 0xFF,
    /// all divided by 255.
    /// </summary>
    public Vector3 PickColor { get; private set; }

    /// <summary>
    /// Assigns ObjectId and the pick-colour pair from SelectionManager.
    /// Call once after the object is constructed (before it enters any scene).
    /// </summary>
    public void AssignObjectId()
    {
        var (uuid, pickColorId) = SelectionManager.Instance.GetNextObjectId();
        ObjectId    = uuid;
        PickColorId = pickColorId;
        PickColor   = new Vector3(
            ((pickColorId >>  0) & 0xFF) / 255f,
            ((pickColorId >>  8) & 0xFF) / 255f,
            ((pickColorId >> 16) & 0xFF) / 255f);
    }

    // ── Hierarchy ────────────────────────────────────────────────────────────
    /// <summary>Direct child SceneObjects (not the viewport root).</summary>
    private readonly List<SceneObject> _children = new();

    /// <summary>The parent SceneObject, or null if parented directly to the viewport.</summary>
    public SceneObject Parent { get; private set; }

    public IReadOnlyList<SceneObject> Children => _children;

    /// <summary>Adds a child to this object and sets its Parent.</summary>
    public void AddChild(SceneObject child)
    {
        if (child == null || child == this) return;
        child.Parent?.RemoveChild(child);
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>Removes a child from this object and clears its Parent.</summary>
    public void RemoveChild(SceneObject child)
    {
        if (_children.Remove(child))
            child.Parent = null;
    }

    /// <summary>Returns the display name for UI use (falls back to ObjectType then "Object").</summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(Name)) return Name;
        if (!string.IsNullOrEmpty(ObjectType)) return ObjectType;
        return "Object";
    }

    /// <summary>Returns true if <paramref name="ancestor"/> is somewhere up this object's parent chain.</summary>
    public bool IsDescendantOf(SceneObject ancestor)
    {
        var current = Parent;
        while (current != null)
        {
            if (current == ancestor) return true;
            current = current.Parent;
        }
        return false;
    }

    // ── Legacy stub ──────────────────────────────────────────────────────────
    public List<Mesh> GetMeshInstancesRecursively(object visual)
    {
        return [];
    }
}
