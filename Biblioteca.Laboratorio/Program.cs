using Biblioteca.Dominio;

var marina = new Pessoa("Marina", DateTime.Today.AddYears(-15));
var caio = new Pessoa("Caio", DateTime.Today.AddYears(-30));
var elias = new Pessoa("Sr. Elias", DateTime.Today.AddYears(-70));

// Cena 1 - Marina tem 15 anos e o DVD e para 16.
var dvdDezesseis = new Dvd("Cidade de Deus", "Fernando Meirelles", 16);
try
{
    marina.Emprestar(dvdDezesseis);
}
catch (ExcecaoDominio excecao)
{
    Console.WriteLine($"Cena 1 - {excecao.Message}");
}

// Cena 2 - Caio esta com tres coisas e quer uma quarta.
caio.Emprestar(new Livro("Dom Casmurro", "Machado de Assis"));
var revistaDoCaio = caio.Emprestar(new Revista("Superinteressante", "Editora Abril"));
caio.Emprestar(new Dvd("Toy Story", "John Lasseter", 0));
try
{
    caio.Emprestar(new Livro("Grande Sertao: Veredas", "Guimaraes Rosa"));
}
catch (ExcecaoDominio excecao)
{
    Console.WriteLine($"Cena 2 - {excecao.Message}");
}

// Cena 3 - Caio devolve uma das tres e pede outra.
revistaDoCaio.RegistrarDevolucao();
caio.Emprestar(new Livro("Grande Sertao: Veredas", "Guimaraes Rosa"));
Console.WriteLine($"Cena 3 - Caio levou a quarta. Em aberto: {caio.QtdEmprestimosEmAberto}");

// Cena 4 - alguem pede um exemplar que ja saiu com outra pessoa.
var livroCompartilhado = new Livro("Vidas Secas", "Graciliano Ramos");
marina.Emprestar(livroCompartilhado);
try
{
    elias.Emprestar(livroCompartilhado);
}
catch (ExcecaoDominio excecao)
{
    Console.WriteLine($"Cena 4 - {excecao.Message}");
}

// Cena 5 - a multa para de subir depois da devolucao.
// A multa agora se calcula contra DataDevolucao quando o item ja voltou,
// e nao mais contra DateTime.Today. Entao o valor congela na devolucao:
// quantas semanas passem depois, MultaAtual responde sempre o mesmo.
var revistaDoElias = elias.Emprestar(new Revista("Veja", "Editora Abril"));
revistaDoElias.RegistrarDevolucao();
decimal multaNaDevolucao = revistaDoElias.MultaAtual;
Console.WriteLine($"Cena 5 - devolvida em {revistaDoElias.DataDevolucao:dd/MM/yyyy}, " +
                  $"{revistaDoElias.QtdDiasAtrasados} dias de atraso, multa {multaNaDevolucao:C}");
Console.WriteLine($"Cena 5 - consultada de novo: {revistaDoElias.MultaAtual:C} " +
                  $"(mesmo valor, travado na data de devolucao)");

// E a devolucao nao pode ser registrada duas vezes, senao a data seria
// sobrescrita e a multa mudaria depois de paga.
try
{
    revistaDoElias.RegistrarDevolucao();
}
catch (ExcecaoDominio excecao)
{
    Console.WriteLine($"Cena 5 - {excecao.Message}");
}

var livroNovo = new Livro("O Cortiço", "Aluísio Azevedo");
var revistaNova = new Revista("Piauí", "Alvinegra");
Console.WriteLine($"Cena 6 - {livroNovo.Titulo} e o Id {livroNovo.Id}, " +
                  $"{revistaNova.Titulo} e o Id {revistaNova.Id}");