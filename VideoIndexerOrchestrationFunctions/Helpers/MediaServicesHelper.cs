// =============================================================================================================
// Adapted code from https://github.com/johndeu/media-services-dotnet-functions-integration/tree/master/shared
//  Special thanks to John Deutscher (https://github.com/johndeu) and Xavier Pouyat (https://github.com/xpouyat)
// 
// =============================================================================================================


using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OrchestrationFunctions
{
    public static class MediaServicesHelper
    {
        // Read values from the App.config file.
        private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("MediaServicesAccountName");
        private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("MediaServicesAccountKey");



        // Field for service context.
        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;

        public static CloudMediaContext Context { get => _context; set => _context = value; }

        static MediaServicesHelper()
        {
            // Static class initialization.. get a media context, etc. 
            // Create and cache the Media Services credentials in a static class variable.
            _cachedCredentials = new MediaServicesCredentials(
                            _mediaServicesAccountName,
                            _mediaServicesAccountKey);

            // Used the chached credentials to create CloudMediaContext.
            Context = new CloudMediaContext(_cachedCredentials);

        }

        internal static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
        {
            var processor = Context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
            ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }

        public static Uri GetValidOnDemandURI(IAsset asset)
        {
            var aivalidurls = GetValidURIs(asset);
            if (aivalidurls != null)
            {
                return aivalidurls.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        public static IEnumerable<Uri> GetValidURIs(IAsset asset)
        {
            IEnumerable<Uri> ValidURIs;
            var ismFile = asset.AssetFiles.AsEnumerable().Where(f => f.Name.EndsWith(".ism")).OrderByDescending(f => f.IsPrimary).FirstOrDefault();

            if (ismFile != null)
            {
                var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

                var se = Context.StreamingEndpoints.AsEnumerable().Where(o => (o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o))).OrderByDescending(o => o.CdnEnabled);

                if (se.Count() == 0) // No running which can do dynpackaging SE. Let's use the default one to get URL
                {
                    se = Context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
                }

                var template = new UriTemplate("{contentAccessComponent}/{ismFileName}/manifest");

                ValidURIs = locators.SelectMany(l =>
                    se.Select(
                            o =>
                                template.BindByPosition(new Uri("http://" + o.HostName), l.ContentAccessComponent,
                                    ismFile.Name)))
                    .ToArray();

                return ValidURIs;
            }
            else
            {
                return null;
            }
        }

        public static Uri GetValidOnDemandPath(IAsset asset)
        {
            var aivalidurls = GetValidPaths(asset);
            if (aivalidurls != null)
            {
                return aivalidurls.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        public static IEnumerable<Uri> GetValidPaths(IAsset asset)
        {
            IEnumerable<Uri> ValidURIs;

            var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

            var se = Context.StreamingEndpoints.AsEnumerable().Where(o => (o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o))).OrderByDescending(o => o.CdnEnabled);

            if (se.Count() == 0) // No running which can do dynpackaging SE. Let's use the default one to get URL
            {
                se = Context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
            }

            var template = new UriTemplate("{contentAccessComponent}/");
            ValidURIs = locators.SelectMany(l => se.Select(
                        o =>
                            template.BindByPosition(new Uri("http://" + o.HostName), l.ContentAccessComponent)))
                .ToArray();

            return ValidURIs;
        }

        static public bool CanDoDynPackaging(IStreamingEndpoint mySE)
        {
            return ReturnTypeSE(mySE) != StreamEndpointType.Classic;
        }

        static public StreamEndpointType ReturnTypeSE(IStreamingEndpoint mySE)
        {
            if (mySE.ScaleUnits != null && mySE.ScaleUnits > 0)
            {
                return StreamEndpointType.Premium;
            }
            else
            {
                if (new Version(mySE.StreamingEndpointVersion) == new Version("1.0"))
                {
                    return StreamEndpointType.Classic;
                }
                else
                {
                    return StreamEndpointType.Standard;
                }
            }
        }

        public enum StreamEndpointType
        {
            Classic = 0,
            Standard,
            Premium
        }

        public static string ReturnContent(IAssetFile assetFile)
        {
            string datastring = null;

            try
            {
                string tempPath = System.IO.Path.GetTempPath();
                string filePath = Path.Combine(tempPath, assetFile.Name);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                assetFile.Download(filePath);

                StreamReader streamReader = new StreamReader(filePath);
                Encoding fileEncoding = streamReader.CurrentEncoding;
                datastring = streamReader.ReadToEnd();
                streamReader.Close();

                File.Delete(filePath);
            }
            catch
            {

            }

            return datastring;
        }
    }
}
