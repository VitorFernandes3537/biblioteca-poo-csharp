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
// Um record no lugar de tres. Isto so foi possivel depois que IdadeMinima virou
// dado de ItemAcervo: antes, "idadeMinima" era campo que dois dos tres tipos
// ignoravam, e um corpo com campo inutil era o argumento contra a rota unica.
// Agora todo item tem classificacao, entao o campo vale para os tres.
//
// Tipo e string e nao enum: enum daria erro de desserializacao do framework —
// um 400 com corpo do proprio ASP.NET, fora do padrao { "erro": "..." } do resto
// da API. Com string, a recusa e nossa, com a nossa mensagem.
public record NovoItem(string? Tipo, string? Titulo, string? Autor, int IdadeMinima);


// Um so para os tres tipos: o que se altera e o que ItemAcervo declara,
// nao o que cada filha acrescenta. Nao ha AlteracaoDvd porque IdadeMinima
// nao entra nesta etapa.
public record AlteracaoItem(string? Titulo, string? Autor);

// DateTime e nao DateTime?: data ausente chega como 01/01/0001, que o construtor
// aceita — nao e futura. ATENCAO: uma pessoa nasce com ~2025 anos e nada reclama.
// O dominio nao tem regra de idade maxima; se ela devesse existir, o lugar dela
// e o construtor de Pessoa, nao este record.
public record NovaPessoa(string? Nome, DateTime DataNascimento);

// O mesmo corpo serve para emprestar e devolver: os dois identificam o par
// pessoa + item. Um record so, e nao dois iguais com nomes diferentes.
// ATENCAO: Ids e nao objetos. O cliente aponta para quem ja existe; a API
// nunca cria pessoa ou item a partir do corpo de um emprestimo.
public record MovimentacaoEmprestimo(int PessoaId, int ItemId);
