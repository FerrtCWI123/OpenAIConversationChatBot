using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenAIConversationChatBot;
using OpenAIConversationChatBot.DTOs;
using OpenAIConversationChatBot.Models;
using System.Text;
using System.Text.Json;

namespace GPTConversationChatBot.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly HttpClient _httpClient;
        private readonly ChatContext _chatContext;

        public ChatController(ILogger<ChatController> logger, ChatContext chatContext, IOptions<OpenAPISettings> openAPISettings)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAPISettings.Value.OpenAPIKey}");
            _chatContext = chatContext;
        }

        [HttpPost]
        public async Task<string> PostAsync([FromBody] string message)
        {
            return await GetOpenAIAnswer(message);
        }

        private async Task<string> GetOpenAIAnswer(string text)
        {
            try
            {
                var chat = _chatContext.Chats.Select(x => x).ToList();

                chat.Add(new Chat()
                {
                    Role = "user",
                    Content = text
                });

                var requestContent = JsonSerializer.Serialize(
                new
                {
                    model = "gpt-3.5-turbo",
                    messages = chat.Select(x => new Message() { role = x.Role, content = x.Content })
                });

                var request = new StringContent(requestContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", request);

                var responseData = await response.Content.ReadFromJsonAsync<OpenAIResponseDTO>((JsonSerializerOptions?)null);
                var textContent = responseData.Choices.FirstOrDefault().Message.content;

                chat.Add(new Chat()
                {
                    Role = "assistant",
                    Content = textContent
                });

                _chatContext.Chats.UpdateRange(chat);
                await _chatContext.SaveChangesAsync();

                _logger.LogDebug(textContent);

                return textContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something went wrong");
                return null;
            }
        }
    }
}