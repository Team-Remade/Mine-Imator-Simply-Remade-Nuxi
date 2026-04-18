using Hexa.NET.ImGui;

namespace MineImatorSimplyRemadeNuxi.ui;

public class PropertiesPanel
{
    public void Render()
    {
        if (ImGui.Begin("Properties"))
        {
            if (ImGui.BeginTabBar("PropertiesTabs"))
            {
                if (ImGui.BeginTabItem("Project"))
                {
                    ImGui.Text("Project Properties");
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Object"))
                {
                    ImGui.Text("Object Properties");
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }
}