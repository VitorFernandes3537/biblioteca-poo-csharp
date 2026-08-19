using Biblioteca.Dominio;

namespace Biblioteca.Api;

// Gemeo do Acervo, para pessoas. Duas classes quase iguais e proposital por ora:
// a Etapa 9 vai olhar para as duas juntas e perguntar se cabe uma abstracao.
// Abstrair agora, com um caso so de cada lado, seria adivinhar.
public class Cadastro
{
    private readonly List<Pessoa> _pessoas = [];

    public IReadOnlyList<Pessoa> Pessoas => _pessoas;

    public void Adicionar(Pessoa pessoa)
    {
        _pessoas.Add(pessoa);
    }

    public Pessoa? BuscarPorId(int id)
    {
        return _pessoas.FirstOrDefault(pessoa => pessoa.Id == id);
    }
}
