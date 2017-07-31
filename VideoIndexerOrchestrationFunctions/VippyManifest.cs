using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace OrchestrationFunctions
{
    public class VippyManifest : Resource
    {
       
        public string internal_id { get; set; }
        public string video_title { get; set; }
        public string video_desc { get; set; }
        public string video_language { get; set; }
        public string[] transcripts { get; set; }
        public dynamic custom { get; set; }

        public string BlobName { get; set; }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
        

       
    
}
