namespace Biblioteca.Dominio;

public class Emprestimo
{
    public ItemAcervo Item { get; }
    public DateTime DataEmprestimo { get; private set; } = DateTime.Today;
    public DateTime PrazoLimite { get; }
    
    public Emprestimo(ItemAcervo item)
    {
        Item = item;
    }

}