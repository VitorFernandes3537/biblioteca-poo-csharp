# Como chegamos aqui — o percurso, as decisões e os erros

Registro cronológico da construção. O `DOC-1-o-que-existe.md` descreve o **resultado**;
este descreve o **caminho** — inclusive os tropeços, que são a parte que não aparece no
código pronto e é justamente a que a turma vai viver.

Cobre da reestruturação em solução até a API completa: commits `f5f1303` a `978e9df`,
todos na branch `reestruturacao`, em 18–19/08/2026.

---

## O ponto de partida

Antes de tudo isto, o repositório era **um projeto de console só**:

```
Biblioteca/
├── Biblioteca.csproj      (OutputType = Exe)
├── Program.cs             (5 cenas de demonstração)
└── Dominio/
    ├── ItemAcervo.cs  Livro.cs  Revista.cs  Dvd.cs
    ├── Pessoa.cs  Emprestimo.cs  ExcecaoDominio.cs
```

O domínio já estava construído e funcionando: herança, classe abstrata, limite de três
empréstimos, idade mínima, multa que congela na devolução. Três commits anteriores
(`57eb8f6`, `7a12cf5`, `d58cdd5`, `242bd5c`) tinham chegado até aí.

**Duas coisas do domínio mandaram no desenho de tudo que veio depois:**

1. **`Pessoa` é a raiz do agregado.** `Pessoa.Emprestar(item)` é o único caminho que aplica
   idade mínima, limite de três em aberto, e registra na lista dela. `new Emprestimo(...)`
   compila e pula tudo isso.
2. **Nada tinha `Id`.** Identidade era por referência de objeto, o que bastava para variáveis
   locais num console. HTTP precisa de endereço.

---

## FASE 1 — da classe ao projeto

Objetivo: transformar um projeto de console em uma solução com três projetos, sem quebrar
nada e sem perder o histórico do git.

### Etapa 1 — reorganizar as pastas

O `.csproj` estava na raiz, junto do `.git`. Se um projeto novo nascesse ali, viraria filho do
projeto atual em vez de irmão.

⚠ **O `.git` fica na raiz e não se move.** Se ele descesse junto com o domínio, a API nasceria
fora do controle de versão **sem erro nenhum** — nada avisaria.

```powershell
New-Item -ItemType Directory -Name Biblioteca.Dominio
Move-Item Biblioteca.csproj Biblioteca.Dominio/Biblioteca.Dominio.csproj
Move-Item Dominio/*.cs Biblioteca.Dominio/
Move-Item Program.cs Biblioteca.Dominio/
Remove-Item Dominio, bin, obj -Recurse -Force
```

**O `namespace` não mudou.** Os arquivos saíram de `Dominio/` e foram para
`Biblioteca.Dominio/`, e continuam com `namespace Biblioteca.Dominio;` — em C# o namespace é o
que está escrito no arquivo, não a pasta onde ele mora.

> **Tropeço:** o `obj/` foi movido junto em vez de apagado, e o `.csproj` ficou com o nome
> antigo. Ao apagar o `obj/`, o C# Dev Kit do VS Code o recriou instantaneamente — não era
> problema: era um restore automático, e os arquivos novos já tinham o nome certo
> (`Biblioteca.Dominio.*`). O `.gitignore` já o ignorava.

### Etapas 2 e 3 — a solução, e o domínio deixa de ser executável

```powershell
dotnet new sln     # gera Biblioteca.slnx — .slnx é o padrão no .NET 10, não .sln
```

E o `<OutputType>Exe</OutputType>` saiu do `.csproj` do domínio. Sem ele, o SDK assume
`Library`: gera `.dll`, não `.exe`.

### Etapa 4 — o erro planejado

`dotnet build Biblioteca.Dominio` → **`error CS8805: O programa que usa as instruções de nível
superior precisa ser um executável.`**

**Este erro era o objetivo da etapa.** `Program.cs` usa *top-level statements* — código solto,
sem `class` nem `Main`. O compilador transforma isso num ponto de entrada, e ponto de entrada só
existe em projeto executável. O erro não se conserta no domínio: ele diz que o `Program.cs`
precisa mudar de casa.

```powershell
dotnet new console -n Biblioteca.Laboratorio
Move-Item Biblioteca.Dominio\Program.cs Biblioteca.Laboratorio\Program.cs -Force
```

