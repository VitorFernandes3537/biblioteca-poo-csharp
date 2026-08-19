using Biblioteca.Api;
using Biblioteca.Dominio;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var acervo = new Acervo();
// Junto do var acervo = new Acervo();
var cadastro = new Cadastro();


// ATENCAO: esta linha PRECISA vir antes de qualquer app.Map... — o middleware
// so enxerga o que roda dentro do await next(), e next() e tudo que foi
// registrado DEPOIS dele. Registrado no fim do arquivo, ele compila, sobe,
// e nao captura nada: o endpoint ja respondeu 500 antes do catch existir.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    // So ExcecaoDominio. Um catch (Exception) generico transformaria
    // NullReference e falha de banco em 409 tambem — e 409 diz "sua regra
    // foi recusada", nao "eu quebrei". Bug da API deve continuar dando 500,
    // alto e feio, para nao passar despercebido.
    catch (ExcecaoDominio excecao)
    {
        // 409 Conflict: o pedido esta bem formado e foi entendido — o estado
        // do dominio e que nao permite. 400 seria "voce escreveu errado";
        // "item ja emprestado" e o oposto disso: escrito certo, recusado.
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { erro = excecao.Message });
    }
});


// Devolve a colecao direto, sem Results.Ok(): quando o retorno nao e um IResult,
// o ASP.NET serializa em JSON e responde 200 sozinho. Results.Ok(acervo.Itens)
// daria exatamente a mesma resposta, com uma camada a mais escrita a mao.
// Acervo vazio sai como [] com 200 — a colecao existe, so esta vazia.
// 404 aqui seria mentira: significaria que /itens nao e recurso nenhum.
app.MapGet("/itens", () => acervo.Itens);

// {id:int} com restricao de rota: /itens/abc nao entra aqui, o roteador ja
// devolve 404 antes do codigo rodar. Sem a restricao, a rota casaria, o bind
// falharia e sairia um 400 com corpo de erro do framework — inconsistente
// com o resto da API, onde erro e sempre { "erro": "..." }.
app.MapGet("/itens/{id:int}", (int id) =>
{
    var item = acervo.BuscarPorId(id);

    // Aqui o retorno e IResult nos dois caminhos, entao o Results. e obrigatorio:
    // os dois ramos precisam ter o mesmo tipo para o compilador aceitar.
    // DECISAO SUA: a mensagem do 404 nomeia o Id procurado. Um 404 de corpo
    // vazio seria igualmente correto em HTTP e piora o diagnostico na aula.
    return item is null
        ? Results.NotFound(new { erro = $"Item {id} não encontrado." })
        : Results.Ok(item);
});

app.MapPost("/itens/livros", (NovoLivro requisicao) =>
{
    // O ! silencia o Nullable: o construtor de ItemAcervo e quem decide se
    // titulo vazio passa, e ele ja recusa null e "" da mesma forma.
    // A ExcecaoDominio sobe daqui direto para o middleware — sem try/catch
    // no endpoint. E isso que a Etapa 3 comprou.
    var livro = new Livro(requisicao.Titulo!, requisicao.Autor!);
    acervo.Adicionar(livro);

    // 201 + Location: quem criou precisa saber o endereco do que criou.
    // 200 devolveria o objeto sem dizer onde ele mora, e o cliente teria
    // de cavar o id do corpo para montar a URL sozinho.
    // ATENCAO: o Location aponta para /itens/{id}, e nao para /itens/livros/{id}.
    // A rota que cria e por tipo; o recurso criado e um item do acervo, e o
    // GET dele e um so. Location aponta para onde o recurso ESTA, nao para
    // onde ele nasceu.
    return Results.Created($"/itens/{livro.Id}", livro);
});

app.MapPost("/itens/revistas", (NovaRevista requisicao) =>
{
    var revista = new Revista(requisicao.Titulo!, requisicao.Autor!);
    acervo.Adicionar(revista);
    return Results.Created($"/itens/{revista.Id}", revista);
});

app.MapPost("/itens/dvds", (NovoDvd requisicao) =>
{
    var dvd = new Dvd(requisicao.Titulo!, requisicao.Autor!, requisicao.IdadeMinima);
    acervo.Adicionar(dvd);
    return Results.Created($"/itens/{dvd.Id}", dvd);
});

app.MapPut("/itens/{id:int}", (int id, AlteracaoItem requisicao) =>
{
    var item = acervo.BuscarPorId(id);
    if (item is null)
    {
        return Results.NotFound(new { erro = $"Item {id} não encontrado." });
    }

    // Titulo vazio sobe como ExcecaoDominio daqui e vira 409 no middleware —
    // mesma regra, mesma mensagem e mesmo status do POST. Foi para isso que a
    // validacao ficou dentro de AlterarDados em vez de virar um if neste bloco.
    item.AlterarDados(requisicao.Titulo!, requisicao.Autor!);

    // DECISAO SUA: 200 com o item alterado. 204 No Content tambem seria correto
    // em HTTP e economizaria o corpo, mas obrigaria um GET logo depois para o
    // cliente ver como o recurso ficou. Devolver o objeto encerra a conversa.
    return Results.Ok(item);
});

