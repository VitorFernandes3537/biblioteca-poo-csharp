using Biblioteca.Api;
using Biblioteca.Dominio;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Criado aqui fora, antes do app.Run(): esta linha roda uma vez, na subida.
// ATENCAO: se o new Acervo() estivesse dentro de um endpoint, cada requisicao
// criaria um acervo vazio e o POST anterior teria sumido — o bug que faz
// "eu criei o item e o GET nao acha" e que nao aparece no compilador.
// Na Etapa 9 esta variavel vira um servico registrado como Singleton;
// Singleton e exatamente este comportamento, com nome e ciclo de vida explicitos.
var acervo = new Acervo();

// Dados de partida para haver o que consultar antes do POST existir.
// Saem quando o POST /itens entrar na Etapa 4.
acervo.Adicionar(new Livro("Dom Casmurro", "Machado de Assis"));
acervo.Adicionar(new Revista("Superinteressante", "Editora Abril"));
acervo.Adicionar(new Dvd("Cidade de Deus", "Fernando Meirelles", 16));

// Continua o Hello World do template: prova que a Etapa 1 nao mexeu no que ja subia.
app.MapGet("/", () => "Hello World!");

// Endpoint temporario, so para ver a lista existindo. A Etapa 2 substitui ele
// pelo GET /itens de verdade, com a decisao sobre o que sai no JSON.
app.MapGet("/acervo-teste", () => acervo.Itens.Select(item => $"{item.Id} - {item.Titulo}"));

app.Run();
