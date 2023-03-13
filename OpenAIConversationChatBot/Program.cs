using Microsoft.EntityFrameworkCore;
using OpenAIConversationChatBot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<OpenAPISettings>(builder.Configuration.GetSection(OpenAPISettings.SECTION_NAME));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var connection = builder.Configuration["SqLiteConnection:SqliteConnectionString"];
builder.Services.AddDbContext<ChatContext>(options =>
    options.UseSqlite(connection)
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
