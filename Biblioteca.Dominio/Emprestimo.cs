namespace Biblioteca.Dominio;

public class Emprestimo
{
    public int PessoaId { get; }
    public ItemAcervo Item { get; }
    public DateTime DataEmprestimo { get; private set; } = DateTime.Today;
    public DateTime PrazoLimite { get; }
    public DateTime? DataDevolucao { get; private set; }
    public Emprestimo(Pessoa pessoa, ItemAcervo item)
    {
        item.MarcarComoEmprestado();
        PessoaId = pessoa.Id;
        Item = item;
        PrazoLimite = DataEmprestimo.AddDays(item.PrazoDevolucao);
    }
    public bool EstaEmAberto => DataDevolucao is null;
    public decimal MultaAtual => Item.CalcularMulta(QtdDiasAtrasados);
    public int QtdDiasAtrasados {
        get
        {
            TimeSpan diasAtrasado = DataReferencia - PrazoLimite;
            return diasAtrasado.Days;
        }
    }
    private DateTime DataReferencia => DataDevolucao ?? DateTime.Today;

    public void RegistrarDevolucao()
    {
        if (!EstaEmAberto)
        {
            throw new ExcecaoDominio("Este empréstimo já foi devolvido.");
        }
        Item.MarcarComoDevolvido();
        DataDevolucao = DateTime.Today;
    }
}
