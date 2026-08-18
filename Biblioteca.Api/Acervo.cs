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
}