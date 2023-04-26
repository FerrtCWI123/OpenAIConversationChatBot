using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAIConversationChatBot;
using OpenAIConversationChatBot.DTOs;
using OpenAIConversationChatBot.Models;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Text.Json;

namespace GPTConversationChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private const string USR_MESSAGE = "user";
        private const string SYS_MESSAGE = "system";

        private readonly ILogger<ChatController> _logger;
        private readonly HttpClient _httpClient;
        private readonly ChatContext _chatContext;
        private readonly IMemoryCache _memoryCache;

        public ChatController(ILogger<ChatController> logger, ChatContext chatContext, IOptions<OpenAPISettings> openAPISettings, IMemoryCache memoryCache)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAPISettings.Value.OpenAPIKey}");
            _chatContext = chatContext;
            _memoryCache = memoryCache;
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


        [HttpPost("Chat")]
        public async Task<IActionResult> PostConversationAsync([FromBody] string message)
        {
            string chatId = "TesteChatKey";
            OpenAIAPI api = new OpenAIAPI("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");
            var chat = api.Chat.CreateConversation();

            ListDictionary conversation = _memoryCache.Get<ListDictionary>(chatId);

            if (conversation == null)
            {
                //chatId = Guid.NewGuid().ToString();

                _memoryCache.Set(chatId, new ListDictionary());
                conversation = new ListDictionary();
            }
            else
            {
                DictionaryEntry[] myArr = new DictionaryEntry[conversation.Count];
                conversation.CopyTo(myArr, 0);


                foreach (var msg in myArr)
                    chat.AppendMessage(new ChatMessage(ChatMessageRole.FromString((string)msg.Key), (string)msg.Value));
            }


            conversation.Add(USR_MESSAGE, message);
            chat.AppendUserInput(message);

            string response = await chat.GetResponseFromChatbotAsync();

            conversation.Add(SYS_MESSAGE, response);

            _memoryCache.Set(chatId, conversation);

            return Ok(response);
        }

    }
}