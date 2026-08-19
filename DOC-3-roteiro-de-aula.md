# Roteiro de aula — do domínio à minimal API

Material bruto para montar a condução. **Não é um plano de aula fechado**: não sei quanto tempo
você tem, o nível da turma, nem o que já foi ensinado antes. O que está aqui é a ordem de
apresentação, o que mostrar em cada momento, onde parar para eles tentarem, e quais erros
provocar de propósito.

Pressupõe que o domínio (`Biblioteca.Dominio`) já foi construído em aulas anteriores — herança,
classe abstrata, `private set`, exceção própria. Se não foi, este roteiro começa tarde demais.

Referências: `DOC-1-o-que-existe.md` (o que está pronto) · `DOC-2-como-chegamos-aqui.md` (as
decisões e os tropeços reais).

---

## A ideia que sustenta a aula inteira

> **O domínio recusa. A API traduz a recusa.**

Se a turma sair só com isso, a aula funcionou. Todo o resto — status codes, records, projeções,
middleware — são consequências dessa frase.

O contraexemplo que torna a frase concreta: `new Emprestimo(pessoa, item)` compila, roda, e
destrói a regra de negócio **sem erro nenhum**. É a demonstração mais valiosa do material.

---

## Blocos, e o que cada um custa

Cinco blocos. Cada um chega a um estado que compila e roda — dá para parar em qualquer um deles
e retomar depois.

| bloco | assunto | o que a turma sai fazendo |
|---|---|---|
| **1** | da classe ao projeto | monta a solução de três projetos e entende a direção da referência |
| **2** | primeiro endpoint | serve dados por HTTP e distingue 404 de lista vazia |
| **3** | erro vira resposta | middleware, 409, e por que a ordem do pipeline importa |
| **4** | escrita | POST e o problema dos construtores diferentes |
| **5** | o agregado | empréstimo/devolução, e a armadilha do construtor |

Blocos 1 e 2 são os mais mecânicos. O 3 é o mais conceitual. O 5 é o que dá sentido a todos.

---

# BLOCO 1 — da classe ao projeto

**Objetivo:** entender por que um projeto vira três, e qual aponta para qual.

### Abertura — a pergunta antes do comando

Mostre o repositório como estava: um `.csproj`, um `Program.cs`, uma pasta `Dominio/`. Funciona.
Roda. Então pergunte:

> "Se eu quiser que uma página web use estas classes, e o console continue usando também, o que
> muda?"

Deixe eles responderem antes de mostrar. A resposta que interessa: **o domínio precisa ser algo
que outros usam, e não algo que roda.**

### O desenho, no quadro, antes de qualquer comando

```
Laboratorio  ──┐
               ├──>  Dominio
Api          ──┘
```

E a pergunta que vale mais que o desenho: **"E se eu inverter uma seta?"**

Resposta: o domínio passaria a saber que existe HTTP. Aí não dá mais para usá-lo no console sem
arrastar a API junto — e qualquer mudança de rota mexeria nas regras de negócio.

### Mão na massa

Os comandos estão no `DOC-2`, seção FASE 1. **Deixe-os digitar.** São seis comandos e o valor
está em errar o caminho e consertar.

⚠ **Avise antes:** o `.git` fica na raiz e não se move. Se descer junto com o domínio, a API
nasce fora do controle de versão **sem erro nenhum**.

### O erro planejado — pare aqui

Depois de tirar o `<OutputType>Exe</OutputType>` e mandar compilar:

```
error CS8805: O programa que usa as instruções de nível superior precisa ser um executável.
```

**Não conserte imediatamente.** Pergunte: *"o que este erro está pedindo?"*

Deixe-os chegar em: `Program.cs` é código solto sem `class` nem `Main`; o compilador transforma
isso num ponto de entrada; biblioteca não tem ponto de entrada; logo, o `Program.cs` está no
projeto errado.

**Este é o momento didático do bloco.** O erro não se conserta onde apareceu — ele diz qual é o
próximo passo.

