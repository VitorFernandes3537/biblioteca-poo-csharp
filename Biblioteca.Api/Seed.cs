using Biblioteca.Dominio;

namespace Biblioteca.Api;

public static class Seed
{
    public static void Popular(Acervo acervo, Cadastro cadastro)
    {
        acervo.Adicionar(new Livro("Dom Casmurro", "Machado de Assis"));
        acervo.Adicionar(new Livro("Vidas Secas", "Graciliano Ramos"));
        acervo.Adicionar(new Revista("Superinteressante", "Editora Abril"));
        acervo.Adicionar(new Dvd("Toy Story", "John Lasseter", 0));
        acervo.Adicionar(new Dvd("Cidade de Deus", "Fernando Meirelles", 16));

        cadastro.Adicionar(new Pessoa("Marina", DateTime.Today.AddYears(-15)));
        cadastro.Adicionar(new Pessoa("Caio", DateTime.Today.AddYears(-30)));
    }
}