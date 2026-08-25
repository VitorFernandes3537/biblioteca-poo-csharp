using Biblioteca.Api;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var cadastro = new Cadastro();
var acervo = new Acervo();
Seed.Popular(acervo, cadastro);

app.MapGet("/", () => Results.Redirect("/itens"));

app.MapGet("/itens", () => acervo.Itens);

app.MapGet("/itens/{id:int}", (int id) =>
{
    var item = acervo.BuscarPorId(id);

    if(item is null)
    {
        return Results.NotFound(new { erro = $"O {id} do Item não foi encontrado!"});
    }

    return Results.Ok(item);

    // return item is null
    // ? Results.NotFound(new { erro = $"O {id} do Item não foi encontrado!"})
    // : Results.Ok(item);
});

app.MapGet("/pessoas", () => cadastro.Pessoa.Select(pessoa => new
{
    pessoa.Id,
    pessoa.Idade,
    pessoa.Nome,
    EmprestimoEmAberto = pessoa.QtdEmprestimosEmAberto
}));

app.Run();