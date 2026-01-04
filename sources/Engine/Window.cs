using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace Engine;

public class Window
{
    private string _title = "Window";
    private Scene _rootScene;
    
    public Scene RootScene => _rootScene;
    
    public Window(string path = "Index.xml")
    {
        _rootScene = new Scene { Name = "RootScene" };
        LoadFromXml(path);
    }
    
    public void LoadFromXml(string xmlPath)
    {
        var (title, children) = XmlLoader.LoadFromFile(xmlPath);
        _title = title;
        
        foreach (var child in children)
        {
            _rootScene.AddChild(child);
        }
    }
    
    public void Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.UndecoratedWindow | ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(800, 600, _title);
        
        Raylib.SetTargetFPS(Raylib.GetMonitorRefreshRate(Raylib.GetCurrentMonitor()));
        
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady())
            return;
        
        _rootScene.Width = 800;
        _rootScene.Height = 600;
        
        _rootScene.CalculateLayout();
        
        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();
            
            _rootScene.RootUpdate(deltaTime);
            
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 20, 20, 255));
            
            _rootScene.RootDraw();

            Raylib.EndDrawing();
        }
        
        _rootScene.RootDispose();
        
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }
}
