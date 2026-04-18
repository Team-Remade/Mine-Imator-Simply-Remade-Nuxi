using System;
using Hexa.NET.ImGui;
using MineImatorSimplyRemadeNuxi.core.objs;

namespace MineImatorSimplyRemadeNuxi.ui;

public class PropertiesPanel
{
    public Node Floor;
    
    public float[] BackgroundColor = [0.5764706f, 0.5764706f, 1f, 1f];

    public int GetResolutionWidth()
    {
        return 0;
    }

    public int GetResolutionHeight()
    {
        return 0;
    }

    public int GetFramerate()
    {
        return 0;
    }

    public int TextureAnimationFps = 0;
    
    public string BackgroundImagePath = "";
    
    public bool StretchBackground = true;
    

    public void Render()
    {
        if (ImGui.Begin("Properties"))
        {
            if (ImGui.BeginTabBar("PropertiesTabs"))
            {
                if (ImGui.BeginTabItem("Project"))
                {
                    ImGui.Text("Project Properties");
                    ImGui.Separator();

                    if (ImGui.CollapsingHeader("Project Settings", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.Text("Project Name:");
                        ImGui.SetNextItemWidth(-1);
                        string projectName = "Untitled Project";
                        ImGui.InputText("##ProjectName", ref projectName, 256);

                        ImGui.Spacing();
                        ImGui.Text("Resolution:");
                        int resWidth = 1920;
                        int resHeight = 1080;
                        ImGui.SetNextItemWidth(80);
                        ImGui.InputInt("##ResWidth", ref resWidth, 0, 0, ImGuiInputTextFlags.None);
                        ImGui.SameLine();
                        ImGui.Text(" x ");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(80);
                        ImGui.InputInt("##ResHeight", ref resHeight, 0, 0, ImGuiInputTextFlags.None);
                        ImGui.Text("Presets:");
                        if (ImGui.Button("720p")) { resWidth = 1280; resHeight = 720; }
                        ImGui.SameLine();
                        if (ImGui.Button("1080p")) { resWidth = 1920; resHeight = 1080; }
                        ImGui.SameLine();
                        if (ImGui.Button("1440p")) { resWidth = 2560; resHeight = 1440; }
                        ImGui.SameLine();
                        if (ImGui.Button("4K")) { resWidth = 3840; resHeight = 2160; }

                        ImGui.Spacing();
                        ImGui.Text("Framerate:");
                        int framerate = 30;
                        ImGui.SetNextItemWidth(80);
                        ImGui.InputInt("##Framerate", ref framerate, 0, 0, ImGuiInputTextFlags.None);
                        ImGui.SameLine();
                        ImGui.Text(" fps");
                        ImGui.Text("Presets:");
                        if (ImGui.Button("24")) { framerate = 24; }
                        ImGui.SameLine();
                        if (ImGui.Button("30")) { framerate = 30; }
                        ImGui.SameLine();
                        if (ImGui.Button("60")) { framerate = 60; }
                        ImGui.SameLine();
                        if (ImGui.Button("120")) { framerate = 120; }

                        ImGui.Spacing();
                        ImGui.Text("Texture Animation Speed:");
                        int texAnimSpeed = 20;
                        ImGui.SetNextItemWidth(80);
                        ImGui.InputInt("##TexAnimSpeed", ref texAnimSpeed, 0, 0, ImGuiInputTextFlags.None);
                        ImGui.SameLine();
                        ImGui.Text(" fps");
                        ImGui.Text("Presets:");
                        if (ImGui.Button("10##tex")) { texAnimSpeed = 10; }
                        ImGui.SameLine();
                        if (ImGui.Button("20##tex")) { texAnimSpeed = 20; }
                        ImGui.SameLine();
                        if (ImGui.Button("30##tex")) { texAnimSpeed = 30; }
                        ImGui.SameLine();
                        if (ImGui.Button("60##tex")) { texAnimSpeed = 60; }
                    }

                    if (ImGui.CollapsingHeader("Background Settings"))
                    {
                        unsafe
                        {
                            ImGui.Text("Background Color:");
                            ImGui.SetNextItemWidth(-1);
                            fixed (byte* label = "##BackgroundColor"u8)
                            fixed (float* bgColorPtr = BackgroundColor)
                            {
                                ImGui.ColorEdit4(label, bgColorPtr, ImGuiColorEditFlags.None);
                            }

                            ImGui.Spacing();
                            ImGui.Text("Presets:");
                            var presets = new (string name, float r, float g, float b, float a)[]
                            {
                                ("Dawn", 1f, 0.7f, 0.5f, 1f),
                                ("Morning", 0.6f, 0.8f, 1f, 1f),
                                ("Day", 0.5764706f, 0.5764706f, 1f, 1f),
                                ("Sunset", 1f, 0.5f, 0.3f, 1f),
                                ("Dusk", 0.3f, 0.4f, 0.7f, 1f),
                                ("Night", 0.05f, 0.05f, 0.15f, 1f)
                            };
                            for (int i = 0; i < presets.Length; i++)
                            {
                                if (i > 0) ImGui.SameLine();
                                if (ImGui.Button(presets[i].name))
                                {
                                    BackgroundColor[0] = presets[i].r;
                                    BackgroundColor[1] = presets[i].g;
                                    BackgroundColor[2] = presets[i].b;
                                    BackgroundColor[3] = presets[i].a;
                                }
                            }
                        }
                    }

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