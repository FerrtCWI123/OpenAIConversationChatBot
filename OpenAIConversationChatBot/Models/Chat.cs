using System.ComponentModel.DataAnnotations.Schema;

namespace OpenAIConversationChatBot.Models
{
    public class Chat
    {
        public int Id { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }

        [ForeignKey("ContextId")]
        public virtual Context Context { get; set; }
        public virtual int ContextId { get; set; }
    }
}