### Fechamento — o critério de sucesso

`dotnet run --project Biblioteca.Laboratorio` produz **exatamente a mesma saída de antes**.

Diga em voz alta: *"mudamos a estrutura inteira e o comportamento não mudou em nada. É isso que
`refactor` significa."*

---

# BLOCO 2 — o primeiro endpoint

**Objetivo:** HTTP não tem memória, e por isso duas coisas precisam existir: identidade e um
lugar onde os objetos moram.

### Abertura — o problema, encenado

Abra o `Program.cs` do Laboratório e aponte:

```csharp
var revistaDoCaio = caio.Emprestar(new Revista("Superinteressante", "Editora Abril"));
```

> "Este objeto tem endereço porque eu o segurei numa variável. Agora me digam: como escrevo uma
> URL para ele?"

Não dá. E é isso que motiva as duas etapas do bloco.

### Identidade — e a alternativa recusada

`Id` no `ItemAcervo`, contador estático. Mostre a alternativa (Id vindo do armazenamento,
`Dictionary<int, ItemAcervo>`) e o trade-off do `DOC-2`, etapa 0.

**Detalhe que vale parar:** o `Id = _proximoId++` fica **depois** da validação do título.
Pergunte por quê antes de responder.

**Se a turma for mais avançada**, vale a pergunta desconfortável: *"um livro no mundo real tem
número de série? Quem atribui esse número?"* — é a discussão sobre o domínio carregar um
conceito que só existe por causa de HTTP.

### Onde o acervo mora — a armadilha para provocar

Escreva o `new Acervo()` **dentro** do endpoint de propósito:

```csharp
app.MapGet("/itens", () => {
    var acervo = new Acervo();     // ERRADO — de propósito
    // ...
});
```

Deixe-os criar um item pelo POST e depois consultar. Sumiu.

> "Nenhum erro. Compila, sobe, responde 200. E está errado."

Depois mova para fora e mostre funcionando. **Esta é a demonstração mais barata do bloco e a que
mais fixa** — porque o bug não tem sintoma até você procurar.

### Os dois GETs — a decisão

Só uma decisão importa aqui, e ela é conceitual:

> **Lista vazia é 200 com `[]`. Item inexistente é 404.**

Pergunte: *"por que os dois não são 404?"* A resposta: `/itens` é uma coleção que existe e está
vazia. 404 diria "esta URL não é recurso nenhum", o que é mentira.

### O que a saída revela — dois minutos que rendem

Rode `/itens` e `/itens/2` lado a lado. A ordem das chaves muda. Pergunte por quê.

Resposta no `DOC-2`, etapa 2 — tipo estático que o serializador enxerga. E vale dizer: **em JSON
a ordem das chaves não tem significado.** Nenhum cliente deve depender dela.

E o problema real: `Livro` e `Revista` saem indistinguíveis. Guarde a pergunta — ela volta no
bloco 4.

---

# BLOCO 3 — quando o domínio recusa

**Objetivo:** o mais conceitual do curso. Erro de regra de negócio não é erro de programa.

### Abertura — provoque o 500

Antes de qualquer explicação, faça um endpoint que estoura:

```csharp
app.MapGet("/estouro-teste", () => new Livro("", "Ninguem"));
```

Chame. Sai **500** com a página de erro do ASP.NET, stack trace inteiro.

> "Isto está errado. Não o código — a resposta. 500 significa 'a API quebrou'. A API não quebrou:
> ela funcionou perfeitamente e o domínio recusou um pedido inválido. São coisas diferentes, e o
> cliente precisa saber qual foi."

### A escada dos status codes

Vale desenhar:

| status | significa | exemplo aqui |
|---|---|---|
| 400 | "você escreveu errado" | JSON malformado |
| **409** | "escreveu certo, o estado não permite" | item já emprestado |
| 404 | "isso não existe" | `/itens/99` |
| 500 | "eu quebrei" | bug de verdade |

