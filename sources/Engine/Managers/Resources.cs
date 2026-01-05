using Raylib_cs;

namespace Engine.Managers;

public class Resources
{
    public static string RootFolderPath {get; set;} = "Resources";
    
    private static Resources _instance;
    public static Resources Instance => _instance ??= new Resources();
    
    private static readonly Dictionary<string, object> _resources = new();
    
    private static readonly Dictionary<Type, Func<string, object>> Loaders = new()
    {
        { typeof(Texture2D), path => Raylib.LoadTexture(path) },
        { typeof(Sound), path => Raylib.LoadSound(path) },
        { typeof(Font), path => Raylib.LoadFont(path) },
        { typeof(Shader), path => Raylib.LoadShader(null, path) },
        { typeof(string), path => File.ReadAllText(path)}
    };
    
    private static string GetSubfolder<T>()
    {
        return typeof(T) switch
        {
            Type t when t == typeof(Texture2D) => "Textures",
            Type t when t == typeof(Sound) => "Sounds",
            Type t when t == typeof(Font) => "Fonts",
            Type t when t == typeof(Shader) => "Shaders",
            _ => ""
        };
    }
    
    public static T Load<T>(string relativePath)
    {
        string fullPath = Path.Combine(RootFolderPath, GetSubfolder<T>(), relativePath);

        if (!Loaders.TryGetValue(typeof(T), out var loader))
            throw new NotSupportedException($"Loader for type {typeof(T)} not found");

        var resource = (T) loader(fullPath);
        _resources[fullPath] = resource;

        return resource;
    }
    
    public static void PreLoad<T>(string str) => Load<T>(str);

    public static void PreLoadQuad<T, K>(IEnumerable<string> strs1, IEnumerable<string> strs2)
    {
        foreach (var str in strs1) 
            Load<T>(str);
        
        foreach (var str in strs2) 
            Load<K>(str);
    }

    public static void PreLoadTheSame<T>( IEnumerable<string> strings)
    {
        foreach (var str in strings) 
            Load<T>(str);
    }
    
    public static T Get<T>(string relativePath)
    {
        string subfolder = GetSubfolder<T>();
        string fullPath = string.IsNullOrEmpty(subfolder) ? Path.Combine(RootFolderPath, relativePath) : Path.Combine(RootFolderPath, subfolder, relativePath);

        if (_resources.TryGetValue(fullPath, out var resource))
            return (T)resource;

        if (!Loaders.ContainsKey(typeof(T)))
            throw new NotSupportedException($"No loader registered for type {typeof(T)}");

        return Load<T>(relativePath);
    }
  
    public static Texture2D Texture(string relativePath) => Get<Texture2D>(relativePath);
    public static Font Font(string relativePath) => Get<Font>(relativePath);
    public static Shader Shader(string relativePath) => Get<Shader>(relativePath);
    public static Sound Sound(string relativePath) => Get<Sound>(relativePath);
    
    public static Font FontEx(string relativePath, int fontSize, int[]? fontChars = null, int charCount = 0)
    {
        string fullPath = Path.Combine(RootFolderPath, "Fonts", relativePath);

        if (_resources.TryGetValue(fullPath + ":" + fontSize, out var cachedFont))
            return (Font)cachedFont;

        Font font = Raylib.LoadFontEx(fullPath, fontSize, fontChars, charCount);
        _resources[fullPath + ":" + fontSize] = font;
        return font;
    }
    
    public static void Unload<T>(string relativePath)
    {
        string fullPath = Path.Combine(RootFolderPath, GetSubfolder<T>(), relativePath);

        if (_resources.TryGetValue(fullPath, out var resource))
        {
            if (resource is Texture2D texture)
                Raylib.UnloadTexture(texture);
            else if (resource is Font font)
                Raylib.UnloadFont(font);
            else if (resource is Shader shader)
                Raylib.UnloadShader(shader);
            else if (resource is Sound sound)
                Raylib.UnloadSound(sound);

            _resources.Remove(fullPath);
        }
    }
    
    public static bool Exists<T>(string relativePath)
    {
        string fullPath = Path.Combine(RootFolderPath, GetSubfolder<T>(), relativePath);
        return File.Exists(fullPath);
    }
}