using System;
using System.Collections.Generic;
using System.Numerics;
using Engine.Internal;
using Raylib_cs;

namespace Engine;

public enum ResizeEdge
{
    None,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public static class Window
{
    private static string _title = "Window";
    private static Scene _rootScene = new Scene { Name = "RootScene" };
    
    private const int EdgeThreshold = 8;
    private const int MinWidth = 500;
    private const int MinHeight = 500;
    
    private static bool _isResizing = false;
    private static ResizeEdge _resizeEdge = ResizeEdge.None;
    private static Vector2 _resizeStartMouse;
    private static Vector2 _resizeStartPos;
    private static Vector2 _resizeStartSize;
    
    private static bool _isMaximized = false;
    private static Vector2 _restorePosition;
    private static Vector2 _restoreSize;
    
    private static bool _isDragging = false;
    private static Vector2 _dragStartMouse;
    private static Vector2 _dragStartPos;
    
    public static Scene RootScene => _rootScene;
    public static bool IsMaximized => _isMaximized;
    public static bool IsDragging => _isDragging;
    
    public static void Init(string path = "Index.xml")
    {
        _rootScene = new Scene { Name = "RootScene" };
        LoadFromXml(path);
    }
    
    public static void LoadFromXml(string xmlPath)
    {
        var (title, children) = XmlLoader.LoadFromFile(xmlPath);
        _title = title;
        
        foreach (var child in children)
        {
            _rootScene.AddChild(child);
        }
    }
    
    public static ResizeEdge GetHoveredEdge()
    {
        var mousePos = Raylib.GetMousePosition();
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();
        
        bool onLeft = mousePos.X < EdgeThreshold;
        bool onRight = mousePos.X > width - EdgeThreshold;
        bool onTop = mousePos.Y < EdgeThreshold;
        bool onBottom = mousePos.Y > height - EdgeThreshold;
        
        if (onTop && onLeft) return ResizeEdge.TopLeft;
        if (onTop && onRight) return ResizeEdge.TopRight;
        if (onBottom && onLeft) return ResizeEdge.BottomLeft;
        if (onBottom && onRight) return ResizeEdge.BottomRight;
        if (onLeft) return ResizeEdge.Left;
        if (onRight) return ResizeEdge.Right;
        if (onTop) return ResizeEdge.Top;
        if (onBottom) return ResizeEdge.Bottom;
        
        return ResizeEdge.None;
    }
    
    private static void UpdateCursor(ResizeEdge edge)
    {
        var cursor = edge switch
        {
            ResizeEdge.Left or ResizeEdge.Right => MouseCursor.ResizeEw,
            ResizeEdge.Top or ResizeEdge.Bottom => MouseCursor.ResizeNs,
            ResizeEdge.TopLeft or ResizeEdge.BottomRight => MouseCursor.ResizeNwse,
            ResizeEdge.TopRight or ResizeEdge.BottomLeft => MouseCursor.ResizeNesw,
            _ => MouseCursor.Default
        };
        Raylib.SetMouseCursor(cursor);
    }
    
    private static void HandleResize()
    {
        if (_isMaximized) return;
        if (_isDragging) return;
        
        var globalMouse = Mouse.GetCursorPos();
        var mousePos = new Vector2(globalMouse.X, globalMouse.Y);
        
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var edge = GetHoveredEdge();
            if (edge != ResizeEdge.None)
            {
                _isResizing = true;
                _resizeEdge = edge;
                _resizeStartMouse = mousePos;
                _resizeStartPos = Raylib.GetWindowPosition();
                _resizeStartSize = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            }
        }
        
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            _isResizing = false;
            _resizeEdge = ResizeEdge.None;
        }
        
