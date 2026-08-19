# Acoplamento e dependência circular — o caso real deste domínio

Material didático de assunto único, escrito a partir de um problema **que existe neste
repositório**. Não é exemplo inventado: o ciclo descrito aqui foi construído, custou um arquivo
inteiro, e foi corrigido depois.

> **Estado:** a correção **foi aplicada** no commit `c1aeb59`. O código atual tem
> `Emprestimo.PessoaId`. O que este documento chama de "antes" é o que existiu até então, e está
> preservado no histórico do git — `git show 978e9df:Biblioteca.Dominio/Emprestimo.cs` mostra a
> versão com o ciclo, útil para comparar em aula.

Referências completas ao fim. **Leia a ressalva sobre as fontes antes de citar em aula.**

---

## O caso, em uma tela

Duas classes do domínio, cada uma guardando referência à outra:

```csharp
public class Pessoa
{
    private readonly List<Emprestimo> _emprestimos = [];   // Pessoa -> Emprestimo
    public int QtdEmprestimosEmAberto => _emprestimos.Count(e => e.EstaEmAberto);
}

public class Emprestimo
{
    public Pessoa Pessoa { get; }                          // Emprestimo -> Pessoa
    public ItemAcervo Item { get; }
}
```

Em memória, isto é literal:

```
pessoa.Emprestimos[0].Pessoa.Emprestimos[0].Pessoa.Emprestimos[0] ...
```

Um caminho infinito. Chama-se **associação bidirecional**, e é o que gerou o problema.

---

## A distinção que essa aula tem que deixar clara

Há duas coisas acontecendo aqui, e confundi-las produz uma lição errada.

### O que NÃO é o problema: `new Emprestimo(...)` dentro de `Pessoa`

```csharp
public Emprestimo Emprestar(ItemAcervo item)
{
    if (!item.PermiteIdade(Idade)) throw new ExcecaoDominio(...);
    if (QtdEmprestimosEmAberto >= LimiteEmprestimosEmAberto) throw new ExcecaoDominio(...);

    var emprestimo = new Emprestimo(this, item);   // <- instanciação dentro de outra classe
    _emprestimos.Add(emprestimo);
    return emprestimo;
}
```

Sim, `Pessoa` instancia `Emprestimo`. Sim, isso é acoplamento. **E está certo.**

É um padrão nomeado e documentado: **Factory Method on Aggregate Root**, descrito por Vaughn
Vernon no capítulo 11 de *Implementing Domain-Driven Design*. O exemplo canônico do livro é
`Product.planBacklogItem()` — um agregado que cria e devolve outro objeto. Estruturalmente
idêntico.

A justificativa registrada: colocar o Factory Method na raiz **esconde os detalhes de criação
dos clientes externos e devolve à raiz a responsabilidade pela integridade do agregado**. Fontes
sobre o livro registram ainda que *Factory Methods na raiz são um bom lugar para invariantes* —
permitem validar regras no momento da instanciação.

E há um princípio anterior ao DDD que explica *por que ali*: **Information Expert**, um dos nove
padrões GRASP de Craig Larman, publicado em *Applying UML and Patterns* (1997).

> **Problema:** qual é o princípio básico para atribuir responsabilidades a objetos?
> **Solução:** atribua a responsabilidade à classe **que tem a informação necessária para
> cumpri-la**.

Aplicado ao caso: quem tem a informação para decidir se um empréstimo pode existir?

| informação necessária | onde está |
|---|---|
| quantos empréstimos em aberto a pessoa tem | `Pessoa._emprestimos` |
| qual a idade da pessoa | `Pessoa.DataNascimento` |
| qual a idade mínima do item | `ItemAcervo.IdadeMinima` |

Duas das três informações estão em `Pessoa`. Por Information Expert, a responsabilidade é dela.
Um `Emprestimo.Criar(pessoa, item)` estático teria que **perguntar** as duas à `Pessoa` para
decidir — e Larman registra que o benefício declarado do princípio é justamente **minimizar
dependências**.

> **A lição:** instanciar uma classe dentro de outra **não é erro por si só**. É acoplamento, e
> acoplamento é o preço de qualquer colaboração entre objetos. A pergunta certa nunca é "há
> acoplamento?" — é **"este acoplamento está pagando alguma regra?"**

### O que É o problema: `Emprestimo.Pessoa`

Agora olhe a referência de volta, e pergunte a mesma coisa:

