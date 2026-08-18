namespace Biblioteca.Dominio;

public abstract class ItemAcervo
{
    private static int _proximoId = 1;

    public int Id { get; private set; }
    protected ItemAcervo(string titulo, string autor)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ExcecaoDominio("O titulo é obrigatório.");
        }
        Id = _proximoId++;
        Titulo = titulo;
        Autor = autor;
    }
    public string Titulo { get; private set; } = string.Empty;
    public string Autor { get; private set; } = string.Empty;
    public bool Disponibilidade { get; private set; } = true;
    public abstract int PrazoDevolucao { get; }
    public abstract decimal MultaDiaAtrasado { get; }
    public virtual int IdadeMinima => 0;
    public bool PermiteIdade(int idade)
    {
        return idade >= IdadeMinima;
    }
    public decimal CalcularMulta(int diasAtrasados)
    {
        return diasAtrasados >= 0 ? diasAtrasados * MultaDiaAtrasado : 0;
    }
    // A porta declarada para alteracao. Existe porque o PUT precisava escrever
    // e private set nao abre — e abrir o set para public teria sido a saida errada:
    // com set publico, qualquer linha em qualquer lugar poderia zerar um titulo,
    // e o construtor que valida viraria enfeite.
    //
    // O que NAO esta aqui e a parte que importa: Id nao muda (e a identidade),
    // Disponibilidade nao muda (so Emprestar e Devolver mexem nela). Um PUT capaz
    // de marcar disponibilidade=true num item emprestado devolveria o item sem
    // fechar o Emprestimo — a lista da Pessoa continuaria apontando para ele.
    public void AlterarDados(string titulo, string autor)
    {
        // A mesma regra do construtor, repetida de proposito: os dois caminhos
        // de escrita precisam recusar as mesmas coisas. Se um dia a regra mudar,
        // ela muda nos dois — e e por isso que este metodo vive aqui, e nao no
        // endpoint, onde a Api teria de saber o que o dominio considera valido.
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ExcecaoDominio("O titulo é obrigatório.");
        }
        Titulo = titulo;
        Autor = autor;
    }

    public void MarcarComoDevolvido()
    {
        if (Disponibilidade)
        {
            throw new ExcecaoDominio("O item Não está emprestado");
        }
        Disponibilidade = true;
    }
    public void MarcarComoEmprestado()
    {
        if (!Disponibilidade)
        {
            throw new ExcecaoDominio("O Item Já está emprestado");
        }
        Disponibilidade = false;
    }
}