namespace Biblioteca.Dominio;

public class Revista(string titulo, string autor) : ItemAcervo(titulo, autor)
{
    public override int PrazoDevolucao => 7;
    public override decimal MultaDiaAtrasado => 2m;

}