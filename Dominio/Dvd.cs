namespace Biblioteca.Dominio;

public class Dvd(string titulo, string autor) : ItemAcervo(titulo, autor)
{
    public override int PrazoDevolucao => 3;
    public override decimal MultaDiaAtrasado => 3m;

}