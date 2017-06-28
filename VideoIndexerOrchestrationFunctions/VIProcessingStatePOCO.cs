using Newtonsoft.Json;
using System;

namespace OrchestrationFunctions
{
    public class VIProcessingStatePOCO
    {
        [JsonProperty(PropertyName = "id")]
        public string id { get; set; }

        public string BlobName { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public string ErrorMessage { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

}