> "Item já emprestado é o oposto de 400. O pedido está perfeito. É o mundo que não deixa."

### Onde o tratamento vive

Duas saídas — `try/catch` por endpoint × middleware. Mostre a conta: 11 endpoints, e a maioria
toca o domínio. **Esquecer um vaza 500 e o compilador não avisa.**

### O momento da ordem do pipeline — provoque de novo

Escreva o `app.Use(...)` **depois** dos `Map`, de propósito. Rode. **500.**

Depois mova para antes. Rode. **409.**

> "Mesma linha de código. Lugar diferente. Comportamento oposto."

Aí desenhe:

```
Build()
   ↓
Use     ← try  ┐
   ↓           │  o catch só cobre
Map            │  o que roda aqui dentro
   ↓           │
Run     ← catch┘
```

Um middleware só enxerga o que roda dentro do seu `await next()`, e `next()` é **tudo que foi
registrado depois dele**.

**Regra para escreverem no caderno:** `Build()` → `Use` → `Map` → `Run()`, sempre.

### O `catch` específico — pergunta rápida

> "Por que `catch (ExcecaoDominio)` e não `catch (Exception)`?"

Com o genérico, um `NullReferenceException` viraria 409 — "sua regra foi recusada" — quando na
verdade a API quebrou. **Bug tem que doer.**

---

# BLOCO 4 — escrita

**Objetivo:** o mundo externo não pode construir objetos do domínio diretamente.

### Abertura — a pergunta que abre tudo

> "O JSON chega. Como ele vira um `Livro`?"

Deixe tentarem. Alguém vai propor `app.MapPost("/itens", (ItemAcervo item) => ...)`.

Não compila: **classe abstrata não se instancia**. E mesmo que fosse concreta, o serializador não
saberia escolher entre `Livro`, `Revista` e `Dvd`.

É aqui que a pergunta guardada do bloco 2 volta: **o tipo não sai no JSON, e agora ele faz falta.**

### As duas saídas

Mostre a tabela do `DOC-2`, etapa 4: campo `tipo` no corpo × rota por tipo.

O argumento decisivo: com rota por tipo, `"revistta"` digitado errado vira **404 do roteador**,
antes de qualquer código seu rodar. Com campo `tipo`, vira um `switch` que falha em runtime.

### Records de requisição — o conceito novo

> "Este é o primeiro objeto que existe só porque HTTP existe. Ele não é domínio."

E o ponto que gera discussão: **nenhuma validação no record.**

> "Por que não valido título vazio aqui, que é onde o dado chega?"

Porque a regra já existe no construtor de `ItemAcervo`. Validar nos dois lugares = duas mensagens
diferentes para a mesma regra, e elas divergem na primeira manutenção.

### A demonstração que fecha o bloco

POST com título vazio → **409 com a mensagem do domínio**, e o endpoint não tem uma linha de
tratamento de erro.

> "Vocês lembram do middleware de ontem? Isto é o que ele comprou. Todo endpoint que eu escrever
> daqui pra frente já nasce coberto."

### PUT — e a parede

Peça para implementarem o PUT sozinhos. **Eles vão travar:**

```
CS0272: the set accessor is inaccessible
```

`Titulo` é `private set`. Não dá para escrever de fora.

**Deixe-os propor a solução.** Alguém vai sugerir abrir o `set`. Aí a pergunta:

> "Se eu abrir, o que mais deixa de valer?"

Resposta: `MarcarComoEmprestado`, `PermiteIdade`, a validação do construtor — tudo isso vale
porque o estado não pode ser mexido por fora. Com `set` público, qualquer linha zera um título.

**A saída certa:** o domínio abre uma **porta declarada** — `AlterarDados(titulo, autor)`.

E o mais importante é o que a porta **não** aceita:

