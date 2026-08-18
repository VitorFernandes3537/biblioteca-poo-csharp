using Biblioteca.Dominio;

namespace Biblioteca.Api;

// Mora na Api, e nao no Dominio, de proposito: guardar coisa entre requisicoes
// e problema de quem atende requisicao. O Dominio nao sabe que HTTP existe —
// se esta classe descesse para la, a primeira regra da solucao ja estaria quebrada.
public class Acervo
{
    // readonly na variavel, nao na lista: ninguem troca _itens por outra lista,
    // mas Adicionar continua podendo mexer no conteudo dela.
    private readonly List<ItemAcervo> _itens = [];

    // IReadOnlyList e nao List: quem le o acervo nao pode Add nem Remove por fora.
    // Mesmo padrao de Pessoa.Emprestimos — mudanca so entra pelos metodos daqui.
    public IReadOnlyList<ItemAcervo> Itens => _itens;

    public void Adicionar(ItemAcervo item)
    {
        _itens.Add(item);
    }

    // ItemAcervo? com interrogacao: "nao encontrei" e uma resposta legitima,
    // nao um erro. Quem chama decide o que fazer — a Etapa 2 transforma esse
    // null em 404. Lancar excecao aqui seria decidir pelo endpoint.
    public ItemAcervo? BuscarPorId(int id)
    {
        return _itens.FirstOrDefault(item => item.Id == id);
    }

    // Primeira regra do sistema que NAO nasce no dominio, e vale reparar no porque:
    // "nao remover item emprestado" precisa de duas coisas ao mesmo tempo — o estado
    // do item e a colecao onde ele esta. ItemAcervo conhece o proprio estado e nao
    // sabe que existe um acervo; nao ha onde escrever isso la dentro.
    //
    // ExcecaoDominio mesmo estando fora do Dominio: o middleware da Etapa 3 traduz
    // esse tipo em 409, e a recusa aqui e da mesma natureza — pedido bem formado,
    // estado que nao permite. Um tipo de excecao novo obrigaria um segundo catch
    // para produzir exatamente a mesma resposta.
    public void Remover(ItemAcervo item)
    {
        if (!item.Disponibilidade)
        {
            // ATENCAO: sem esta guarda o Emprestimo na lista da Pessoa continuaria
            // em aberto apontando para um item fora do acervo. Ele seguiria contando
            // para o limite de tres, e a devolucao ainda funcionaria — devolvendo
            // ao acervo um item que nao esta mais nele. Nada disso lanca; so fica errado.
            throw new ExcecaoDominio(
                $"O item \"{item.Titulo}\" está emprestado e não pode ser removido.");
        }
        _itens.Remove(item);
    }

}