O `-Force` é necessário porque `dotnet new console` já cria um `Program.cs` com "Hello, World!".

### Etapas 6, 7 e 8 — a API e as amarrações

```powershell
dotnet new web -n Biblioteca.Api    # 'web' = minimal API. 'webapi' traria controllers e Swagger

dotnet sln add Biblioteca.Dominio Biblioteca.Laboratorio Biblioteca.Api

dotnet add Biblioteca.Laboratorio reference Biblioteca.Dominio
dotnet add Biblioteca.Api reference Biblioteca.Dominio
```

**Duas ligações diferentes, e confundi-las é comum:**
- `dotnet sln add` só diz "este projeto faz parte desta solução". É organização.
- `dotnet add reference` é a dependência real: quem enxerga as classes de quem.

**A direção é a decisão:** API e Laboratório apontam para o domínio. Nunca o contrário. Se um
dia o domínio precisar referenciar a API, a regra foi quebrada em algum lugar antes.

### Verificação

`dotnet run --project Biblioteca.Laboratorio` produziu **exatamente a mesma saída** de antes da
reestruturação. Era o único critério que importava: o domínio atravessou a mudança de casa
intacto.

Commit: `refactor: reestruturacao em solucao com tres projetos, dominio como biblioteca, laboratorio e api`

---

## FASE 2 — a API

Cada etapa carregou **uma decisão**, e as alternativas recusadas estão registradas porque elas
são o conteúdo — o código final não mostra o que não foi escolhido.

### Etapa 0 — identidade

**O problema:** sem `Id`, não há URL para item nenhum. Cada requisição HTTP chega sem memória da
anterior; `GET /itens/3` precisa achar um item que ninguém está segurando numa variável.

**As duas saídas:**

| | onde o Id nasce | vantagem | custo |
|---|---|---|---|
| **A** ✅ | no domínio, contador estático | item já nasce identificado, igual no console e na API | o domínio carrega um conceito que só existe por causa de HTTP |
| B | no armazenamento (`Dictionary<int, ItemAcervo>`) | domínio intocado | todo endpoint precisa compor a resposta com o Id por fora |

**Escolhido: A.** Não pela pureza — por ser material de aula: em B, o primeiro `GET /itens` já
exigiria explicar projeção de resposta, e é cedo para dois assuntos ao mesmo tempo.

Detalhe que virou regra: o `Id = _proximoId++` fica **depois** da validação do título. Tentativa
recusada não queima Id. (Verificado na prática: um POST que falhou não pulou o número seguinte.)

Escolha secundária: `int` e não `Guid`. `Guid` nunca colide e sobrevive a reinício, mas
`/itens/3` se digita na aula e `/itens/8f14e45f-...` não.

### Etapa 1 — onde o acervo mora

**O problema:** `POST /itens` cria um item e retorna; `GET /itens` roda depois e não vê nada.
Precisa haver uma coleção que sobreviva entre requisições.

**As duas saídas:** `List<ItemAcervo>` (busca linear, estrutura já conhecida) ou
`Dictionary<int, ItemAcervo>` (busca por chave, garante Id único, mas grava o Id em dois lugares
— dentro do objeto e como chave — sem nada obrigando que sejam iguais).

**Escolhido: `List`.** Na etapa 9 (não implementada) a coleção viraria detalhe interno de um
repositório, e a estrutura mudaria numa classe só.

A instância nasce **fora de qualquer endpoint**:

```csharp
var acervo = new Acervo();
```

> **A armadilha ensinada aqui:** `new Acervo()` dentro de um endpoint faria cada requisição
> criar um acervo vazio. O POST anterior teria sumido. É o bug do "criei o item e o GET não
> acha", e o compilador não avisa.

Verificação: recarregar `/acervo-teste` várias vezes e conferir que **os Ids não mudam**.

### Etapa 2 — GET /itens e GET /itens/{id:int}

**Duas decisões, ambas já fechadas antes:**

- **Lista vazia é 200 com `[]`, não 404.** A coleção existe, só está vazia. 404 significaria
  "esta URL não é recurso nenhum", o que é falso. Já `/itens/99` inexistente é 404 legítimo.
