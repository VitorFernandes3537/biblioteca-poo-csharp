using Biblioteca.Dominio;
namespace Biblioteca.Api;

public static class AcervoSeed
{
    public static void Popular(Acervo acervo)
    {
        acervo.Adicionar(new Livro("Dom Casmurro", "Machado de Assis"));
        acervo.Adicionar(new Revista("Superinteressante", "Editora Abril"));
        acervo.Adicionar(new Dvd("Cidade de Deus", "Fernando Meirelles", 16));

    }
}