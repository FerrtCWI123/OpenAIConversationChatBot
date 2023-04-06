using Microsoft.AspNetCore.Mvc;
using OpenAIConversationChatBot;

namespace GPTConversationChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContextController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly ChatContext _chatContext;

        public ContextController(ILogger<ChatController> logger, ChatContext chatContext)
        {
            _logger = logger;
            _chatContext = chatContext;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(_chatContext.Contexts.Select(x => x));
        }

        [HttpGet("{Id}")]
        public IActionResult Get([FromRoute] int id)
        {
            return Ok(_chatContext.Contexts.FirstOrDefault(x => x.Id == id));
        }
    }
}