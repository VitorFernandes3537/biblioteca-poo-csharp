using Biblioteca.Dominio;

namespace Biblioteca.Api;

public record NovoEmprestimo(int PessoaId, int ItemId);


public record NovoDvd(string Titulo, string Autor, int IdadeMinima);
public record NovoLivro(string Titulo, string Autor);
public record NovoRevista(string Titulo, string Autor);

public record DtoItem(int Id, string Titulo, string Autor, bool Disponibilidade, int IdadeMinima)
{
    public static DtoItem DoAcervo(ItemAcervo i) 
    => new(i.Id, i.Titulo, i.Autor, i.Disponibilidade, i.IdadeMinima);
};