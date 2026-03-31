using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Reflection;
using System.Xml.Serialization;
using Microsoft.Extensions.Options;
using Core.Configurations;
using Core.Resources.Xml;

namespace Pages
{
    public class IndexModel : PageModel
    {
        private readonly App _appConfiguration;
        private readonly IWebHostEnvironment _environment;
        public IndexModel(IOptions<App> appInfo, IWebHostEnvironment environment)
        {
            _appConfiguration = appInfo.Value;
            _environment = environment;
            
            // Manifest = GetManifestFromXml();
        }

        public Manifest Manifest;
        public string BackendVersion => _appConfiguration.BackendVersion;
        public string FrontendVersion => _appConfiguration.FrontendVersion + " " + _appConfiguration.FrontendAdditionalVersionText;
        public string AppName => _appConfiguration.Name;
        
        
        public Manifest GetManifestFromXml()
        {
            string webRootPath = _environment.WebRootPath;
            string xmlFilePath = Path.Combine(webRootPath, "Manifest.xml");
        
            XmlSerializer serializer = new XmlSerializer(typeof(Manifest));
        
            using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open))
            {
                return (Manifest) serializer.Deserialize(fileStream);
            }
        }
    }
}