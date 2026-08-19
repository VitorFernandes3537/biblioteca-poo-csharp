using Biblioteca.Api;
using Biblioteca.Dominio;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var acervo = new Acervo();
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

app.MapPost("/itens", (NovoItem requisicao) =>
{
    // O switch que a heranca cobra. Ele existe porque ItemAcervo e abstrata e o
    // JSON nao carrega tipo: alguem precisa dizer qual classe instanciar, e essa
    // informacao nao esta no dado. E o mesmo problema que ORM resolve com coluna
    // discriminadora, e que o System.Text.Json resolveria com [JsonPolymorphic] —
    // recusado porque poria atributo de serializacao dentro do Dominio.
    //
    // ToLowerInvariant e nao ToLower(): ToLower() usa a cultura do sistema, e em
    // turco o "I" minusculo nao e "i". Comparacao de palavra-chave de protocolo
    // nao pode depender do idioma da maquina onde a API roda.
    ItemAcervo item = requisicao.Tipo?.ToLowerInvariant() switch
    {
        "livro" => new Livro(requisicao.Titulo!, requisicao.Autor!),
        "revista" => new Revista(requisicao.Titulo!, requisicao.Autor!),
        "dvd" => new Dvd(requisicao.Titulo!, requisicao.Autor!, requisicao.IdadeMinima),

        // ATENCAO: o descarte _ cobre null, "" e "revistta" — os tres casos em que
        // nao da para saber o que criar. Sem este braco o switch lancaria
        // SwitchExpressionException, que nao e ExcecaoDominio: escaparia do
        // middleware e viraria 500.
        _ => throw new ExcecaoDominio(
            $"Tipo \"{requisicao.Tipo}\" não existe. Use livro, revista ou dvd.")
    };

    acervo.Adicionar(item);
    return Results.Created($"/itens/{item.Id}", item);
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


app.MapGet("/pessoas/{id:int}/emprestimos", (int id) =>
{
    var pessoa = cadastro.BuscarPorId(id);
    if (pessoa is null)
    {
        // 404 da PESSOA, e nao lista vazia: /pessoas/99/emprestimos com pessoa
        // inexistente nao e "essa pessoa nao tem emprestimos" — e "essa pessoa
        // nao existe". Devolver [] responderia a pergunta errada.
        return Results.NotFound(new { erro = $"Pessoa {id} não encontrada." });
    }

    // Aqui lista vazia E a resposta certa: a pessoa existe e nunca pegou nada.
    return Results.Ok(pessoa.Emprestimos.Select(
        emprestimo => EmprestimoResposta.De(emprestimo, pessoa)));
});

app.MapGet("/emprestimos", (bool? emAberto) =>
{
    // SelectMany achata: para cada pessoa, pega a lista dela, e junta tudo numa so.
    // Guardar a pessoa junto (o objeto anonimo) porque EmprestimoResposta.De precisa
    // dela — o emprestimo tem so o PessoaId depois que quebramos o ciclo.
    //
    // ATENCAO: esta e a evidencia de que emprestimo NAO e uma colecao de primeira
    // classe neste desenho. Item e Pessoa tem Acervo e Cadastro; emprestimo e
    // residuo do agregado. Se um dia houver consulta pesada por emprestimo — por
    // periodo, por atraso, por multa em aberto — este SelectMany vira o gargalo,
    // e a saida seria dar a ele colecao propria.
    var emprestimos = cadastro.Pessoas
        .SelectMany(pessoa => pessoa.Emprestimos.Select(
            emprestimo => new { emprestimo, pessoa }));

    // Filtro opcional: sem o parametro, sai o historico inteiro. ?emAberto=true traz
    // so os pendentes; ?emAberto=false, so os ja devolvidos.
    // bool? e nao bool: com bool nao-anulavel, omitir o parametro daria false, e a
    // rota sem filtro devolveria so os devolvidos — o oposto de "sem filtro".
    if (emAberto is not null)
    {
        emprestimos = emprestimos.Where(par => par.emprestimo.EstaEmAberto == emAberto);
    }

    return emprestimos.Select(par => EmprestimoResposta.De(par.emprestimo, par.pessoa));
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
    return Results.Created(string.Empty, EmprestimoResposta.De(emprestimo, pessoa));
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
    return Results.Ok(EmprestimoResposta.De(emprestimo, pessoa));
});


app.Run();