| referência | que regra ela sustenta? |
|---|---|
| `Pessoa` → `Emprestimo` | o **limite de três em aberto**. Sem a lista, `QtdEmprestimosEmAberto` não existe e a regra some. |
| `Emprestimo` → `Pessoa` | **nenhuma.** Nenhum método de `Emprestimo` usa `Pessoa` para decidir coisa alguma. |

`RegistrarDevolucao()` não consulta a pessoa. `MultaAtual` não consulta a pessoa. `EstaEmAberto`
não consulta a pessoa. A referência existia por **conveniência**: para a resposta da API saber o
nome de quem pegou o item.

Um lado carrega regra. O outro carrega comodidade. **É o segundo que fecha o ciclo.**

---

## O que a literatura diz sobre a bidirecionalidade

### Eric Evans — impor uma direção de travessia

Em *Domain-Driven Design* (2003), Evans lista três formas de tornar associações tratáveis, e a
primeira é **impor uma direção de travessia**. A frase que decide este caso:

> *"A bidirectional association means that both objects can be understood only together. When
> application requirements do not call for traversal on both directions, adding a traversal
> direction reduces interdependence and simplifies the design."*

O teste é uma pergunta: **os requisitos pedem travessia nas duas direções?**

Aqui, não. `Pessoa → Emprestimo` é requisito (o limite de três). `Emprestimo → Pessoa` é
conveniência de apresentação. Pelo critério de Evans, a direção deve ser imposta.

### Martin Fowler — o refactoring tem nome

*Change Bidirectional Association to Unidirectional* é um refactoring catalogado. As
justificativas registradas:

- associação bidirecional é **mais difícil de manter** — exige código extra para criar e apagar
  corretamente os objetos, o que torna o programa mais complicado;
- pode causar **problemas de garbage collection**, com objetos não usados ocupando memória;
- há **interdependência entre as classes**: as duas passam a se conhecer, e **não podem ser
  usadas separadamente**.

E a orientação que decide o desenho:

> Se uma classe precisa da associação reversa, **calcule-a**. Só a mantenha se o cálculo for
> complexo.

No nosso caso o cálculo é trivial — o empréstimo guarda o `PessoaId`, e quem precisa do nome
consulta o cadastro.

### Robert C. Martin — onde o ADP se aplica (e onde não)

É comum ouvir que dependência circular "viola o Acyclic Dependencies Principle". **Cuidado com
essa citação:** o ADP, de Robert C. Martin, é explicitamente um princípio de **pacotes e
componentes** — um dos princípios de acoplamento de pacote, distinto dos SOLID, que tratam de
classes individuais. Ele afirma que o grafo de dependências **de pacotes ou componentes** não
deve ter ciclos.

Um ciclo entre duas classes do mesmo agregado **não viola o ADP**. O que ele viola é a orientação
de Evans e de Fowler, que é mais branda: *simplifique quando não precisar dos dois lados*.

Dito isso, a literatura sobre ADP registra o motivo pelo qual ciclos incomodam — e ele vale, em
menor escala, também entre classes: ciclos criam interações complexas, tornando **difícil prever
o impacto de uma mudança**, e dificultam **testar em isolamento**, porque não se consegue separar
uma parte das outras do ciclo.

> **Precisão importa em aula:** dizer "isto viola o ADP" é errado. Dizer "isto contraria a
> orientação de Evans sobre direção de travessia, e Fowler tem um refactoring com nome para
> desfazer" é correto e verificável.

---

## O preço que este ciclo cobrou — concreto, neste repositório

Não é dano hipotético. Três consequências mensuráveis:

### 1. Um erro 500 que só aparece depois

Serializar `Pessoa` em JSON percorre `Emprestimos` → `Emprestimo` → `Pessoa` → `Emprestimos`…
O `System.Text.Json` detecta e lança:

```
A possible object cycle was detected.
```

**O detalhe cruel:** com uma pessoa **sem** empréstimo, funciona. Passa no teste manual, sobe,
responde 200. Quebra no dia em que alguém pega o primeiro livro emprestado.

### 2. Um arquivo inteiro que existe só por causa disso

`Biblioteca.Api/Resposta.cs` — `PessoaResposta` e `EmprestimoResposta` — nasceu para cortar o
ciclo. **Compare:** os itens do acervo não têm ciclo, e não têm projeção nenhuma; saem
serializados direto.

Esse é o custo em linha reta: uma referência que nenhuma regra usava obrigou a existência de um
arquivo, dois records e dois métodos de fábrica.

### 3. Um endpoint que não foi escrito

