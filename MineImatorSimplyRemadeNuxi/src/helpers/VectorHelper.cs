using Microsoft.Xna.Framework;

namespace MineImatorSimplyRemadeNuxi.helpers;

public static class VectorHelper
{
    public static System.Numerics.Vector3 MonogameToSystemVec3(Vector3 monogame)
    {
        return new System.Numerics.Vector3(monogame.X, monogame.Y, monogame.Z);
    }

    public static System.Numerics.Vector4 MonogameToSystemVec4(Vector4 monogame)
    {
        return new System.Numerics.Vector4(monogame.X, monogame.Y, monogame.Z, monogame.W);
    }

    public static Vector3 SystemToMonogameVec3(System.Numerics.Vector3 systemVec3)
    {
        return new Vector3(systemVec3.X, systemVec3.Y, systemVec3.Z);
    }

    public static Vector4 SystemToMonogameVec4(System.Numerics.Vector4 systemVec4)
    {
        return new Vector4(systemVec4.X, systemVec4.Y, systemVec4.Z, systemVec4.W);
    }

    public static System.Numerics.Vector2 MonogameToSystemVec2(Vector2 monogame)
    {
        return new System.Numerics.Vector2(monogame.X, monogame.Y);
    }

    public static Vector2 SystemToMonogameVec2(System.Numerics.Vector2 monogame)
    {
        return new Vector2(monogame.X, monogame.Y);
    }
}