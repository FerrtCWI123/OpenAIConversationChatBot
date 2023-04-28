using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAIConversationChatBot;
using OpenAIConversationChatBot.DTOs;
using OpenAIConversationChatBot.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace GPTConversationChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private const string USR_MESSAGE = "user";
        private const string SYS_MESSAGE = "system";
        private const string CONTEXT_MESSAGE = "context";
        private const int INTERATION_LIMIT = 5;

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


        [HttpPost("Message/{id}")]
        public async Task<IActionResult> PostConversationAsync(string id, [FromBody] string message)
        {
            try
            {
                string chatId = id;
                OpenAIAPI api = new OpenAIAPI("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");
                var chat = api.Chat.CreateConversation();

                List<Chat> conversation = _memoryCache.Get<List<Chat>>(chatId);

                if (conversation == null)
                {
                    _memoryCache.Set(chatId, new List<Chat>());
                    conversation = new List<Chat>();
                }
                else
                {
                    if(conversation.Where(p => p.Role.Equals(USR_MESSAGE)).Count() > INTERATION_LIMIT)
                    {
                        chat.AppendUserInput("Give me the context of this conversation for use later here");
                        string responseContext = await chat.GetResponseFromChatbotAsync();

                        _memoryCache.Set(chatId, new List<Chat> { new Chat { Role = CONTEXT_MESSAGE, Content = message } });

                        return BadRequest($"O numero maximo de interações({INTERATION_LIMIT}) do usuário foi atingido");
                    }
                    else if(conversation.Count() == 1 && conversation[0].Role.Equals(CONTEXT_MESSAGE))
                    {
                        var newChat = $"- Using the follow context: {Environment.NewLine}{Environment.NewLine}" +
                                      $" {conversation[0].Content}{Environment.NewLine}{Environment.NewLine}" +
                                      $"- Continue the conversation(with a single message) pretending you are a person with the user send the next message \"{message}\"";

                        conversation = new List<Chat> { new Chat { Role = USR_MESSAGE, Content = newChat } };

                        chat.AppendUserInput(newChat);
                        string responseContext = await chat.GetResponseFromChatbotAsync();

                        conversation.Add(new Chat { Role = SYS_MESSAGE, Content = message });
                        _memoryCache.Set(chatId, conversation);

                        return Ok(responseContext);
                    }

                    foreach (var msg in conversation)
                        chat.AppendMessage(new ChatMessage(ChatMessageRole.FromString(msg.Role), msg.Content));
                }


                conversation.Add(new Chat { Role = USR_MESSAGE, Content = message });
                chat.AppendUserInput(message);

                string response = await chat.GetResponseFromChatbotAsync();

                conversation.Add(new Chat { Role = SYS_MESSAGE, Content = message });

                _memoryCache.Set(chatId, conversation);

                return Ok($"{response}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something went wrong");
                throw;
            }
        }

        [HttpPost("Start/{ContextId}")]
        public async Task<IActionResult> StatChatAsync([FromRoute(Name = "ContextId")] int contextId)
        {
            try
            {
                var teste = _chatContext.Contexts.ToList();
                var context = _chatContext.Contexts.FirstOrDefault(x => x.Id == contextId);

                if (context == null)
                    return BadRequest($"Context \"{contextId}\" does not exist.");

                string chatId = Guid.NewGuid().ToString();
                OpenAIAPI api = new OpenAIAPI("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");
                var chat = api.Chat.CreateConversation();

                List<Chat> conversation = new List<Chat>();

                _memoryCache.Set(chatId, conversation);

                chat.AppendUserInput(context.Prompt);

                string response = await chat.GetResponseFromChatbotAsync();

                conversation.Add(new Chat { Role = CONTEXT_MESSAGE, Content = response });

                _memoryCache.Set(chatId, conversation);

                return Ok(new {id = chatId, content = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something went wrong");
                throw;
            }
        }
    }
}