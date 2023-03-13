using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAIConversationChatBot.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Chats",
                columns: new[] { "Id", "Content", "Role" },
                values: new object[] { 1, "You are a friend having a conversation with the user and should respond impersonating a friend.", "system" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Chats",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
