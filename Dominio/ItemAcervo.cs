namespace Biblioteca.Dominio;

public abstract class ItemAcervo
{
    protected ItemAcervo(string titulo, string autor)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ExcecaoDominio("O titulo é obrigatório.");
        }
        Titulo = titulo;
        Autor = autor;
    }
    public string Titulo { get; private set; } = string.Empty;
    public string Autor { get; private set; } = string.Empty;
    public bool Disponibilidade { get; private set; } = true;
    public abstract int PrazoDevolucao { get; }
    public abstract decimal MultaDiaAtrasado { get; }
    public decimal CalcularMulta(int diasAtrasados)
    {
        return diasAtrasados >= 0 ? diasAtrasados * MultaDiaAtrasado : 0;
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