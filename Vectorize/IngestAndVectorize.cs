using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Vectorize.Services;


namespace Vectorize
{
    public class IngestAndVectorize
    {

        private readonly MongoDbService _mongo;
        private readonly ILogger _logger;

        public IngestAndVectorize(MongoDbService mongo, ILoggerFactory loggerFactory)
        {
            _mongo = mongo;
            _logger = loggerFactory.CreateLogger<IngestAndVectorize>();
        }

        [Function("IngestAndVectorize")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestData req)
        {
            _logger.LogInformation("La función de Ingesta y Vectorización está procesando la solicitud.");
            try
            {

                // Ingesta de datos desde json (en blob storage) a colecciones MongoDB
                await IngestDataFromBlobStorageAsync();


                //Generar vectores en los datos y almacenarlos en la colección de vectores
                await GenerateAndStoreVectorsAsync();


                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await response.WriteStringAsync("La función de ingesta y vectorización se ha ejecutado correctamente.");

                return response;
            }
            catch (Exception ex)
            {

                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync(ex.ToString());
                return response;

            }
        }

        public async Task IngestDataFromBlobStorageAsync()
        {


            try
            {
                //BlobContainerClient blobContainerClient = new BlobContainerClient(new Uri("https://6ra5ehlohm6skfnstorage.blob.core.windows.net/movies"));

                BlobContainerClient blobContainerClient = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=6ra5ehlohm6skfnstorage;AccountKey=spofxiExkV8EaGF6ExKUa6Nr06jgOVuS0FrI5Fw+W96WSukJ1IRCS7EwfAvZFnLckKLsBXzy3Rp++AStbKevXg==", "movies");
                //Download and ingest movies.json
                _logger.LogInformation("Ingesta de películas desde blob storage");


                BlobClient movieBlob = blobContainerClient.GetBlobClient("movies-2020s.json");
                BlobDownloadStreamingResult pResult = await movieBlob.DownloadStreamingAsync();

                using (StreamReader pReader = new StreamReader(pResult.Content))
                {
                    string movieJson = await pReader.ReadToEndAsync();
                    await _mongo.ImportJsonAsync("movie", movieJson);

                }
                _logger.LogInformation("Ingesta de datos de películas completada");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception: IngestDataFromBlobStorageAsync(): {ex.Message}");
                throw;
            }
        }

        public async Task GenerateAndStoreVectorsAsync()
        {

            try
            {
                //Generar vectores de películas y almacenarlos en la colección de vectores
                int moviesVectors = await _mongo.InitialMoviesVectorsAsync();

                _logger.LogInformation("Generación y almacenamiento de vectores completado");
                _logger.LogInformation($"{moviesVectors} peliculas completadas.");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception: GenerateAndStoreVectorsAsync(): {ex.Message}");
                throw;
            }
        }
    }
}
