using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MineImatorSimplyRemadeNuxi.core.mdl;

namespace MineImatorSimplyRemadeNuxi.core.objs;

public class SceneObject
{
    public object Visual;
    
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale;
    
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

    public List<Mesh> GetMeshInstancesRecursively(object visual)
    {
        return [];
    }
}