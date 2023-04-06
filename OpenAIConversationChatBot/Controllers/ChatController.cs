using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenAIConversationChatBot;
using OpenAIConversationChatBot.DTOs;
using OpenAIConversationChatBot.Models;
using System.Text;
using System.Text.Json;

namespace GPTConversationChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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

        [HttpGet("{ContextId}")]
        public async Task<IActionResult> GetAsync([FromRoute(Name = "ContextId")] int contextId)
        {
            var chats = _chatContext.Chats.Where(x => x.ContextId == contextId).OrderBy(x => x.Id).Include(x => x.Context);

            return Ok(chats);
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            var chats = _chatContext.Chats.OrderBy(x => x.Context).ThenBy(x => x.Id).Include(x => x.Context);

            return Ok(chats);
        }

        [HttpPost("{ContextId}")]
        public async Task<IActionResult> PostAsync([FromRoute(Name = "ContextId")] int contextId, [FromBody] string message)
        {
            return await GetOpenAIAnswer(contextId, message);
        }

        [HttpDelete("{ContextId}")]
        public async Task<IActionResult> Delete([FromRoute(Name = "ContextId")] int contextId)
        {
            var chatsToDelete = _chatContext.Chats.Where(x => x.ContextId == contextId);
            _chatContext.RemoveRange(chatsToDelete);
            _chatContext.SaveChanges();
            return NoContent();
        }

        private async Task<IActionResult> GetOpenAIAnswer(int contextId, string text)
        {
            try
            {
                var context = _chatContext.Contexts.FirstOrDefault(x => x.Id == contextId);

                if (context == null)
                    return BadRequest($"Context \"{contextId}\" does not exist.");

                var chats = _chatContext.Chats
                    .Where(x => x.ContextId == contextId)
                    .Select(x => x)
                    .ToList();

                if (!chats.Any())
                {
                    chats.Add(new Chat()
                    {
                        ContextId = contextId,
                        Role = "system",
                        Content = context.Prompt
                    });
                }

                chats.Add(new Chat()
                {
                    ContextId = contextId,
                    Role = "user",
                    Content = text
                });

                var requestContent = JsonSerializer.Serialize(
                new
                {
                    model = "gpt-3.5-turbo",
                    messages = chats.Select(x => new Message() { role = x.Role, content = x.Content })
                });

                var request = new StringContent(requestContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", request);

                var responseData = await response.Content.ReadFromJsonAsync<OpenAIResponseDTO>((JsonSerializerOptions?)null);
                var textContent = responseData.Choices.FirstOrDefault().Message.content;

                chats.Add(new Chat()
                {
                    ContextId = contextId,
                    Role = "assistant",
                    Content = textContent
                });

                _chatContext.Chats.UpdateRange(chats);
                await _chatContext.SaveChangesAsync();

                _logger.LogDebug(textContent);

                return Ok(textContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something went wrong");
                throw;
            }
        }
    }
}