app.MapDelete("/itens/{id:int}", (int id) =>
{
    var item = acervo.BuscarPorId(id);
    if (item is null)
    {
        // 404 e nao 204: o cliente pediu para apagar algo que nunca existiu, e
        // saber disso e util. DECISAO SUA — ha quem defenda 204 aqui, porque o
        // efeito desejado ("esse item nao existe mais") ja vale de qualquer forma.
        return Results.NotFound(new { erro = $"Item {id} não encontrado." });
    }

    // Item emprestado sobe ExcecaoDominio daqui e vira 409 no middleware.
    acervo.Remover(item);

    // 204 No Content: deu certo e nao ha o que devolver. Devolver o objeto
    // apagado seria estranho — o corpo descreveria um recurso que a propria
    // resposta acabou de dizer que nao existe mais.
    return Results.NoContent();
});

app.MapGet("/pessoas", () => cadastro.Pessoas.Select(PessoaResposta.De));

app.MapGet("/pessoas/{id:int}", (int id) =>
{
    var pessoa = cadastro.BuscarPorId(id);
    return pessoa is null
        ? Results.NotFound(new { erro = $"Pessoa {id} não encontrada." })
        // ATENCAO: PessoaResposta.De(pessoa), nunca pessoa. Passar o objeto do
        // dominio aqui compila, sobe, e so estoura em 500 quando essa pessoa
        // tiver o primeiro emprestimo. Ate la, parece funcionar.
        : Results.Ok(PessoaResposta.De(pessoa));
});

app.MapPost("/pessoas", (NovaPessoa requisicao) =>
{
    // Nome vazio e data futura sobem como ExcecaoDominio e viram 409 no middleware.
    var pessoa = new Pessoa(requisicao.Nome!, requisicao.DataNascimento);
    cadastro.Adicionar(pessoa);
    return Results.Created($"/pessoas/{pessoa.Id}", PessoaResposta.De(pessoa));
});


app.MapPost("/emprestimos", (MovimentacaoEmprestimo requisicao) =>
{
    var pessoa = cadastro.BuscarPorId(requisicao.PessoaId);
    if (pessoa is null)
    {
        return Results.NotFound(new { erro = $"Pessoa {requisicao.PessoaId} não encontrada." });
    }

    var item = acervo.BuscarPorId(requisicao.ItemId);
    if (item is null)
    {
        return Results.NotFound(new { erro = $"Item {requisicao.ItemId} não encontrado." });
    }

    // ATENCAO: pessoa.Emprestar(item), NUNCA new Emprestimo(pessoa, item).
    // O construtor compila, marca o item como emprestado e pula tudo: nao checa
    // idade, nao checa o limite de tres, e o emprestimo nao entra na lista da
    // pessoa — QtdEmprestimosEmAberto continua no valor antigo e o limite some.
    // Nada disso lanca. A unica defesa e nao chamar o construtor daqui.
    // As tres recusas (idade, limite, item ja emprestado) sobem como
    // ExcecaoDominio e viram 409 no middleware, sem try/catch neste bloco.
    var emprestimo = pessoa.Emprestar(item);

    // DECISAO SUA: 201 sem Location. Location aponta para o GET do recurso criado,
    // e nao existe GET /emprestimos/{id} — emprestimo nao tem Id proprio, ele se
    // identifica pelo par pessoa+item. Location apontando para /pessoas/{id}
    // seria mentira: aquela URL devolve a pessoa, nao este emprestimo.
    return Results.Created(string.Empty, EmprestimoResposta.De(emprestimo));
});

app.MapPost("/devolucoes", (MovimentacaoEmprestimo requisicao) =>
{
    var pessoa = cadastro.BuscarPorId(requisicao.PessoaId);
    if (pessoa is null)
    {
        return Results.NotFound(new { erro = $"Pessoa {requisicao.PessoaId} não encontrada." });
    }

    // A assimetria da etapa: emprestar e da Pessoa, devolver e do Emprestimo.
    // Entao aqui a API precisa ACHAR o emprestimo — o par (pessoa, item) em aberto.
    // EstaEmAberto no filtro nao e detalhe: sem ele, um item emprestado, devolvido
    // e emprestado de novo casaria com o registro antigo, ja fechado, e a
    // devolucao nova estouraria "ja foi devolvido" apontando para o emprestimo errado.
    var emprestimo = pessoa.Emprestimos.FirstOrDefault(
        e => e.Item.Id == requisicao.ItemId && e.EstaEmAberto);

    if (emprestimo is null)
    {
        // 404 e nao 409: nao ha emprestimo em aberto desse par para devolver —
        // o recurso que a requisicao aponta nao existe. 409 seria o caso em que
        // ele existe e o dominio recusa a operacao.
        return Results.NotFound(new
        {
            erro = $"{pessoa.Nome} não está com o item {requisicao.ItemId} emprestado."
        });
    }

    emprestimo.RegistrarDevolucao();

    // 200 e nao 201: a devolucao nao criou recurso nenhum, alterou um existente.
    // E devolver o corpo importa aqui — e nele que sai a multa apurada.
    return Results.Ok(EmprestimoResposta.De(emprestimo));
});




// Temporario, so para provar o middleware. Sai quando o POST /itens entrar.
app.MapGet("/estouro-teste", () => new Livro("", "Ninguem"));
app.Run();
