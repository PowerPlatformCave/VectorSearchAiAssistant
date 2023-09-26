using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Vectorize.Models;
using Vectorize.Services;
using MongoDB.Bson;

namespace Vectorize
{
    public class AddRemoveData
    {

        private readonly MongoDbService _mongo;
        private readonly ILogger _logger;

        public AddRemoveData(MongoDbService mongo, ILoggerFactory loggerFactory)
        {
            _mongo = mongo;
            _logger = loggerFactory.CreateLogger<AddRemoveData>();
        }


        [Function("AddRemoveData")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string? action = req.Query["action"];

            try
            {

                if (action == "add")
                {
                    await AddMovie();
                }
                else if (action == "remove")
                {
                    await RemoveMovie();

                }
                else
                {
                    throw new Exception("Bad Request: AddRemoveData HTTP trigger. Missing value for action in query string, add or remove");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await response.WriteStringAsync("AddRemoveData HTTP trigger function executed successfully.");

                return response;
            }
            catch (Exception ex)
            {

                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync(ex.ToString());
                return response;

            }
        }

        public async Task AddMovie()
        {

            try
            {

                Movie movie = GetMovieTest;

                await _mongo.UpsertMovieAsync(movie);

                _logger.LogInformation("Vector generado para Movie Recomendation Engine y guardado en el catálogo de películas");

            }
            catch (Exception ex)
            {

                _logger.LogError(ex.Message);
                throw;

            }
        }

        public async Task RemoveMovie()
        {

            try
            {
                Movie movie = GetMovieTest;

                await _mongo.DeleteMovieAsync(movie);

                _logger.LogInformation("Vector borrado de Movie Recomendation Engine y eliminado del catálogo de películas");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;

            }

        }

        public Movie GetMovieTest
        {
            get => new Movie(
                Id: null,
                Title: "The Grudge",
                Year: 2020,
                Cast: new List<string>
                {
                    "Andrea Riseborough",
                    "Demián Bichir",
                    "John Cho",
                    "Betty Gilpin",
                    "Lin Shaye",
                    "Jacki Weaver"
                },
                Genres: new List<string>
                {
                    "Horror",
                    "Supernatural"
                },
                Href: "The_Grudge_(2020_film)",
                Extract: "The Grudge is a 2020 American psychological supernatural horror film written and directed by Nicolas Pesce. Originally announced as a reboot of the 2004 American remake and the original 2002 Japanese horror film Ju-On: The Grudge, the film ended up taking place before and during the events of the 2004 film and its two direct sequels, and is the fourth installment in the American The Grudge film series. The film stars Andrea Riseborough, Demián Bichir, John Cho, Betty Gilpin, Lin Shaye, and Jacki Weaver, and follows a police officer who investigates several murders that are seemingly connected to a single house.",
                Thumbnail: "https://upload.wikimedia.org/wikipedia/en/3/34/The_Grudge_2020_Poster.jpeg",
                ThumbnailWidth: 220,
                ThumbnailHeight: 326
                );
        }


    }
}
