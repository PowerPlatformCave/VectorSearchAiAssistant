using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using System.Text.RegularExpressions;

namespace Search.Services;

/// <summary>
/// Servicio para acceder a Azure OpenAI.
/// </summary>
public class OpenAiService
{
    private readonly string _embeddingsModelOrDeployment = string.Empty;
    private readonly string _completionsModelOrDeployment = string.Empty;
    private readonly int _maxConversationTokens = default;
    private readonly int _maxCompletionTokens = default;
    private readonly ILogger _logger;
    private readonly OpenAIClient _client;



    //Mensajes del sistema para enviar con mensajes de usuario para instruir al modelo para la sesión de chat
    private readonly string _systemPrompt = @"
        Eres un asistente de inteligencia artificial que recomienda películas a la gente.
        Proporciona respuestas concisas que sean educadas y profesionales" + Environment.NewLine;

    private readonly string _systemPromptRetailAssistant = @"
        Eres un asistente inteligente para la plataforma Power Cave AI Assistant. 
        Estás diseñado para proporcionar respuestas útiles a las preguntas de los usuarios sobre 
        recomendaciones de películas proporcionadas en formato JSON a continuación.

        Instrucciones:
        - Responde únicamente a las preguntas relacionadas con la información que se facilita a continuación,
        - No hagas referencia a datos de películas que no se proporcionen a continuación.
        - Si no está seguro de una respuesta, puedes decir ""No lo sé"" o ""No estoy seguro"" y recomendar a los usuarios que busquen por sí mismos.

        Texto de información relevante:";

    //Mensaje del sistema para enviar  mensajes de usuario para instruir al modelo de resumen
    private readonly string _summarizePrompt = @"
        Resume esta pregunta en una o dos palabras para utilizarla como etiqueta en un botón de una página web. Sólo palabras de salida." + Environment.NewLine;


    /// <summary>
    /// Obtiene el número máximo de tokens de la conversación a enviar como parte de la petición al usuario.
    /// </summary>
    public int MaxConversationTokens
    {
        get => 100;// MaxConversationPromptTokens;
    }
    /// <summary>
    /// Obtiene el número máximo de tokens que se pueden utilizar en la generación de la finalización.
    /// </summary>
    public int MaxCompletionTokens
    {
        get => _maxCompletionTokens;
    }

    /// <summary>
    /// Crea una nueva instancia del servicio.
    /// </summary>
    /// <param name="endpoint">Endpoint URI.</param>
    /// <param name="key">Account key.</param>
    /// <param name="embeddingsDeployment">Nombre del despliegue del modelo para generar incrustaciones.</param>
    /// <param name="completionsDeployment">Nombre del despliegue del modelo para generar terminaciones.</param>
    /// <param name="maxConversationBytes">Número máximo de bytes para limitar el historial de conversación enviado para una finalización.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">Se lanza cuando endpoint, key, deploymentName o maxConversationBytes son nulos o están vacíos.</exception>
    /// <remarks>
    /// Este constructor validará las credenciales y creará una instancia de cliente HTTP.
    /// </remarks>
    public OpenAiService(string endpoint, string key, string embeddingsDeployment, string completionsDeployment, string maxCompletionTokens, string maxConversationTokens, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(embeddingsDeployment);
        ArgumentException.ThrowIfNullOrEmpty(completionsDeployment);
        ArgumentException.ThrowIfNullOrEmpty(maxConversationTokens);
        ArgumentException.ThrowIfNullOrEmpty(maxCompletionTokens);

        _embeddingsModelOrDeployment = embeddingsDeployment;
        _completionsModelOrDeployment = completionsDeployment;
        _maxConversationTokens = int.TryParse(maxConversationTokens, out _maxConversationTokens) ? _maxConversationTokens : 100;
        _maxCompletionTokens = int.TryParse(maxCompletionTokens, out _maxCompletionTokens) ? _maxCompletionTokens : 500;

        _logger = logger;

        OpenAIClientOptions options = new OpenAIClientOptions()
        {
            Retry =
            {
                Delay = TimeSpan.FromSeconds(2),
                MaxRetries = 10,
                Mode = RetryMode.Exponential
            }
        };

        //Utilizar como punto final en la configuración para utilizar nombres de endpoint y modelos de OpenAI que no sean de Azure Open AI.
        if (endpoint.Contains("api.openai.com"))
            _client = new OpenAIClient(key, options);
        else
            _client = new(new Uri(endpoint), new AzureKeyCredential(key), options);


    }

