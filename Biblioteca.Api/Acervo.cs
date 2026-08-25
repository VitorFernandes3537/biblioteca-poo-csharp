using Biblioteca.Dominio;

namespace Biblioteca.Api;

public class Acervo
{
    private readonly List<ItemAcervo> _itens = [];

    public IReadOnlyList<ItemAcervo> Itens => _itens;

    public void Adicionar(ItemAcervo item)
    {
        _itens.Add(item);
    }
    public ItemAcervo? BuscarPorId(int id)
    {
        return _itens.FirstOrDefault(item => item.Id == id);
    }
}