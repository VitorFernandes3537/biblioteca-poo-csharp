using Biblioteca.Dominio;

namespace Biblioteca.Api;

public class Cadastro
{
    private readonly List<Pessoa> _pessoa = [];

    public IReadOnlyList<Pessoa> Pessoa => _pessoa;

    public void Adicionar(Pessoa pessoa)
    {
        _pessoa.Add(pessoa);
    }
    public Pessoa? BuscarPorId(int id)
    {
        return _pessoa.FirstOrDefault(item => item.Id == id);
    }
}