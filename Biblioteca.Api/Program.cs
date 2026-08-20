using Biblioteca.Api;
using Biblioteca.Dominio;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
// Seed feito FORA de Endpoints, fazendo com que ele sobreviva a requisicições
// Pois todo endpoint que cria algo, esse algo morre assim que ele retornar
var acervo = new Acervo();
AcervoSeed.Popular(acervo);

app.MapGet("/", () => Results.Redirect("/itens"));

app.MapGet("/itens", () => acervo.Itens);

app.MapGet("/itens/{id:int}", (int id) =>
{
    var itemEncontrado = acervo.BuscarPorId(id);
    if (itemEncontrado is null)
    {
        return Results.NotFound(new { erro = $"O Item {id} não foi encontrado!" });
    }
    return Results.Ok(itemEncontrado);
});

app.Run();