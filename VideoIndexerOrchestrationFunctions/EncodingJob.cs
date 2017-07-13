using System.Collections.Generic;

namespace OrchestrationFunctions
{
    public class EncodingJob
    {
        public string BlobFileName { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }
}