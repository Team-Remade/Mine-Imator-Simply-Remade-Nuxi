using Hexa.NET.ImGui;

namespace MineImatorSimplyRemadeNuxi.ui;

public class Timeline
{
    public static Timeline Instance { get; private set; }
    
    public int CurrentFrame { get; private set; }

    public Timeline()
    {
        Instance = this;
    }
    
    public void Render()
    {
        ImGui.Begin("Timeline");
        ImGui.End();
    }
}