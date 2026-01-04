using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Reflection;
using Engine.Binding;
using Engine.Types;
using Raylib_cs;
using Engine.UI;

namespace Engine;

public static class XmlLoader
{
    private static readonly Dictionary<string, Type> _elementTypes = new();
    private static readonly List<string> _searchPaths = new();
    private static string _currentBasePath = "";
    
    static XmlLoader()
    {
        RegisterElement<Rect>("Rect");
        RegisterElement<Circle>("Circle");
        RegisterElement<Scene>("Scene");
    }
    
    public static void RegisterElement<T>(string tagName) where T : Node, new()
    {
        _elementTypes[tagName] = typeof(T);
    }
    
    public static void AddSearchPath(string path)
    {
        if (!_searchPaths.Contains(path))
            _searchPaths.Add(path);
    }
    
    public static void ClearSearchPaths()
    {
        _searchPaths.Clear();
    }
    
    public static (string title, List<Node> nodes) LoadFromFile(string path)
    {
        _currentBasePath = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        
        var doc = XDocument.Load(path);
        var root = doc.Root;
        
        if (root == null || root.Name.LocalName != "Window")
            throw new Exception("Root element must be <Window>");
        
        var title = root.Attribute("Title")?.Value ?? "Window";
        var nodes = new List<Node>();
        
        foreach (var element in root.Elements())
        {
            var result = ParseElement(element);
            if (result != null)
                nodes.AddRange(result);
        }
        
        return (title, nodes);
    }
    
    private static List<Node>? ParseElement(XElement element)
    {
        var tagName = element.Name.LocalName;
        
        if (tagName == "Fragment")
        {
            var fragmentChildren = new List<Node>();
            foreach (var childElement in element.Elements())
            {
                var childNodes = ParseElement(childElement);
                if (childNodes != null)
                    fragmentChildren.AddRange(childNodes);
            }
            return fragmentChildren;
        }
        
        Node? node = null;
        if (_elementTypes.TryGetValue(tagName, out var type))
        {
            node = (Node)Activator.CreateInstance(type)!;
        }
        else
        {
            var externalFile = FindExternalFile(tagName + ".xml");
            if (externalFile != null)
            {
                node = LoadFromExternalFile(externalFile);
            }
            else
            {
                Console.WriteLine($"Unknown element: {tagName} (no registered type or {tagName}.xml file found)");
                return null;
            }
        }
        
        if (node == null)
            return null;
        
        foreach (var attr in element.Attributes())
        {
            SetProperty(node, attr.Name.LocalName, attr.Value);
        }
        
        foreach (var childElement in element.Elements())
        {
            var childNodes = ParseElement(childElement);
            if (childNodes != null)
            {
                foreach (var childNode in childNodes)
                    node.AddChild(childNode);
            }
        }
        
        return new List<Node> { node };
    }
    
    private static string? FindExternalFile(string fileName)
    {
        var relativePath = Path.Combine(_currentBasePath, fileName);
        if (File.Exists(relativePath))
            return relativePath;
        
        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(searchPath, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        
        return null;
    }
    
    private static Node? LoadFromExternalFile(string filePath)
    {
        try
        {
            var doc = XDocument.Load(filePath);
            var root = doc.Root;
            
            if (root == null)
                return null;
            
            var rootTagName = root.Name.LocalName;
            
            if (rootTagName == "Fragment")
            {
                Console.WriteLine("Warning: Fragment as root element in external file is not fully supported. Use a wrapper element.");
                return null;
            }
            
            var result = ParseElement(root);
            return result?.Count > 0 ? result[0] : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load external file {filePath}: {ex.Message}");
            return null;
        }
    }
    
    private static readonly HashSet<string> _eventProperties = new()
    {
        "OnPressed", "OnPress", "OnRelease", "OnHover", "OnHoverExit"
    };
    
    private static void SetProperty(Node node, string propertyName, string value)
    {
        var type = node.GetType();
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        
        if (prop == null)
        {
            var currentType = type.BaseType;
            while (currentType != null && prop == null)
            {
                prop = currentType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                currentType = currentType.BaseType;
            }
        }
        
        if (prop == null)
        {
            Console.WriteLine($"Unknown property: {propertyName} on {type.Name}");
            return;
        }
        
        if (!prop.CanWrite)
        {
            Console.WriteLine($"Property {propertyName} is read-only on {type.Name}");
            return;
        }
        
        try
        {
            if (_eventProperties.Contains(propertyName))
            {
                prop.SetValue(node, value);
                return;
            }
            
            if (BindingResolver.IsBinding(value))
            {
                var result = BindingResolver.ExecuteWithReturn(value, node);
                if (result != null)
                {
                    var convertedResult = ConvertValue(result.ToString()!, prop.PropertyType);
                    prop.SetValue(node, convertedResult);
                }
                return;
            }
            
            var convertedValue = ConvertValue(value, prop.PropertyType);
            prop.SetValue(node, convertedValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set {propertyName}: {ex.Message}");
        }
    }
    
    private static object? ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(DynamicFloat))
            return DynamicFloat.Parse(value);
        
        if (targetType == typeof(float))
            return float.Parse(value);
        
        if (targetType == typeof(int))
            return int.Parse(value);
        
        if (targetType == typeof(string))
            return value;
        
        if (targetType == typeof(bool))
            return bool.Parse(value);
        
        if (targetType == typeof(Color))
            return ParseColor(value);
        
        if (targetType == typeof(Color?))
            return ParseColor(value);
        
        if (targetType.IsEnum)
            return Enum.Parse(targetType, value, ignoreCase: true);
        
        return Convert.ChangeType(value, targetType);
    }
    
    private static Color ParseColor(string value)
    {
        return value.ToLower() switch
        {
            "red" => Color.Red,
            "green" => Color.Green,
            "blue" => Color.Blue,
            "white" => Color.White,
            "black" => Color.Black,
            "yellow" => Color.Yellow,
            "orange" => Color.Orange,
            "purple" => Color.Purple,
            "gray" or "grey" => Color.Gray,
            _ => ParseHexColor(value)
        };
    }
    
    private static Color ParseHexColor(string hex)
    {
        if (hex.StartsWith("#"))
            hex = hex[1..];
        
        if (hex.Length == 6)
        {
            var r = Convert.ToByte(hex[0..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            return new Color(r, g, b, (byte)255);
        }
        
        if (hex.Length == 8)
        {
            var r = Convert.ToByte(hex[0..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            var a = Convert.ToByte(hex[6..8], 16);
            return new Color(r, g, b, a);
        }
        
        return Color.White;
    }
}
