using System.ComponentModel;
using System.Drawing.Design;
using Newtonsoft.Json;

namespace ContentManagement
{
    internal class PortfolioItem: IImageOwner
    {
        private string _image;
        private string _description;
        private string _url;
        private string _video;

        public PortfolioItem()
        {
            Id = PersistenceManager.NextId;
        }

        [JsonIgnore]
        [Browsable(false)]
        public int Id { get; set; }

        [JsonProperty("image")]
        [DefaultValue("")]
        [Editor(typeof(ImagePicker), typeof(UITypeEditor))]
        [TypeConverter(typeof(DummyConverter))]
        public string Image
        {
            get => _image;
            set => _image = value?.Trim();
        }

        [JsonProperty("description")]
        [DefaultValue("")]
        public string Description
        {
            get => _description;
            set => _description = value?.Trim();
        }

        [JsonProperty("url")]
        [DefaultValue("")]
        public string Url
        {
            get => _url;
            set => _url = value?.Trim();
        }

        [JsonProperty("video")]
        [DefaultValue("")]
        public string Video
        {
	        get => _video;
	        set => _video = value?.Trim();
        }

        [JsonIgnore]
        [Browsable(false)]
        public PreviewItem Parent { get; set; }
    }
}
