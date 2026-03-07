using System.Text.Json.Serialization;

namespace emby_ani_sync.Models
{
    public class AniListGet
    {
        public class AniListGetData
        {
            [JsonPropertyName("Media")]
            public AniListSearch.Media Media { get; set; }
        }

        public class AniListGetMedia
        {
            [JsonPropertyName("data")]
            public AniListGetData Data { get; set; }
        }
    }
}
