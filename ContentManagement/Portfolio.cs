using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContentManagement
{
    internal class Portfolio
    {
        [JsonProperty("previewItems")]
        public List<PreviewItem> PreviewItems { get; set; }
    }
}
