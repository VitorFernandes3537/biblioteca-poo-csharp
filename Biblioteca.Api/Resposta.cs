using Biblioteca.Dominio;

namespace Biblioteca.Api;

// O motivo deste arquivo: serializar Pessoa direto lanca. Pessoa tem Emprestimos,
// cada Emprestimo tem Pessoa, que tem Emprestimos — o serializador anda em circulo
// e estoura em 500. Este record corta o ciclo por nao ter a lista.
//
// A alternativa recusada foi ReferenceHandler.IgnoreCycles: uma linha de config,
// e a resposta sairia com "pessoa": null enterrado dentro de cada emprestimo —
// um campo que existe, e sempre nulo, e o cliente nao tem como saber por que.
// [JsonIgnore] em Emprestimo.Pessoa foi recusada por outro motivo: poria um
// atributo de serializacao dentro do Dominio, que nao pode saber que JSON existe.
//
// Idade e QtdEmprestimosEmAberto sao calculadas em Pessoa e entram aqui porque
// interessam a quem le. O que sai na resposta e escolha da Api, nao sobra do
// formato interno do objeto.
public record PessoaResposta(
    int Id,
    string Nome,
    DateTime DataNascimento,
    int Idade,
    int QtdEmprestimosEmAberto)
{
    // Metodo de fabrica no proprio record: a traducao Pessoa -> resposta fica
    // num lugar so. Espalhada pelos endpoints, cada um acabaria devolvendo um
    // conjunto ligeiramente diferente de campos.
    public static PessoaResposta De(Pessoa pessoa) => new(
        pessoa.Id,
        pessoa.Nome,
        pessoa.DataNascimento,
        pessoa.Idade,
        pessoa.QtdEmprestimosEmAberto);
}

// Continua existindo, e por um motivo diferente do de antes. Antes ela cortava um
// ciclo; agora ela COMPOE: o emprestimo tem os Ids, o nome vem da pessoa, o titulo
// vem do item. Isto e trabalho normal de camada de fronteira — juntar dados de
// fontes diferentes para formar a resposta que o cliente precisa.
//
// Repare no que o De() passou a exigir: a pessoa por fora. O emprestimo sozinho
// nao sabe mais o nome — e nao deve saber. Quem monta a resposta e que sabe onde
// buscar. Esse e o custo da direcao imposta, e ele e explicito, nao escondido.
public record EmprestimoResposta(
    int PessoaId,
    string PessoaNome,
    int ItemId,
    string ItemTitulo,
    DateTime DataEmprestimo,
    DateTime PrazoLimite,
    DateTime? DataDevolucao,
    bool EstaEmAberto,
    int QtdDiasAtrasados,
    decimal MultaAtual)
{
    public static EmprestimoResposta De(Emprestimo emprestimo, Pessoa pessoa) => new(
        emprestimo.PessoaId,
        pessoa.Nome,
        emprestimo.Item.Id,
        emprestimo.Item.Titulo,
        emprestimo.DataEmprestimo,
        emprestimo.PrazoLimite,
        emprestimo.DataDevolucao,
        emprestimo.EstaEmAberto,
        emprestimo.QtdDiasAtrasados,
        emprestimo.MultaAtual);
}

