using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;

namespace OrchestrationFunctions
{
    public class VippyProcessingState : Resource
    {
       
        public string AlternateId { get; set; }

        public string DocumentType => "state";

        public string AmsAssetId { get; set; }
    
        public string VideoIndexerId { get; set; }    

        public string BlobName { get; set; }

        public string videoTitle { get; set; }

        public string VideoDescription { get; set; }
    
        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public string ErrorMessage { get; set; }

        public string[] Transcripts { get; set; }

        /// <summary>
        /// This holds the optional client values passed in via the json manifest
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

}
