using Biblioteca.Api;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var acervo = new Acervo();
AcervoSeed.Popular(acervo);

app.MapGet("/", () => Results.Redirect("/itens"));

app.MapGet("/itens", () => acervo.Itens);

app.Run();