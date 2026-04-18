using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MineImatorSimplyRemadeNuxi.core.mdl.materials;

public class StandardMaterial : Material
{
    public Color AlbedoColor = new Color();
    public float Metallic;
    public float Roughness;
    public bool NormalEnabled;
    public Texture2D NormalTexture;
    public float Transparency;
    public bool EmissionEnabled;
    public Color Emission;
    public float EmissionEnergyMultiplier;
}