`GET /emprestimos` ficou de fora da API. Não por decisão — a rota simplesmente não foi
considerada. Mas o ciclo tornaria sua implementação mais desconfortável: qualquer listagem de
empréstimos arrasta o grafo inteiro se ninguém projetar.

---

## Onde o problema foi resolvido: na fronteira, não no domínio

Esta parte é a mais instrutiva, porque mostra um **erro de tratamento** que quase foi cometido.

Quando o 500 apareceu, havia três saídas. Duas delas tratam o sintoma no lugar errado:

| saída | onde age | por que foi recusada |
|---|---|---|
| `ReferenceHandler.IgnoreCycles` | configuração global do JSON | uma linha resolve, e a resposta passa a sair com `"pessoa": null` enterrado dentro de cada empréstimo — um campo que existe, é sempre nulo, e o cliente não tem como saber por quê. E vale para a API inteira, inclusive onde ninguém pediu. |
| `[JsonIgnore]` em `Emprestimo.Pessoa` | **dentro do domínio** | poria um atributo de serialização em `Biblioteca.Dominio` — o projeto que não pode saber que JSON existe. Resolveria o sintoma criando um acoplamento novo, do domínio para o formato de transporte. |
| **projeção** (`PessoaResposta`) ✅ | na API | o que sai na resposta vira **escolha explícita** da fronteira, não resíduo do formato interno dos objetos. |

A projeção foi a escolha certa **dado que o ciclo existia**. Mas repare no que ela é: uma defesa
construída na fronteira contra um problema criado no núcleo.

> **A lição de fronteira:** quando um problema do domínio vaza para a serialização, há sempre a
> tentação de calar o sintoma onde ele apareceu — uma configuração no serializador, um atributo
> na classe. Isso funciona e deixa a causa intacta. A pergunta a fazer primeiro é sempre: **por
> que este objeto tem essa referência?**

---

## A correção

Trocar a referência por identificador:

```csharp
// antes — o ciclo
public class Emprestimo
{
    public Pessoa Pessoa { get; }
}

// depois — a direção imposta
public class Emprestimo
{
    public int PessoaId { get; }
}
```

O que muda, e o que não muda:

| | antes | depois |
|---|---|---|
| `Pessoa` é raiz do agregado | sim | **sim** — inalterado |
| `Pessoa.Emprestar` aplica as regras | sim | **sim** — inalterado |
| `Emprestimo` sabe de quem é | sim, por referência | sim, por Id |
| Ciclo de objetos | **sim** | não |
| Serializar `Emprestimo` estoura | sim | não |
| Quem precisa do nome da pessoa | lê direto | consulta o cadastro |

**Observe a direção do acoplamento:** depois da mudança, `Emprestimo` depende **menos** de
`Pessoa`, não mais. Ele guarda um `int` — não conhece mais a classe. `Biblioteca.Dominio`
continua sem depender de nada externo, e `Emprestimo` deixa de depender de `Pessoa` em nível de
tipo.

O custo é real e vale nomear: **a resposta da API passa a precisar de duas fontes** — o
empréstimo dá os Ids, o cadastro dá o nome, o acervo dá o título. `EmprestimoResposta` continua
existindo, agora compondo dados de lugares diferentes. Isso não é defeito: é o trabalho normal de
uma camada de fronteira, e é o que ela existe para fazer.

Na prática, o método de fábrica mudou de assinatura:

```csharp
EmprestimoResposta.De(emprestimo)           // antes: o empréstimo sabia tudo
EmprestimoResposta.De(emprestimo, pessoa)   // depois: a fronteira compõe
```

E o `GET /emprestimos`, escrito depois, precisa carregar a pessoa junto ao achatar a lista:

```csharp
cadastro.Pessoas.SelectMany(pessoa =>
    pessoa.Emprestimos.Select(emprestimo => new { emprestimo, pessoa }))
```

**Este é o custo visível da direção imposta**, e ele é preferível ao anterior: antes o empréstimo
sabia o nome sozinho, ao preço de um ciclo no domínio, um erro 500 latente e um arquivo inteiro
de projeções construído para contorná-lo.

---

## Perguntas para a turma

1. `Pessoa` instancia `Emprestimo`. Isso é acoplamento? **É errado?** (Sim para a primeira; não
   para a segunda — e a justificativa é o que interessa.)
2. Qual regra de negócio deixaria de existir se apagássemos `Pessoa._emprestimos`? E se
   apagássemos `Emprestimo.Pessoa`? (limite de três × nenhuma)
