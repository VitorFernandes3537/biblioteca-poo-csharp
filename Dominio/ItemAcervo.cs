namespace Biblioteca.Dominio;

public abstract class ItemAcervo
{
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public bool Disponibilidade { get; private set; } = true;
    public abstract int PrazoDevolucao { get; set; }
    public abstract decimal MultaDiaAtrasado { get; set; }
    public decimal CalcularMulta(int diasAtrasados)
    {
        return diasAtrasados >= 0 ? diasAtrasados * MultaDiaAtrasado : 0;
    }
}