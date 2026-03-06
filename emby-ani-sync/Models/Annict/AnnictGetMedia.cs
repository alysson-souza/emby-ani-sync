using System.Text.Json.Serialization;

namespace emby_ani_sync.Models.Annict;

public class AnnictGetMedia
{
    public class AnnictGetMediaData
    {
        [JsonPropertyName("node")]
        public AnnictSearch.AnnictAnime Node { get; set; }
    }

    public class AnnictGetMediaRoot
    {
        [JsonPropertyName("data")]
        public AnnictGetMediaData AnnictGetMediaData { get; set; }
    }
}