        if (_isResizing)
        {
            Vector2 delta = mousePos - _resizeStartMouse;
            float newX = _resizeStartPos.X;
            float newY = _resizeStartPos.Y;
            float newW = _resizeStartSize.X;
            float newH = _resizeStartSize.Y;
            
            switch (_resizeEdge)
            {
                case ResizeEdge.Right:
                    newW = Math.Max(MinWidth, _resizeStartSize.X + delta.X);
                    break;
                case ResizeEdge.Bottom:
                    newH = Math.Max(MinHeight, _resizeStartSize.Y + delta.Y);
                    break;
                case ResizeEdge.Left:
                    newW = Math.Max(MinWidth, _resizeStartSize.X - delta.X);
                    newX = _resizeStartPos.X + _resizeStartSize.X - newW;
                    break;
                case ResizeEdge.Top:
                    newH = Math.Max(MinHeight, _resizeStartSize.Y - delta.Y);
                    newY = _resizeStartPos.Y + _resizeStartSize.Y - newH;
                    break;
                case ResizeEdge.TopLeft:
                    newW = Math.Max(MinWidth, _resizeStartSize.X - delta.X);
                    newH = Math.Max(MinHeight, _resizeStartSize.Y - delta.Y);
                    newX = _resizeStartPos.X + _resizeStartSize.X - newW;
                    newY = _resizeStartPos.Y + _resizeStartSize.Y - newH;
                    break;
                case ResizeEdge.TopRight:
                    newW = Math.Max(MinWidth, _resizeStartSize.X + delta.X);
                    newH = Math.Max(MinHeight, _resizeStartSize.Y - delta.Y);
                    newY = _resizeStartPos.Y + _resizeStartSize.Y - newH;
                    break;
                case ResizeEdge.BottomLeft:
                    newW = Math.Max(MinWidth, _resizeStartSize.X - delta.X);
                    newH = Math.Max(MinHeight, _resizeStartSize.Y + delta.Y);
                    newX = _resizeStartPos.X + _resizeStartSize.X - newW;
                    break;
                case ResizeEdge.BottomRight:
                    newW = Math.Max(MinWidth, _resizeStartSize.X + delta.X);
                    newH = Math.Max(MinHeight, _resizeStartSize.Y + delta.Y);
                    break;
            }
            
            Raylib.SetWindowPosition((int)newX, (int)newY);
            Raylib.SetWindowSize((int)newW, (int)newH);
            
            _rootScene.Width = (int)newW;
            _rootScene.Height = (int)newH;
            _rootScene.CalculateLayout();
            
            UpdateCursor(_resizeEdge);
        }
        else
        {
            UpdateCursor(GetHoveredEdge());
        }
    }
    
    public static void Maximize()
    {
        if (_isMaximized) return;
        
        _restorePosition = Raylib.GetWindowPosition();
        _restoreSize = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        
        int monitor = Raylib.GetCurrentMonitor();
        int monitorWidth = Raylib.GetMonitorWidth(monitor);
        int monitorHeight = Raylib.GetMonitorHeight(monitor);
        
        Raylib.SetWindowPosition(0, 0);
        Raylib.SetWindowSize(monitorWidth, monitorHeight);
        
        _rootScene.Width = monitorWidth;
        _rootScene.Height = monitorHeight;
        _rootScene.CalculateLayout();
        
        _isMaximized = true;
    }
    
    public static void Restore()
    {
        if (!_isMaximized) return;
        
        Raylib.SetWindowPosition((int)_restorePosition.X, (int)_restorePosition.Y);
        Raylib.SetWindowSize((int)_restoreSize.X, (int)_restoreSize.Y);
        
        _rootScene.Width = (int)_restoreSize.X;
        _rootScene.Height = (int)_restoreSize.Y;
        _rootScene.CalculateLayout();
        
        _isMaximized = false;
    }
    
    public static void ToggleMaximize()
    {
        if (_isMaximized)
            Restore();
        else
            Maximize();
    }
    
    public static void Minimize()
    {
        Raylib.MinimizeWindow();
    }
    
    public static void Close()
    {
        Raylib.CloseWindow();
    }
    
    public static void Drag()
    {
        if (_isMaximized) return;
        if (_isDragging) return;
        if (_isResizing) return;
        
        var globalMouse = Mouse.GetCursorPos();
        _isDragging = true;
        _dragStartMouse = new Vector2(globalMouse.X, globalMouse.Y);
        _dragStartPos = Raylib.GetWindowPosition();
    }
    
    private static void HandleDrag()
    {
        if (!_isDragging) return;
        
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            _isDragging = false;
            return;
        }
        
        var globalMouse = Mouse.GetCursorPos();
        var mousePos = new Vector2(globalMouse.X, globalMouse.Y);
        Vector2 delta = mousePos - _dragStartMouse;
        Vector2 newPos = _dragStartPos + delta;
        Raylib.SetWindowPosition((int)newPos.X, (int)newPos.Y);
    }
    
    public static void Run()
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
        Internal.Window.SetWindowRoundedCorners();
        
        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();
            
            HandleResize();
            HandleDrag();
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