| campo | por que fica de fora |
|---|---|
| `Id` | é a identidade — trocar significa alterar outro item |
| `Disponibilidade` | um PUT com `disponibilidade: true` num item emprestado apagaria o empréstimo sem devolvê-lo. Item volta ao acervo, `Emprestimo` continua em aberto. **Estado corrompido em silêncio.** |

### DELETE — a pergunta que eles respondem

> "E se o item estiver emprestado?"

**Não responda.** Deixe-os listar as consequências de apagar mesmo assim. Se travarem, dê a
pista: *"o `Emprestimo` na lista da pessoa aponta para quê, depois do delete?"*

As três saídas estão no `DOC-2`, etapa 6.

E a observação que fecha: **esta é a primeira regra que não cabe no domínio.** Ela precisa do
estado do item **e** da coleção onde ele está — e `ItemAcervo` não sabe que existe um acervo.

---

# BLOCO 5 — o agregado

**Objetivo:** o mais importante. A regra de negócio pode ser destruída por código que compila.

### Abertura — mostre os dois caminhos

Escreva os dois lado a lado, sem dizer qual é o certo:

```csharp
var emprestimo = pessoa.Emprestar(item);
var emprestimo = new Emprestimo(pessoa, item);
```

> "Os dois compilam. Os dois marcam o item como emprestado. Qual é a diferença?"

Deixe-os abrir `Pessoa.Emprestar` e ler. A lista:

| | `Emprestar` | `new Emprestimo` |
|---|---|---|
| checa idade mínima | ✅ | ❌ |
| checa limite de três | ✅ | ❌ |
| marca item emprestado | ✅ | ✅ |
| **entra na lista da pessoa** | ✅ | ❌ |

E o golpe final:

> "Repare na última linha. Se o empréstimo não entra na lista, `QtdEmprestimosEmAberto` continua
> respondendo o valor antigo. **O limite de três deixa de existir.** E nada lança exceção. Nada
> avisa. Vocês só descobrem quando alguém levar oito livros."

### A demonstração ao vivo, se houver tempo

Troque para `new Emprestimo(...)` no endpoint. Empreste quatro itens para a mesma pessoa de 15
anos, incluindo um DVD para maiores de 16.

Todos passam. **201 em todos.**

Volte para `pessoa.Emprestar(...)`. O quarto dá 409, e o DVD dá 409 por idade.

> "O código que 'funcionava' era o quebrado."

### A assimetria da devolução

> "Emprestar é da `Pessoa`. Devolver é do `Emprestimo`. Então, para devolver, o que a API precisa
> fazer primeiro?"

Achar o empréstimo. E o filtro tem duas condições:

```csharp
e => e.Item.Id == requisicao.ItemId && e.EstaEmAberto
```

Pergunte o que acontece **sem** o `EstaEmAberto`. Resposta: um item emprestado, devolvido e
emprestado de novo casaria com o registro antigo, já fechado — e a devolução nova estouraria "já
foi devolvido" apontando para o empréstimo errado.

### O ciclo do JSON — se ainda houver fôlego

Este assunto pertence a `/pessoas`, mas pode entrar aqui se o tempo apertar.

Faça o endpoint devolver `pessoa` direto. Com pessoa **sem** empréstimo, funciona. Com
empréstimo: **500 — `A possible object cycle was detected`.**

> "Repare: funcionava. Passou no teste manual. E quebrou quando alguém emprestou um livro."

As três saídas estão no `DOC-2`, etapa 7. A que interessa ensinar é por que `[JsonIgnore]` no
domínio está **fora de questão**: poria um atributo de serialização dentro do projeto que não
pode saber que JSON existe.

### Fechamento da aula inteira

Volte à frase do começo e mostre o caminho completo de uma recusa:

```
Pessoa.Emprestar          lança ExcecaoDominio
        ↓
middleware                traduz em 409
        ↓
cliente                   { "erro": "Marina tem 15 anos e o item ... é para 16 anos ou mais." }
```

> "A mensagem que o usuário lê foi escrita dentro do domínio, num arquivo que não sabe o que é
> HTTP. Nenhuma linha da API validou idade. E não dá para burlar pela API, porque a API não tem
> outro caminho."

