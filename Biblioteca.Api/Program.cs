using Biblioteca.Api;
using Biblioteca.Dominio;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var acervo = new Acervo();

acervo.Adicionar(new Livro("Dom Casmurro", "Machado de Assis"));
acervo.Adicionar(new Revista("Superinteressante", "Editora Abril"));
acervo.Adicionar(new Dvd("Cidade de Deus", "Fernando Meirelles", 16));

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

app.Run();
