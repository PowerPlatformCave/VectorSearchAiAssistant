using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Vectorize.Models;
using Newtonsoft.Json.Linq;


namespace Vectorize.Services
{
    public class MongoDbService
    {
        private readonly MongoClient? _client;
        private readonly IMongoDatabase? _database;
        private readonly Dictionary<string, IMongoCollection<BsonDocument>> _collections;

        private readonly OpenAiService _openAiService;
        private readonly ILogger _logger;

        public MongoDbService(string connection, string databaseName, string collectionNames, OpenAiService openAiService, ILogger logger)
        {

            _logger = logger;
            _openAiService = openAiService;

            _collections = new Dictionary<string, IMongoCollection<BsonDocument>>();

            try
            {
                _client = new MongoClient(connection);
                _database = _client.GetDatabase(databaseName);

                //movie, vectors, completions
                List<string> collections = collectionNames.Split(',').ToList();


                foreach (string collectionName in collections)
                {

                    IMongoCollection<BsonDocument>? collection = _database.GetCollection<BsonDocument>(collectionName.Trim()) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB for MongoDB vCore collection or database.");

                    _collections.Add(collectionName, collection);
                }

                CreateVectorIndexIfNotExists(_collections["vectors"]);


            }
            catch (Exception ex)
            {
                _logger.LogError("MongoDbService Init failure: " + ex.Message);
            }
        }

        public void CreateVectorIndexIfNotExists(IMongoCollection<BsonDocument> vectorCollection)
        {

            try
            {
                string vectorIndexName = "vectorSearchIndex";

                //Comprobar si el índice del vector existe en la colección de vectores
                using (IAsyncCursor<BsonDocument> indexCursor = vectorCollection.Indexes.List())
                {
                    bool vectorIndexExists = indexCursor.ToList().Any(x => x["name"] == vectorIndexName);
                    if (!vectorIndexExists)
                    {
                        BsonDocumentCommand<BsonDocument> command = new BsonDocumentCommand<BsonDocument>(
                        BsonDocument.Parse(@"
                            { createIndexes: 'vectors', 
                              indexes: [{ 
                                name: 'vectorSearchIndex', 
                                key: { vector: 'cosmosSearch' }, 
                                cosmosSearchOptions: { kind: 'vector-ivf', numLists: 5, similarity: 'COS', dimensions: 1536 } 
                              }] 
                            }"));

                        BsonDocument result = _database.RunCommand(command);
                        if (result["ok"] != 1)
                        {
                            _logger.LogError("CreateIndex falló con respuesta: " + result.ToJson());
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("MongoDbService InitializeVectorIndex: " + ex.Message);
            }

        }


        public async Task UpsertVectorAsync(BsonDocument document)
        {

            //Dado que almacenamos todos los vectores en la misma colección sólo necesitamos una función para gestionar todo

            if (!document.Contains("_id"))
            {
                _logger.LogError("UpsertVectorAsync: El documento no contiene _id.");
                throw new ArgumentException("UpsertVectorAsync: El documento no contiene _id.");
            }

            //string? _idValue = document["_id"].ToString();


            try
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", document["_id"]);
                var options = new ReplaceOptions { IsUpsert = true };
                 
                await _collections["vectors"].ReplaceOneAsync(filter, document, options);

            }
            catch (Exception ex)
            {

                _logger.LogError($"Exception: UpsertVectorAsync(): {ex.Message}");
                throw;
            }

        }


        public async Task<Movie> UpsertMovieAsync(Movie movie)
        {

            //Añadir primero a la colección de películas, luego vectorizar y almacenar en la colección de vectores.
            //Podría almacenar los vectores en la colección de películas, pero es más sencillo almacenar en una única colección y buscar allí.
            

            try
            {

                var bsonItem = movie.ToBsonDocument();

                await _collections["movie"].ReplaceOneAsync(
                    filter: Builders<BsonDocument>.Filter.Eq("_id", movie.id),
                    options: new ReplaceOptions { IsUpsert = true },
                    replacement: bsonItem);


                //TO DO: Hacerlo mas sencillo   

                //Almacenar en la colección de vectores
                //Serializar el objeto pelicula para enviarlo a OpenAI
                string sMovie = JObject.FromObject(movie).ToString();
                movie.vector = await _openAiService.GetEmbeddingsAsync(sMovie);
                await UpsertVectorAsync(movie.ToBsonDocument());

            }
            catch (MongoException ex)
            {
                _logger.LogError($"Exception: UpsertMovieAsync(): {ex.Message}");
                throw;

            }

            return movie;
        }

        public async Task DeleteMovieAsync(Movie movie)
        {

            try
            {

                var filter = Builders<BsonDocument>.Filter.And(                 
                     Builders<BsonDocument>.Filter.Eq("_id", movie.id));

                //Eliminar de la colección de películas
                await _collections["movie"].DeleteOneAsync(filter);

                //Borrar de la colección de vectores
                await _collections["vectors"].DeleteOneAsync(filter);

            }
            catch (MongoException ex)
            {
                _logger.LogError($"Exception: DeleteMovieAsync(): {ex.Message}");
                throw;

            }

        }

        public async Task<int> InitialMoviesVectorsAsync()
        {
            try
            {

                _logger.LogInformation("Generación de vectores para películas");


                var filter = new BsonDocument();
                int movieCount = 1;


                using (var cursor = await _collections["movie"].Find(filter).ToCursorAsync())
                {
                    while (await cursor.MoveNextAsync())
                    {
                        var batch = cursor.Current;

                        foreach (var document in batch)
                        {

                            //Deserializar la película para obtener el vector
                            Movie movie = BsonSerializer.Deserialize<Movie>(document);

                            //Generar el vector
                            movie.vector = await _openAiService.GetEmbeddingsAsync(movie.ToString());

                            //Guardar el vector en la colección de vectores
                            //await UpsertVectorAsync(movie.ToBsonDocument());
                            await UpsertVectorAsync(movie.ToBsonDocument());

                            movieCount++;
                            if (movieCount % 100 == 0)
                                _logger.LogInformation($"Generados {movieCount} vectores de películas");
                        }
                    }
                }

                _logger.LogInformation($"Finalizada la generación de vectores de películas. {movieCount} vectores generados");

                return movieCount;
            }
            catch (MongoException ex)
            {
                _logger.LogError($"Exception: InitialMovieVectorsAsync(): {ex.Message}");
                throw;
            }
        }

        public async Task ImportJsonAsync(string collectionName, string json)
        {
            try
            {

                IMongoCollection<BsonDocument> collection = _collections[collectionName];
                var documents = BsonSerializer.Deserialize<IEnumerable<BsonDocument>>(json);
                await collection.InsertManyAsync(documents);
            }

            catch (MongoException ex)
            {
                _logger.LogError($"Exception: ImportJsonAsync(): {ex.Message}");
                throw;
            }
        }


    }
}
