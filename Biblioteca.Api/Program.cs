


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

ItemAcervo Livro = new Livro("Titulo", "Autor");

app.Run();