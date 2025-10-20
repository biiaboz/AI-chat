using ChatApi.Data;
using ChatApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => new { ok = true, service = "ChatApi" });

app.MapPost("/auth/register", async (AppDbContext db, string nickname) =>
{
    nickname = (nickname ?? "").Trim();
    if (nickname == "") nickname = "misafir";

    var exists = await db.Users.FirstOrDefaultAsync(u => u.Nickname == nickname);
    if (exists is not null) return new { userId = exists.Id, nickname = exists.Nickname };

    var user = new User { Nickname = nickname };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return new { userId = user.Id, nickname = user.Nickname };
});

app.MapPost("/messages", async (AppDbContext db, HttpClient http, IConfiguration cfg,
    int userId, string text) =>
{
    var aiUrl = cfg["AI_ENDPOINT"];
    if (string.IsNullOrWhiteSpace(aiUrl))
        return Results.BadRequest(new { error = "AI_ENDPOINT not configured" });

    var payload = new { data = new[] { text } };
    var res = await http.PostAsJsonAsync(aiUrl, payload);
    if (!res.IsSuccessStatusCode)
        return Results.Problem($"AI service error: {(int)res.StatusCode}");

    var aiJson = await res.Content.ReadFromJsonAsync<dynamic>();
    string sentiment = "neutral";
    double score = 0.0;
    try
    {
        var first = aiJson?.data?[0];
        sentiment = (string)(first?.label ?? "neutral");
        score = (double)(first?.score ?? 0.0);
    }
    catch {}

    var message = new Message
    {
        Text = text,
        Sentiment = sentiment,
        Score = score,
        UserId = userId
    };

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    return Results.Ok(new { message.Id, message.Text, message.Sentiment, message.Score, message.CreatedAt });
});

app.MapGet("/messages/latest", async (AppDbContext db) =>
{
    var list = await db.Messages
        .OrderByDescending(m => m.Id)
        .Take(50)
        .Select(m => new { m.Id, m.Text, m.Sentiment, m.Score, m.CreatedAt, m.UserId })
        .ToListAsync();
    list.Reverse();
    return list;
});

app.Run();
