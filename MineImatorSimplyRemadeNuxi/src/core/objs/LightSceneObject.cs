using Microsoft.Xna.Framework;

namespace MineImatorSimplyRemadeNuxi.core.objs;

public class LightSceneObject : SceneObject
{
    public Color LightColor;
    public float LightEnergy;
    public float LightRange;
    public float LightIndirectEnergy;
    public float LightSpecular;
    public bool LightShadowEnabled;
}