namespace OpenAIConversationChatBot.DTOs
{
    public class Choice
    {
        public Message Message { get; set; }
        public string FinishReason { get; set; }
        public int Index { get; set; }
    }

}
