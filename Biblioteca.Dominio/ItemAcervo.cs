namespace Biblioteca.Dominio;

public abstract class ItemAcervo
{
    private static int _proximoId = 1;

    public int Id { get; private set; }
    // Deixou de ser "public virtual int IdadeMinima => 0" — e a diferenca nao e de
    // sintaxe, e de natureza. PrazoDevolucao e MultaDiaAtrasado continuam abstract
    // porque sao regra do TIPO: todo Livro tem 14 dias, sempre, e a classe inteira
    // responde igual. IdadeMinima e dado da INSTANCIA: dois DVDs tem classificacoes
    // diferentes. Ela estava disfarcada de comportamento porque so um tipo a usava.
    //
    // private set pelo mesmo motivo de Titulo e Autor: quem muda a classificacao
    // de um item ja criado passa por porta declarada, nao por atribuicao de fora.
    public int IdadeMinima { get; private set; }

    // idadeMinima com default 0 na assinatura da base: todo item TEM classificacao,
    // e a maioria tem a mesma (livre). Quem nao passa nada fica com 0, que e o
    // valor que Livro e Revista ja tinham pelo virtual antigo.
    //
    // DECISAO SUA: o default vive aqui, na base, e nao em cada filha. A alternativa
    // era a base exigir os tres parametros e cada filha passar base(titulo, autor, 0)
    // por escrito — nada implicito, e tres lugares para mudar se o default mudar.
    protected ItemAcervo(string titulo, string autor, int idadeMinima = 0)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ExcecaoDominio("O titulo é obrigatório.");
        }
        // ATENCAO: idade minima negativa nao existe. Sem esta guarda, um -5 passaria
        // e PermiteIdade responderia true para qualquer um, inclusive para idade
        // negativa, se um dia algo calculasse errado. Recusar na entrada e mais
        // barato que descobrir depois.
        if (idadeMinima < 0)
        {
            throw new ExcecaoDominio("A idade mínima não pode ser negativa.");
        }
        Id = _proximoId++;
        Titulo = titulo;
        Autor = autor;
        IdadeMinima = idadeMinima;
    }

    public string Titulo { get; private set; } = string.Empty;
    public string Autor { get; private set; } = string.Empty;
    public bool Disponibilidade { get; private set; } = true;
    public abstract int PrazoDevolucao { get; }
    public abstract decimal MultaDiaAtrasado { get; }
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
    // Separado de AlterarDados de proposito: mudar o titulo de um exemplar e corrigir
    // uma digitacao; mudar a classificacao etaria e reclassificar a obra. Sao decisoes
    // de natureza diferente, e juntar as duas num metodo so obrigaria quem quer
    // corrigir um acento a reinformar a idade minima.
    //
    // ATENCAO: isto NAO revalida os emprestimos ja em aberto. Se um DVD sair de 12
    // para 16 anos, quem ja o levou continua com ele — a regra de idade e checada
    // no momento de emprestar, nao continuamente. Mudar isso exigiria o item conhecer
    // seus emprestimos, e ai o ciclo que acabamos de desfazer voltaria pelo outro lado.
    public void AlterarClassificacao(int idadeMinima)
    {
        if (idadeMinima < 0)
        {
            throw new ExcecaoDominio("A idade mínima não pode ser negativa.");
        }
        IdadeMinima = idadeMinima;
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