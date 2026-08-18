namespace Biblioteca.Api;

// Records de entrada, nao classes de dominio. Eles existem porque o JSON precisa
// virar objeto C# antes de o dominio ser chamado — e o dominio nao aceita ser
// construido pela metade. Um Livro so nasce valido; este record nasce como veio
// da rede, com titulo nulo se o cliente nao mandou.
//
// ATENCAO: sem "required" e sem validacao aqui de proposito. Titulo vazio nao vira
// erro neste arquivo — vira quando o construtor de ItemAcervo recusar, e o
// middleware da Etapa 3 traduzir em 409. Validar duas vezes significa duas
// mensagens diferentes para a mesma regra, e a do dominio e a que manda.
//
// string? porque o cliente pode simplesmente nao mandar o campo. Anotar string
// nao-anulavel seria mentira: o desserializador poe null ali de qualquer forma,
// e o Nullable ligado avisaria no lugar errado.
public record NovoLivro(string? Titulo, string? Autor);

public record NovaRevista(string? Titulo, string? Autor);

// idadeMinima so existe aqui. Era o custo da rota unica: um corpo com um campo
// que dois dos tres tipos ignoram. Com rota por tipo, cada corpo tem so o que
// aquele tipo usa.
// int e nao int?: se o cliente omitir, chega 0 — "sem restricao de idade",
// que e o mesmo default de Livro e Revista.
public record NovoDvd(string? Titulo, string? Autor, int IdadeMinima);
