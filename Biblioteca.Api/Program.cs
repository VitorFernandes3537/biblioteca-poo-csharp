using Biblioteca.Api;
using Biblioteca.Dominio;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var cadastro = new Cadastro();
var acervo = new Acervo();
Seed.Popular(acervo, cadastro);

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ExcecaoDominio excecao)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { erro = excecao.Message });
    }
});

app.MapGet("/", () => Results.Redirect("/itens"));

app.MapGet("/itens", () => acervo.Itens);

app.MapGet("/itens/{id:int}", (int id) =>
{
    var item = acervo.BuscarPorId(id);

    if (item is null)
    {
        return Results.NotFound(new { erro = $"O {id} do Item não foi encontrado!" });
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

app.MapPost("/emprestimos", (NovoEmprestimo dados) =>
{
    var pessoa = cadastro.BuscarPorId(dados.PessoaId);

    if (pessoa is null)
    {
        return Results.NotFound(new { erro = $"Pessoa {dados.PessoaId} não encontrada." });
    }

    var item = acervo.BuscarPorId(dados.ItemId);

    if (item is null)
    {
        return Results.NotFound(new { erro = $"Item {dados.ItemId} não encontrado." });
    }

    var emprestimo = pessoa.Emprestar(item);

    return Results.Ok(new
    {
        pessoa = pessoa.Nome,
        item = item.Titulo,
        prazo = emprestimo.PrazoLimite
    });
});

app.Run();