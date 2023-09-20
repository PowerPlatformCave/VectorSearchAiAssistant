using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace Search.Models
{

    public class Movie
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Title { get; set; }
        public int Year { get; set; }
        public List<string> Cast { get; set; }
        public List<string> Genres { get; set; }
        public string Href { get; set; }
        public string Extract { get; set; }
        public string Thumbnail { get; set; }
        public int ThumbnailWidth { get; set; }
        public int ThumbnailHeight { get; set; }
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
            this.Id = Id;
            this.Title = Title;
            this.Year = Year;
            this.Cast = Cast;
            this.Genres = Genres;
            this.Href = Href;
            this.Extract = Extract;
            this.Thumbnail = Thumbnail;
            this.ThumbnailWidth = ThumbnailWidth;
            this.ThumbnailHeight = ThumbnailHeight;
            this.vector = vector;
        }
    }


}