- **`{id:int}` com restrição de rota.** Sem ela, `/itens/abc` casaria com a rota, o bind
  falharia, e sairia um 400 com o corpo de erro do framework — inconsistente com o resto da API,
  onde erro é sempre `{ "erro": "..." }`. Com ela, o roteador devolve 404 antes do código rodar.

**O que a saída revelou:**

```
/itens     → {"id":1,"titulo":...,"prazoDevolucao":14,...}
/itens/2   → {"prazoDevolucao":7,"multaDiaAtrasado":2,"id":2,...}
```

A **ordem das chaves muda** entre os dois. Não é bug — em JSON a ordem não tem significado. A
causa é o tipo estático que o serializador enxerga: em `/itens` ele recebe
`IReadOnlyList<ItemAcervo>` e serializa pela base; em `/itens/2`, o `Results.Ok(item)` passa o
objeto e ele serializa por `Revista`, começando pelos overrides.

E o problema real que ficou visível: **os três tipos são indistinguíveis no JSON**. Um cliente
não tem como saber que o Id 1 é `Livro` e o 3 é `Dvd` — só dá para deduzir pelo `prazoDevolucao`.
Isso voltou como problema concreto na etapa 4.

### Etapa 3 — o middleware

**O problema:** a etapa 4 ia chamar construtores do domínio, e eles lançam. Sem tratamento, o
Kestrel devolve **500** com o stack trace inteiro. Título vazio não é a API quebrando — é o
domínio recusando um pedido inválido.

**As duas saídas:** `try/catch` em cada endpoint (explícito, mas repetido em todos, e esquecer um
vaza 500 sem o compilador avisar) ou **middleware** (um `try/catch` só, cobre inclusive os
endpoints que ainda não existem).

**Escolhido: middleware.**

**O 409, e não 400:** 400 diz "você escreveu errado". "Item já emprestado" é o oposto — o pedido
está bem formado e foi entendido; o **estado** é que não permite. Isso é 409 Conflict.

**O `catch` é de `ExcecaoDominio` e só dela.** Um `catch (Exception)` genérico transformaria
`NullReferenceException` em 409 também, e bug da API deve continuar dando 500, alto e feio.

> **O tropeço que valeu a etapa inteira.** O código foi colado com o `app.Use(...)` **depois**
> dos `MapGet`, e o `MapGet` de teste **depois do `app.Run()`**. Resultado: 404 de corpo vazio.
>
> **Por quê:** `app.Run()` bloqueia — fica segurando o processo enquanto o servidor atende. A
> linha abaixo dele só executaria quando o servidor desligasse, ou seja, nunca. A rota jamais foi
> registrada.
>
> E o middleware abaixo dos `Map` não capturaria nada de qualquer forma: ele só enxerga o que
> roda dentro do seu `await next()`, e `next()` é tudo que foi registrado **depois** dele.
>
> **A ordem correta, sempre:** `Build()` → `Use` → todos os `Map` → `Run()`.
>
> **Os dois sintomas se distinguem pelo status:** `MapGet` abaixo do `Run` → 404 de corpo vazio
> (a rota não existe). `Use` abaixo dos `Map` → 500 com página de erro (o catch nunca rodou).

### Etapa 4 — POST /itens

**O problema que a etapa 2 deixou:** os três tipos têm construtores diferentes. `Livro` e
`Revista` recebem `(titulo, autor)`; `Dvd` recebe `(titulo, autor, idadeMinima)`. E o corpo não
pode ser um `ItemAcervo` — classe abstrata não se instancia, e o serializador não sabe escolher
entre as filhas.

**As duas saídas:**

| | como o cliente diz o tipo | vantagem | custo |
|---|---|---|---|
| A | campo `tipo` no corpo, uma rota só | uma rota | `idadeMinima` no corpo de todos, ignorado em dois; `tipo: "revistta"` só falha em runtime, num `switch` |
| **B** ✅ | uma rota por tipo | cada corpo tem só os campos daquele tipo; tipo errado vira 404 do roteador | três rotas parecidas |

**Escolhido: B.** Elimina o `switch` de string — erro de digitação vira 404 antes de qualquer
código rodar.

**Os records de entrada.** Primeiro objeto da API que existe só por causa de HTTP. Sem
`required` e **sem validação**, de propósito: título vazio vira erro quando o construtor de
`ItemAcervo` recusar. Validar nos dois lugares daria duas mensagens diferentes para a mesma
regra.

