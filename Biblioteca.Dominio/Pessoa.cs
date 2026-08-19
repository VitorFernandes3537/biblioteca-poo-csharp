namespace Biblioteca.Dominio;

public class Pessoa
{
    public const int LimiteEmprestimosEmAberto = 3;
    // Contador proprio, separado do de ItemAcervo. Pessoa 1 e Item 1 coexistem
    // sem conflito: o que identifica o recurso e a rota (/pessoas/1, /itens/1),
    // nao o numero solto. Um contador unico compartilhado daria Ids "unicos no
    // sistema" — garantia que ninguem pediu, ao custo de numeros esburacados
    // em cada listagem.
    private static int _proximoId = 1;
    public int Id { get; private set; }
    private readonly List<Emprestimo> _emprestimos = [];
    public IReadOnlyList<Emprestimo> Emprestimos => _emprestimos;

    public Pessoa(string nome, DateTime dataNascimento)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ExcecaoDominio("O nome é obrigatório.");
        }
        if (dataNascimento > DateTime.Today)
        {
            throw new ExcecaoDominio("A data de nascimento não pode ser no futuro.");
        }
        Id = _proximoId++;
        Nome = nome;
        DataNascimento = dataNascimento;
    }

    public string Nome { get; private set; }
    public DateTime DataNascimento { get; private set; }

    public int Idade
    {
        get
        {
            int idade = DateTime.Today.Year - DataNascimento.Year;
            if (DataNascimento.Date > DateTime.Today.AddYears(-idade))
            {
                idade--;
            }
            return idade;
        }
    }

    public int QtdEmprestimosEmAberto => _emprestimos.Count(emprestimo => emprestimo.EstaEmAberto);

    public Emprestimo Emprestar(ItemAcervo item)
    {
        if (!item.PermiteIdade(Idade))
        {
            throw new ExcecaoDominio(
                $"{Nome} tem {Idade} anos e o item \"{item.Titulo}\" é para {item.IdadeMinima} anos ou mais.");
        }
        if (QtdEmprestimosEmAberto >= LimiteEmprestimosEmAberto)
        {
            throw new ExcecaoDominio(
                $"{Nome} já está com {QtdEmprestimosEmAberto} itens. Devolva um antes de levar outro.");
        }

        var emprestimo = new Emprestimo(this, item);
        _emprestimos.Add(emprestimo);
        return emprestimo;
    }
}