---

## Exercícios — em ordem de dificuldade

**Mecânicos** (fixam sintaxe e o ciclo requisição-resposta)
1. `GET /itens/disponiveis` — só os com `Disponibilidade == true`.
2. `GET /pessoas/{id}/emprestimos` — usando `EmprestimoResposta`.
3. `GET /itens/{id}` devolvendo também o tipo (`"livro"`, `"revista"`, `"dvd"`).

**Conceituais** (exigem decidir onde a regra mora)
4. `PUT /pessoas/{id}` — o que pode mudar? `DataNascimento` pode? E se mudar a idade e ela já
   estiver com um DVD +16 emprestado?
5. `DELETE /pessoas/{id}` — e se ela tiver empréstimo em aberto? (mesma discussão do DELETE de
   item, mas eles têm que chegar sozinhos)
6. Idade máxima: hoje uma pessoa nasce em 01/01/0001 e nada reclama. **Onde a regra deve ficar?**
   (resposta: construtor de `Pessoa`, não no record de requisição)

**Difíceis** (mexem no domínio)
7. `QtdDiasAtrasados` devolve `-14` para quem não está atrasado. Conserte — e justifique se a
   correção é do domínio ou da projeção.
8. Renovação de empréstimo: estende o prazo se não houver atraso. Onde esse método vive?
9. Multa paga: como registrar sem quebrar o congelamento da multa na devolução?

---

## Perguntas que a turma vai fazer

**"Por que não uso banco de dados?"**
Porque persistência é outro assunto e traria EF Core, migrations e connection string junto. O
acervo em memória tem exatamente o comportamento necessário para aprender HTTP: sobrevive entre
requisições, morre com o processo.

**"Por que `record` e não `class` para as requisições?"**
Porque são dados sem comportamento, e imutáveis por natureza — o que chegou pela rede não muda.
`class` funcionaria; `record` diz a intenção em menos linhas.

**"Isso não é muita camada para uma biblioteca?"**
Para uma biblioteca de verdade, sim. O exercício não é a biblioteca — é a separação. Três
projetos com sete classes mostram a direção da dependência melhor do que trinta classes num só.

**"Por que os Ids somem quando reinicio?"**
Contador `static` é por processo, e a lista está em memória. É a pergunta que motiva persistência,
e é uma boa deixa para a próxima aula.

**"Por que tanto comentário no código?"**
Porque este código é material de estudo. Em produção, comentário que explica **o quê** é lixo —
o código já diz. O que sobrevive é o comentário que explica **por quê**, e principalmente **qual
alternativa foi recusada**, porque isso o código não mostra.

---

## Onde eles provavelmente travam

Baseado nos tropeços reais registrados no `DOC-2`.

| trava | sintoma | a pergunta que destrava |
|---|---|---|
| Colar `Map` depois do `app.Run()` | 404 de corpo vazio | "o que `app.Run()` faz com as linhas abaixo dele?" |
| `Use` depois dos `Map` | 500 em vez de 409 | "o que `next()` está chamando?" |
| Verbo não registrado | 405 + `Allow: GET` | "o `Allow` está dizendo o quê?" |
| Compilar com a API rodando | `MSB3027` | "quem está com o arquivo aberto?" |
| Serializar `Pessoa` direto | 500 só quando há empréstimo | "quando exatamente começou a quebrar?" |
| `curl` sem `.exe` no PowerShell | sintaxe estranha, `-i` ignorado | `curl` é apelido de `Invoke-WebRequest` |
| JSON malformado no POST | 400 do framework | faltou `--%` |

**Regra de ouro para a condução:** quando o erro aparecer, **não conserte**. Leia a mensagem em
voz alta e pergunte o que ela está dizendo. Metade dos erros deste projeto se explica sozinha na
própria mensagem — e o hábito de ler o erro vale mais que qualquer endpoint.