    /// <summary>
    /// Envía una consulta al modelo de incrustación de OpenAI desplegado y devuelve una matriz de vectores como respuesta.
    /// </summary>
    /// <param name="sessionId">Identificador de la sesión de chat para la conversación actual.</param>
    /// <param name="prompt">Mensaje de aviso para incrustaciones generadas </param>
    /// <returns> Respuesta del modelo OpenAI como un array de vectores junto con tokens para el prompt y la respuesta.</returns>
    public async Task<(float[] response, int responseTokens)> GetEmbeddingsAsync(string sessionId, string input)
    {

        float[] embedding = new float[0];
        int responseTokens = 0;

        try
        {
            EmbeddingsOptions options = new EmbeddingsOptions(input)
            {
                Input = input,
                User = sessionId
            };


            var response = await _client.GetEmbeddingsAsync(_embeddingsModelOrDeployment, options);


            Embeddings embeddings = response.Value;

            responseTokens = embeddings.Usage.TotalTokens;
            embedding = embeddings.Data[0].Embedding.ToArray();

            return (
                embedding,
                responseTokens);
        }
        catch (Exception ex)
        {
            string message = $"OpenAiService.GetEmbeddingsAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    /// <summary>
    /// Envía una consulta al modelo OpenAI LLM desplegado y devuelve la respuesta.
    /// </summary>
    /// <param name="sessionId">Identificador de la sesión de chat para la conversación actual.</param>
    /// <param name="prompt">Mensaje de aviso para enviar al despliegue.</param>
    /// <returns>Respuesta del modelo OpenAI junto con tokens para el prompt y la respuesta.</returns>
    public async Task<(string response, int promptTokens, int responseTokens)> GetChatCompletionAsync(string sessionId, string userPrompt, string documents)
    {

        try
        {

            ChatMessage systemMessage = new ChatMessage(ChatRole.System, _systemPromptRetailAssistant + documents);
            ChatMessage userMessage = new ChatMessage(ChatRole.User, userPrompt);


            ChatCompletionsOptions options = new()
            {

                Messages =
                {
                    systemMessage,
                    userMessage
                },
                MaxTokens = _maxCompletionTokens,
                User = sessionId,
                Temperature = 0.5f, //0.3f,
                NucleusSamplingFactor = 0.95f, //0.5f,
                FrequencyPenalty = 0,
                PresencePenalty = 0
            };

            Response<ChatCompletions> completionsResponse = await _client.GetChatCompletionsAsync(_completionsModelOrDeployment, options);


            ChatCompletions completions = completionsResponse.Value;

            return (
                response: completions.Choices[0].Message.Content,
                promptTokens: completions.Usage.PromptTokens,
                responseTokens: completions.Usage.CompletionTokens
            );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    /// <summary>
    /// Envía la conversación existente al modelo OpenAI y devuelve un resumen de dos palabras.
    /// </summary>
    /// <param name="sessionId">Identificador de la sesión de chat para la conversación actual.</param>
    /// <param name="userPrompt">La primera solicitud de usuario y la finalización para enviar a la implementación.</param>
    /// <returns>Respuesta de resumen del despliegue del modelo OpenAI.</returns>
    public async Task<string> SummarizeAsync(string sessionId, string userPrompt)
    {

        ChatMessage systemMessage = new ChatMessage(ChatRole.System, _summarizePrompt);
        ChatMessage userMessage = new ChatMessage(ChatRole.User, userPrompt);


        ChatCompletionsOptions options = new()
        {
            Messages = {
                systemMessage,
                userMessage
            },
            User = sessionId,
            MaxTokens = 200,
            Temperature = 0.0f,
            NucleusSamplingFactor = 1.0f,
            FrequencyPenalty = 0,
            PresencePenalty = 0
        };

        Response<ChatCompletions> completionsResponse = await _client.GetChatCompletionsAsync(_completionsModelOrDeployment, options);

        ChatCompletions completions = completionsResponse.Value;
        string output = completions.Choices[0].Message.Content;

        //Eliminar todos los caracteres numéricos no alfa (Turbo tiene la costumbre de poner las cosas entre comillas, incluso cuando le dices que no lo haga).
        string summary = Regex.Replace(output, @"[^a-zA-Z0-9\s]", "");

        return summary;
    }
}