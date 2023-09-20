using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace Vectorize.Models
{

    public class Movie
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string id { get; set; }
        public string title { get; set; }
        public int year { get; set; }
        public List<string> cast { get; set; }
        public List<string> genres { get; set; }
        public string href { get; set; }
        public string extract { get; set; }
        public string thumbnail { get; set; }
        public int thumbnail_width { get; set; }
        public int thumbnail_height { get; set; }
        public float[]? vector { get; set; }

        public Movie(
    string Id,
    string Title,
    int Year,
    List<string> Cast,
    List<string> Genres,
    string Href,
    string Extract,
    string Thumbnail,
    int ThumbnailWidth,
    int ThumbnailHeight,
    float[]? vector = null)
        {
            this.id = Id;
            this.title = Title;
            this.year = Year;
            this.cast = Cast;
            this.genres = Genres;
            this.href = Href;
            this.extract = Extract;
            this.thumbnail = Thumbnail;
            this.thumbnail_width = ThumbnailWidth;
            this.thumbnail_height = ThumbnailHeight;
            this.vector = vector;
        }
    }


}