3. Por que o erro 500 do ciclo **não apareceu** nos primeiros testes da API?
4. `[JsonIgnore]` em `Emprestimo.Pessoa` resolveria o 500 em uma linha. Por que foi recusado?
5. Depois de trocar `Pessoa` por `PessoaId`, o `Emprestimo` ficou mais ou menos acoplado?
6. Se `Emprestimo.Pessoa` fosse usado por uma regra — digamos, "não devolver item de outra
   pessoa" — a análise mudaria? (Sim: aí o acoplamento pagaria uma regra.)

---

## Resumo em quatro frases

1. **Acoplamento não é erro; acoplamento sem regra é.** `Pessoa → Emprestimo` sustenta o limite
   de três. `Emprestimo → Pessoa` não sustentava nada.
2. **Instanciar dentro de outra classe pode ser o desenho certo** — *Factory Method on Aggregate
   Root* (Vernon) e *Information Expert* (Larman/GRASP) defendem exatamente isso.
3. **Bidirecionalidade se questiona pelo requisito**: se a travessia não é exigida nos dois
   sentidos, imponha a direção (Evans) — e existe refactoring catalogado para desfazê-la (Fowler).
4. **Problema do núcleo não se resolve na fronteira.** A projeção foi correta como defesa, mas a
   causa estava em uma referência que ninguém usava.

---

## Fontes

- **Craig Larman**, *Applying UML and Patterns* (1997) — GRASP, Information Expert.
  [GRASP (object-oriented design) — Wikipedia](https://en.wikipedia.org/wiki/GRASP_(object-oriented_design)) ·
  [GRASP explained — Kamil Grzybek](https://www.kamilgrzybek.com/blog/posts/grasp-explained)
- **Eric Evans**, *Domain-Driven Design* (2003) — agregados, direção de travessia.
  [DDD cap. 5, notas — herbertograca.com](https://herbertograca.com/2015/09/29/domain-driven-design-by-eric-evans-chap-5-a-model-expressed-in-software/) ·
  [Associations in DDD — stochastyk](http://stochastyk.blogspot.com/2008/06/associations-in-domain-driven-design.html)
- **Vaughn Vernon**, *Implementing Domain-Driven Design* (2013), cap. 11 "Factories" — Factory
  Method on Aggregate Root.
  [Índice do cap. 11 — O'Reilly](https://www.oreilly.com/library/view/implementing-domain-driven-design/9780133039900/ch11lev1sec2.html) ·
  [Implementing DDD: Aggregates — InformIT](https://www.informit.com/articles/article.aspx?p=2020371) ·
  [Aggregate Design Rules — archi-lab.io](https://www.archi-lab.io/infopages/ddd/aggregate-design-rules-vernon.html)
- **Martin Fowler**, *Refactoring* — Change Bidirectional Association to Unidirectional.
  [refactoring.com — catálogo](https://www.refactoring.com/catalog/changeBidirectionalAssociationToUnidirectional.html)
- **Robert C. Martin** — Acyclic Dependencies Principle (pacotes/componentes).
  [Acyclic dependencies principle — Wikipedia](https://en.wikipedia.org/wiki/Acyclic_dependencies_principle)

### ⚠ Ressalva sobre as fontes — leia antes de citar em aula

As citações de **Evans** e **Vernon** foram obtidas de **fontes secundárias** (resenhas, notas de
leitura, páginas de índice). O texto integral dos dois livros está atrás de paywall e **não foi
lido diretamente**.

- A frase de Evans sobre associação bidirecional aparece **consistente em múltiplas fontes
  independentes** e é amplamente citada.
- A existência da seção **"Factory Method on Aggregate Root"** no capítulo 11 de Vernon está
  confirmada como título de seção, e o exemplo `planBacklogItem()` aparece em várias fontes —
  mas **o parágrafo original de justificativa não foi lido**.
- A atribuição do **Information Expert** a Larman/GRASP e a formulação do princípio estão
  confirmadas em fontes independentes.
- O escopo do **ADP** (pacotes, não classes) está confirmado.

**Se este material for para aula, confira as passagens de Evans e Vernon nos livros físicos.**

### Correções de atribuição que circularam durante a construção

Registradas porque são erros fáceis de repetir:

- **Eric Evans não criou a POO.** POO vem de Alan Kay (Smalltalk, anos 1970) e de Simula (Dahl e
  Nygaard, anos 1960). Evans é o autor do *Domain-Driven Design* (2003).
- **Information Expert não é de Evans.** É GRASP, de Craig Larman (1997), anterior ao DDD.
