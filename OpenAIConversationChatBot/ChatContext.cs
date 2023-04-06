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
        public DbSet<Context> Contexts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Chat>().HasKey(m => m.Id);
            builder.Entity<Chat>().HasOne(x => x.Context)
                .WithMany();

            builder.Entity<Context>().HasKey(x => x.Id);
            builder.Entity<Context>();

            base.OnModelCreating(builder);
        }
    }
}