**O `Location` do 201** aponta para `/itens/{id}`, não `/itens/livros/{id}` — Location aponta
para onde o recurso **está**, não onde nasceu.

**Verificado na prática:** o POST com título vazio devolveu 409 do middleware, **sem uma linha de
try/catch no endpoint**. Foi o que a etapa 3 comprou. E o Id do item seguinte não pulou número —
a validação vem antes do `_proximoId++`.

### Etapa 5 — PUT /itens/{id}

**O problema:** `Titulo` e `Autor` são `{ get; private set; }`. Nenhum código fora de
`ItemAcervo` atribui a eles. **O endpoint não tem como escrever.**

**O reflexo errado** seria abrir o `set` para `public`. Isso desfaz a garantia que a classe dá:
`PermiteIdade`, `MarcarComoEmprestado` e `Emprestar` valem porque o estado não pode ser mexido
por fora. Com `set` público, qualquer linha zeraria um título e o construtor que valida viraria
decoração.

**O caminho:** o domínio ganha `AlterarDados(titulo, autor)` — uma porta declarada, com a regra
dentro. E a decisão está no que o método **não** aceita:

| campo | muda? | por quê |
|---|---|---|
| `Titulo`, `Autor` | sim | descrição do exemplar; corrigir digitação é legítimo |
| `Id` | nunca | é a identidade — trocar significa alterar outro item |
| `Disponibilidade` | nunca por aqui | só empréstimo e devolução mexem nela |
| `IdadeMinima` do DVD | fora do escopo | `Dvd` lê o parâmetro do construtor primário; não há campo |

> **Por que `Disponibilidade` não entra:** um `PUT` marcando `disponibilidade: true` num item
> emprestado apagaria o empréstimo sem devolvê-lo. O item volta ao acervo e o `Emprestimo`
> continua em aberto, apontando para ele. Estado corrompido em silêncio.

A validação de título ficou **dentro de `AlterarDados`**, não num `if` no endpoint: assim POST e
PUT recusam a mesma coisa com a mesma mensagem e o mesmo 409.

> **Tropeço:** o bloco não foi colado — nem o `AlterarDados` no domínio, nem o `MapPut`. O
> sintoma foi **405 Method Not Allowed com `Allow: GET`**: a rota existia (o roteador a
> encontrou), mas só com `GET` registrado. 405 com `Allow` listando os verbos que existem é o
> sinal exato de "esta rota existe, este verbo não".

### Etapa 6 — DELETE /itens/{id}

**A pergunta:** e se o item estiver emprestado?

Apagar um item emprestado deixa o sistema mentindo: o `Emprestimo` na lista da `Pessoa` continua
em aberto, apontando para um item que não está mais no acervo. Ele segue contando para o limite
de três, e a devolução ainda funciona — devolvendo um item que não existe mais. **Nada disso
lança exceção; simplesmente fica errado.**

**As três saídas:** recusar com 409 ✅ · apagar mesmo assim (empréstimo órfão) · apagar forçando a
devolução junto (um DELETE que faz duas coisas, e devolução tem semântica própria — data, multa —
que ninguém pediu).

**Onde a regra vive:** no `Acervo`, na API — **não** no domínio. "Não remover item emprestado"
precisa de duas coisas ao mesmo tempo: o estado do item e a coleção onde ele está. `ItemAcervo`
conhece o próprio estado e não sabe que existe um acervo.

Mas lança `ExcecaoDominio` mesmo estando fora do domínio: a recusa é da mesma natureza, e um tipo
de exceção novo obrigaria um segundo `catch` para produzir exatamente a mesma resposta.

**Esta foi a primeira regra do sistema que não nasceu no domínio** — e vale marcar isso.

### Etapa 7 — GET/POST /pessoas

**O problema que só aparece ao serializar:** `Pessoa` tem `Emprestimos`; cada `Emprestimo` tem
`Pessoa`, que tem `Emprestimos`… O `System.Text.Json` percorre e lança
`A possible object cycle was detected` → **500**.

