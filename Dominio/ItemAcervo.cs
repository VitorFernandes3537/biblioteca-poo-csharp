namespace Biblioteca.Dominio;

public abstract class ItemAcervo
{
    public ItemAcervo(string titulo, string autor)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ExcecaoDominio("O titulo é obrigatório.");
        }
        Titulo = titulo;
        Autor = autor;
    }
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public bool Disponibilidade { get; private set; } = true;
    public abstract int PrazoDevolucao { get; }
    public abstract decimal MultaDiaAtrasado { get; }
    public decimal CalcularMulta(int diasAtrasados)
    {
        return diasAtrasados >= 0 ? diasAtrasados * MultaDiaAtrasado : 0;
    }
}