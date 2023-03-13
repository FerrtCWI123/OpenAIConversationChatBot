using Microsoft.EntityFrameworkCore;
using OpenAIConversationChatBot.Models;

namespace OpenAIConversationChatBot
{
    public class ChatContext : DbContext
    {
        public ChatContext(DbContextOptions<ChatContext> options) : base(options)
        {
        }

        public DbSet<Chat> Chats { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Chat>().HasKey(m => m.Id);
            builder.Entity<Chat>().HasData(
                new Chat[]
                {
                    new Chat()
                    {
                        Id = 1,
                        Role = "system",
                        Content = "You are a friend having a conversation with the user and should respond impersonating a friend."
                    }
                });

            base.OnModelCreating(builder);
        }
    }
}
