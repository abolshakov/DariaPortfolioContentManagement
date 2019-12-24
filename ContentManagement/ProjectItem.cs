using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using Newtonsoft.Json;

namespace ContentManagement
{
	internal class ProjectItem: IImageOwner
	{
		private int _id;
	    private string _image;
        private string _description;
        private string _url;
        private string _video;

        [Browsable(false)]
        [JsonProperty("id")]
        public int Id
        {
	        get
	        {
		        if (_id == 0)
		        {
			        _id = PersistenceManager.NextId;
		        }
		        return _id;
	        }
	        set => _id = value;
        }

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
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
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

        [JsonProperty("width")]
        [DefaultValue(0)]
        [Browsable(false)]
		public int Width { get; set; }

        [JsonProperty("height")]
        [DefaultValue(0)]
        [Browsable(false)]
		public int Height { get; set; }

		[JsonIgnore]
        [Browsable(false)]
        public Project Parent { get; set; }

        public void UpdateImageSize(Size size)
        {
	        Width = size.Width;
	        Height = size.Height;
        }
	}
}
