using Newtonsoft.Json;
using System;

namespace VideoIndexerOrchestrationWeb.Models
{
    public class VIJob
    {

        [JsonProperty(PropertyName = "id")]
        public string VIId { get; set; }


        [JsonProperty(PropertyName = "BlobName")]
        public string BloblName { get; set; }


        [JsonProperty(PropertyName = "StartTime")]
        public DateTime? StartTime { get; set; }


        [JsonProperty(PropertyName = "EndTime")]
        public DateTime? EndTime { get; set; }

        public string ProcessingState
        {
            get { 
                if (EndTime != null)
                return "Processed";
            else
                return "Pending";

            }
            set { }
        }

        public TimeSpan? ProcessingTime
        {
            get
            {
                if (ProcessingState == "Processed")
                {
                    return EndTime.Value.Subtract(StartTime.Value);
                }
                else
                    return null;
            }
        }
    }
}