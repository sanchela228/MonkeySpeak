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
    private static readonly Dictionary<string, FontFamily> _fontFamilies = new();
    
    public static FontFamily? GetFontFamily(string name)
    {
        return _fontFamilies.TryGetValue(name, out var font) ? font : null;
    }
    
    static XmlLoader()
    {
        RegisterElement<Rect>("Rect");
        RegisterElement<Circle>("Circle");
        RegisterElement<Scene>("Scene");
        RegisterElement<Grid>("Grid");
        RegisterElement<Text>("Text");
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
        
        if (tagName == "Include")
        {
            var source = element.Attribute("Source")?.Value;
            if (string.IsNullOrEmpty(source))
            {
                Console.WriteLine("Include: Source attribute is required");
                return null;
            }
            
            var externalFile = FindExternalFile(source);
            if (externalFile != null)
            {
                var includedNode = LoadFromExternalFile(externalFile);
                
                if (includedNode != null)
                    return new List<Node> { includedNode };
                
                return null;
            }
            
            Console.WriteLine($"Include: File not found: {source}");
            return null;
        }
        
        if (tagName == "FontFamily")
        {
            var name = element.Attribute("Name")?.Value;
            var fontFile = element.Attribute("Font")?.Value ?? "default.ttf";
            var sizeStr = element.Attribute("Size")?.Value ?? "16";
            var colorStr = element.Attribute("Color")?.Value ?? "White";
            var spacingStr = element.Attribute("Spacing")?.Value ?? "1";
            
            int size = int.Parse(sizeStr);
            float spacing = float.Parse(spacingStr.Replace('.', ','));
            var color = ParseColor(colorStr);
            
            var fontFamily = new FontFamily
            {
                FontPath = fontFile,
                Size = size,
                Color = color,
                Spacing = spacing,
                Rotation = 0
            };
            
            _fontFamilies[name] = fontFamily;
            return null;
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
                foreach (var child in root.Elements())
                {
                    ParseElement(child);
                }

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
        if (propertyName == "Controller" || propertyName == "Bind")
        {
            var controllerType = FindControllerType(value);
            if (controllerType != null)
            {
                var controller = (NodeController?)Activator.CreateInstance(controllerType);
                if (controller != null)
                    node.SetController(controller);
            }
            else
            {
                Console.WriteLine($"Controller type '{value}' not found");
            }
            return;
        }
        
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
        {
            if (value.Contains('.')) return float.Parse(value.Replace('.', ','));
            return float.Parse(value);
        }
        
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
        
        if (targetType == typeof(FontFamily))
        {
            var registered = GetFontFamily(value);
            if (registered.HasValue)
                return registered.Value;
            
            return ParseFontFamily(value);
        }
        
        return Convert.ChangeType(value, targetType);
    }
    
    private static readonly Dictionary<string, Type> _controllerTypeCache = new();
    private static bool _controllerTypesScanned = false;
    
    private static Type? FindControllerType(string name)
    {
        if (_controllerTypeCache.TryGetValue(name, out var cached))
            return cached;
        
        if (!_controllerTypesScanned)
            ScanControllerTypes();
        
        return _controllerTypeCache.GetValueOrDefault(name);
    }
    
    private static void ScanControllerTypes()
    {
        _controllerTypesScanned = true;
        
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(NodeController).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        _controllerTypeCache[type.Name] = type;
                        if (type.FullName != null)
                            _controllerTypeCache[type.FullName] = type;
                    }
                }
            }
            catch
            {
            }
        }
    }
    
    public static void RegisterController<T>(string? alias = null) where T : NodeController
    {
        var type = typeof(T);
        _controllerTypeCache[alias ?? type.Name] = type;
    }
    
    private static Color ParseColor(string value)
    {
        return value.ToLower() switch
        {
            "red" => Color.Red,
            "green" => Color.Green,
            "blue" => Color.Blue,
            "blank" => Color.Blank,
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
    
    private static FontFamily ParseFontFamily(string value)
    {
        var parts = value.Split(',');
        
        string fontName = parts.Length > 0 ? parts[0].Trim() : "default.ttf";
        int fontSize = parts.Length > 1 ? int.Parse(parts[1].Trim()) : 16;
        Color color = parts.Length > 2 ? ParseColor(parts[2].Trim()) : Color.White;
        float spacing = parts.Length > 3 ? float.Parse(parts[3].Trim()) : 1;
        
        return new FontFamily
        {
            FontPath = fontName,
            Size = fontSize,
            Color = color,
            Spacing = spacing,
            Rotation = 0
        };
    }
}
