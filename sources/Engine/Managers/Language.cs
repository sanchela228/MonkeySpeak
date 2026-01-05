namespace Engine.Managers;

public static class Language
{
    public static List<string> Languages { get; set; } = new();
    public static string CurrentLanguage { get; set; } = "English";
    private static Dictionary<string, string> _dictionary = new();
    private static bool _loaded;

    public static string Get(string code) => _loaded ? _dictionary.GetValueOrDefault(code, code) : code;
    
    public static async Task Load(string language)
    {
        if (_dictionary.Count > 0)
            _dictionary = new();    
            
        _loaded = false;
        
        try
        {
            var path = Path.Combine(Resources.RootFolderPath, "Repositories\\Languages\\" + language + ".lang");
            foreach (var line in await File.ReadAllLinesAsync( path ) )
            {
                var parts = line.Split('=', 2);
        
                if (parts.Length == 2)
                    _dictionary[parts[0]] = parts[1];
            }
            
            CurrentLanguage = language;
            _loaded = true;
        }
        catch (Exception e)
        {
            throw;
        }
    }
}