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

app.MapGet("/itens", () =>
{
    List<DtoItem> listaDto = [];
    foreach (var item in acervo.Itens)
    {
        listaDto.Add(DtoItem.DoAcervo(item));
    }
    return Results.Ok(listaDto);
});

app.MapGet("/itens/{id:int}", (int id) =>
{
    var item = acervo.BuscarPorId(id);

    if (item is null)
    {
        return Results.NotFound(new { erro = $"O {id} do Item não foi encontrado!" });
    }

    return Results.Ok(DtoItem.DoAcervo(item));

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

// Endpoints de criação de cada tipo de item. 
app.MapPost("/itens/livro", (NovoLivro dados) =>
{
    var livro = new Livro(dados.Titulo, dados.Autor);
    acervo.Adicionar(livro);
    
    return Results.Created($"/itens/{livro.Id}", DtoItem.DoAcervo(livro));
});

app.MapPost("/itens/revista", (NovoRevista dados) =>
{
    var revista = new Revista(dados.Titulo, dados.Autor);
    acervo.Adicionar(revista);
    
    return Results.Created($"/itens/{revista.Id}", DtoItem.DoAcervo(revista));
});

app.MapPost("/itens/dvd", (NovoDvd dados) =>
{
    var dvd = new Dvd(dados.Titulo, dados.Autor, dados.IdadeMinima);
    acervo.Adicionar(dvd);
    
    return Results.Created($"/itens/{dvd.Id}", DtoItem.DoAcervo(dvd));
});

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