**Antes:** `Pessoa` precisou ganhar `Id`, nos mesmos moldes de `ItemAcervo` — consequência da
etapa 0, não decisão nova. Contador **próprio**, separado: pessoa 1 e item 1 coexistem sem
conflito, porque o que identifica o recurso é a rota, não o número solto.

**As três saídas para o ciclo:**

| | como | vantagem | custo |
|---|---|---|---|
| A | `ReferenceHandler.IgnoreCycles` | uma linha de config | `"pessoa": null` no meio de cada empréstimo — campo que existe, é sempre nulo, e o cliente não sabe por quê. Vale para a API inteira |
| **B** ✅ | projeção (`PessoaResposta`) | resposta desenhada, não resíduo do formato interno; campos calculados entram naturalmente | um tipo a mais |
| C | `[JsonIgnore]` em `Emprestimo.Pessoa` | corta na origem | põe atributo de serialização **dentro do domínio** — fora de questão pela direção das dependências |

**Escolhido: B.** C está fora de questão pela arquitetura. A resolve rápido e ensina errado: o
`null` no meio da resposta é um efeito colateral que o aluno não consegue explicar.

**O que a saída revelou:** as datas saem em **formatos diferentes** —
`1996-08-19T00:00:00-03:00` (com fuso) e `2011-03-14T00:00:00` (sem). A causa é o
`DateTime.Kind`: `DateTime.Today` produz `Local` e o serializador escreve o offset; a data
desserializada do JSON vem como `Unspecified` e não há offset a escrever. Mesma propriedade,
dois formatos, dependendo de como o valor nasceu.

> **Tropeço (não foi de código):** `dotnet build` falhou com **`MSB3027` / `MSB3021`** — não
> conseguiu sobrescrever a `.dll` porque a API do teste anterior continuava rodando e segurando
> o arquivo. A mensagem nomeia o culpado: `bloqueado por: "Biblioteca.Api (20056)"`.
> **Regra: pare a API antes de compilar.**

### Etapa 8 — POST /emprestimos e /devolucoes

**A etapa que o desenho inteiro estava protegendo.**

```csharp
var emprestimo = pessoa.Emprestar(item);   // ✅ aplica idade, limite e registra na lista
// var emprestimo = new Emprestimo(pessoa, item);   // ❌ compila e pula tudo
```

O construtor marca o item como emprestado e **não** checa idade, **não** checa o limite de três,
e o empréstimo **não** entra na lista da pessoa — `QtdEmprestimosEmAberto` continua no valor
antigo e o limite deixa de existir. Nada disso lança. A única defesa é não chamar o construtor.

**A assimetria da devolução:** emprestar é da `Pessoa`; devolver é do `Emprestimo`. Então a API
precisa **achar** o empréstimo — o par (pessoa, item) em aberto:

```csharp
var emprestimo = pessoa.Emprestimos.FirstOrDefault(
    e => e.Item.Id == requisicao.ItemId && e.EstaEmAberto);
```

O `EstaEmAberto` no filtro não é detalhe: sem ele, um item emprestado, devolvido e emprestado de
novo casaria com o registro antigo, já fechado, e a devolução nova estouraria "já foi devolvido"
apontando para o empréstimo errado.

**404 e não 409 quando não há empréstimo em aberto:** o recurso que a requisição aponta não
existe. 409 seria o caso em que ele existe e o domínio recusa a operação.

**201 sem `Location`** no empréstimo: não existe `GET /emprestimos/{id}` — empréstimo não tem Id
próprio. **200 na devolução**, não 201: não criou recurso, alterou um existente — e o corpo
importa, é nele que sai a multa.

> **Tropeço:** ao remover o andaime da etapa 6, os três `acervo.Adicionar` de partida foram
> apagados junto. Sintoma: `Item 1 não encontrado` em todos os empréstimos. **A saída foi não
> devolvê-los** — os dados de partida sempre foram andaime para haver o que consultar antes do
> POST existir; com todos os endpoints prontos, o teste fica mais honesto criando tudo por HTTP.

**O que a saída provou:** `409 — "Marina tem 15 anos e o item "Cidade de Deus" é para 16 anos ou
mais."` A regra nasceu em `Pessoa.Emprestar`, atravessou o middleware e virou resposta HTTP —
**sem uma linha de validação na API**.

---

## Onde a construção parou, e por quê

Três etapas estavam planejadas e **não foram feitas**:

| # | etapa | o que traria |
|---|---|---|
| 9 | `IRepositorioAcervo` + DI `Singleton` | `Acervo` vira interface; some a variável do `Program.cs` |
| 10 | serviço, se houvesse o que extrair | candidato: achar o empréstimo em aberto |
| 11 | segunda implementação do repositório | trocar mexendo em uma linha |

**A razão de parar:** nenhuma delas acrescenta comportamento. A API responde exatamente o mesmo
depois delas. São sobre **inverter dependência**, e esse assunto só faz sentido quando existe uma
segunda implementação concreta para trocar. Sem essa necessidade, viram cerimônia decorada.

A decisão foi consolidar o entendimento do que existe em vez de acrescentar abstração que não
seria absorvida.

---

## Os onze commits

```
978e9df feat: emprestimo e devolucao via api, sempre pelo agregado Pessoa, com projecao do emprestimo
d68ffa9 feat: cadastro de pessoas com id, endpoints de leitura e criacao, projecao que corta o ciclo do json
f5043ba feat: remocao de itens do acervo com recusa em 409 quando o item esta emprestado
ae6b4d8 feat: alteracao de itens via put com metodo AlterarDados no dominio e validacao de titulo
14029aa feat: criacao de itens por tipo com rotas separadas, resposta 201 e cabecalho location
692d1b9 feat: middleware que traduz ExcecaoDominio em resposta 409 com mensagem do dominio
724fa8f feat: endpoints de leitura do acervo, lista de itens e busca por id com 404
a8c4948 feat: acervo em memoria na api, coleção de itens que sobrevive entre requisicoes
4779df7 feat: identidade dos itens do acervo com Id incremental herdado de ItemAcervo
f5f1303 refactor: reestruturacao em solucao com tres projetos, dominio como biblioteca, laboratorio e api
242bd5c feat: incremento 3 - Pessoa, limite de emprestimos, idade minima, devolucao com data
```

**Cadência:** um commit por etapa que compila e roda. Nunca duas etapas num commit.

---

## Catálogo de tropeços — para reconhecer o sintoma

Todos aconteceram de verdade durante esta construção.

| sintoma | causa | como reconhecer |
|---|---|---|
| `CS8805: instruções de nível superior precisa ser um executável` | `Program.cs` com top-level statements num projeto biblioteca | esperado ao converter o domínio em `Library` |
| `404 Not Found` com **corpo vazio** | a rota não existe — `MapGet` abaixo do `app.Run()`, ou URL não casou | corpo vazio = roteador; corpo com `{"erro":...}` = seu código |
| `405 Method Not Allowed` + `Allow: GET` | a rota existe, o verbo não foi registrado | o `Allow` lista os verbos que existem |
| `500` + página de erro em vez de 409 | `app.Use` registrado **depois** dos `Map` | o middleware só cobre o que vem depois dele |
| `MSB3027` / `MSB3021` ao compilar | a API está rodando e segurando a `.dll` | a mensagem nomeia o processo |
| `A possible object cycle was detected` | serializou `Pessoa` ou `Emprestimo` direto | só aparece quando há empréstimo — até lá parece funcionar |
| `415 Unsupported Media Type` | faltou `-H "Content-Type: application/json"` | — |
| JSON chega malformado no POST | faltou `--%` no `curl.exe` do PowerShell | — |
| `Item N não encontrado` depois de reiniciar | acervo em memória: reiniciou, zerou | os Ids voltam a contar do 1 |

---

## As quatro decisões que mais mandaram no resultado

1. **`Id` no domínio, e não no armazenamento** (etapa 0) — permitiu que o JSON saísse direto e
   adiou a conversa sobre projeção até ela ter motivo concreto.
2. **Middleware, e não try/catch por endpoint** (etapa 3) — todo endpoint escrito depois já
   nasceu coberto. Foi ela que permitiu que os POSTs não tivessem nenhum tratamento de erro.
3. **`AlterarDados`, e não `set` público** (etapa 5) — a única alteração possível é a que o
   domínio declarou. `Id` e `Disponibilidade` continuam inalcançáveis de fora.
4. **Projeção, e não `IgnoreCycles`** (etapa 7) — o que sai na resposta virou escolha explícita
   da API, não resíduo do formato interno dos objetos.
