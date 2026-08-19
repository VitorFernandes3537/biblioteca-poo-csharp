# O que existe — Biblioteca: domínio + minimal API

Documento de referência do **estado atual** do repositório. Descreve o que está no disco,
não o caminho até aqui — esse é o assunto do `DOC-2-como-chegamos-aqui.md`.

Escrito em 19/08/2026, no commit `978e9df`.

---

## 1. A solução

Três projetos, .NET 10, amarrados por `Biblioteca.slnx` na raiz.

```
Biblioteca/                          <- raiz do repositório, onde mora o .git
├── Biblioteca.slnx                  <- a solução (formato .slnx, padrão do .NET 10)
├── Biblioteca.Dominio/              <- biblioteca de classes: as regras
│   ├── ItemAcervo.cs                (abstrata)
│   ├── Livro.cs  Revista.cs  Dvd.cs (herdam de ItemAcervo)
│   ├── Pessoa.cs                    (raiz do agregado)
│   ├── Emprestimo.cs
│   └── ExcecaoDominio.cs
├── Biblioteca.Laboratorio/          <- console: exercita o domínio sem HTTP
│   └── Program.cs                   (6 cenas)
└── Biblioteca.Api/                  <- minimal API: expõe o domínio por HTTP
    ├── Program.cs                   (middleware + 11 endpoints)
    ├── Acervo.cs                    (onde os itens moram)
    ├── Cadastro.cs                  (onde as pessoas moram)
    ├── Requisicoes.cs               (records de entrada)
    └── Resposta.cs                  (records de saída)
```

### A direção das dependências

```
Biblioteca.Laboratorio  ──┐
                          ├──>  Biblioteca.Dominio
Biblioteca.Api          ──┘
```

**O domínio não referencia ninguém.** Ele não sabe que existe HTTP, JSON, console ou banco
de dados. Api e Laboratório apontam *para* ele; nunca o contrário.

Consequência prática: qualquer regra de negócio escrita em `Biblioteca.Dominio` vale para os
dois consumidores, sem duplicação. E qualquer coisa que só faça sentido por causa de HTTP
(status code, serialização, formato de resposta) **não pode** descer para o domínio.

O domínio é biblioteca de classes: seu `.csproj` **não tem** `<OutputType>Exe</OutputType>`.
Ele gera `.dll`, não `.exe`, e não tem ponto de entrada — quem roda é o Laboratório ou a Api.

---

## 2. O domínio

### `ItemAcervo` (abstrata)

Base dos três tipos de item. Ninguém instancia `ItemAcervo` direto.

| membro | o que é |
|---|---|
| `Id` | `int`, atribuído no construtor por contador estático `_proximoId` |
| `Titulo`, `Autor` | `string`, `private set` |
| `Disponibilidade` | `bool`, começa `true`; só empréstimo/devolução mudam |
| `PrazoDevolucao` | `abstract int` — cada filha declara o seu |
| `MultaDiaAtrasado` | `abstract decimal` — idem |
| `IdadeMinima` | `virtual int`, default `0`; só `Dvd` sobrescreve |
| `PermiteIdade(idade)` | `idade >= IdadeMinima` |
| `CalcularMulta(dias)` | `dias * MultaDiaAtrasado`, ou `0` se `dias < 0` |
| `MarcarComoEmprestado()` | lança se já estiver emprestado |
| `MarcarComoDevolvido()` | lança se não estiver emprestado |
| `AlterarDados(titulo, autor)` | única porta de escrita depois da criação |

O construtor recusa título vazio (`ExcecaoDominio`) **antes** de gastar Id — tentativa
recusada não queima número.

`AlterarDados` existe porque `Titulo` e `Autor` são `private set`: nenhum código fora da
classe atribui a eles. Abrir o `set` para `public` teria sido a saída errada — com `set`
público, qualquer linha em qualquer lugar zeraria um título e a validação do construtor
viraria enfeite. E repare no que o método **não** aceita: `Id` (é a identidade) e
`Disponibilidade` (só empréstimo e devolução mexem nela).

### Os três tipos

| tipo | prazo | multa/dia | idade mínima |
|---|---|---|---|
| `Livro` | 14 dias | R$ 1,00 | 0 |
| `Revista` | 7 dias | R$ 2,00 | 0 |
| `Dvd` | 3 dias | R$ 3,00 | parâmetro do construtor |

Os três usam **construtor primário** (`public class Livro(string titulo, string autor) : ItemAcervo(titulo, autor)`),
passando os parâmetros para a base.

### `Pessoa` — a raiz do agregado

É o conceito mais importante do desenho.

| membro | o que é |
|---|---|
| `Id` | `int`, contador estático próprio (separado do de `ItemAcervo`) |
| `Nome`, `DataNascimento` | `private set` |
| `Idade` | calculada a partir de `DataNascimento` e da data de hoje |
| `Emprestimos` | `IReadOnlyList<Emprestimo>` — a lista real é privada |
| `QtdEmprestimosEmAberto` | conta os empréstimos com `EstaEmAberto` |
| `LimiteEmprestimosEmAberto` | `const int = 3` |
| `Emprestar(item)` | **o único caminho legítimo para criar um empréstimo** |

`Emprestar` aplica, em ordem:

1. **Idade mínima** — `item.PermiteIdade(Idade)`, senão lança.
2. **Limite de três em aberto** — senão lança.
3. `new Emprestimo(this, item)` — que por sua vez chama `item.MarcarComoEmprestado()`,
   e este lança se o item já estiver com outra pessoa.
4. Adiciona o empréstimo à lista dela.

**A armadilha:** `new Emprestimo(pessoa, item)` compila, marca o item como emprestado e
**pula os passos 1, 2 e 4**. O empréstimo não entra na lista da pessoa —
`QtdEmprestimosEmAberto` continua no valor antigo e o limite de três deixa de existir.
Nada disso lança exceção. A única defesa é a disciplina: **a API nunca chama o construtor
de `Emprestimo` direto.**

### `Emprestimo`

| membro | o que é |
|---|---|
| `Pessoa`, `Item` | referências, `get` sem `set` |
| `DataEmprestimo` | `DateTime.Today` na criação |
| `PrazoLimite` | `DataEmprestimo + item.PrazoDevolucao` dias |
| `DataDevolucao` | `DateTime?` — `null` enquanto em aberto |
| `EstaEmAberto` | `DataDevolucao is null` |
| `QtdDiasAtrasados` | `DataReferencia - PrazoLimite`, em dias |
| `MultaAtual` | `Item.CalcularMulta(QtdDiasAtrasados)` |
| `RegistrarDevolucao()` | lança se já devolvido; senão devolve o item e grava a data |

`DataReferencia` é `DataDevolucao ?? DateTime.Today` — privada. É o que faz a multa
**congelar na devolução**: depois de devolvido, o cálculo passa a usar a data da devolução,
não a de hoje, e o valor para de subir.

**Detalhe que aparece no JSON:** antes do prazo, `QtdDiasAtrasados` é **negativo**
(ex.: `-14` num livro emprestado hoje). A multa está certa — `CalcularMulta` devolve `0`
para dias negativos — mas um campo chamado "dias atrasados" respondendo `-14` para quem não
está atrasado é confuso na resposta. Decisão de domínio ainda em aberto.

### `ExcecaoDominio`

`public class ExcecaoDominio(string mensagem) : Exception(mensagem)`

Uma classe de três linhas que carrega todo o contrato de erro do sistema: **toda recusa de
regra de negócio lança este tipo**, e a API traduz esse tipo — e só ele — em HTTP 409.

---

## 3. A API

### O que mora onde

`Acervo` e `Cadastro` vivem em **`Biblioteca.Api`**, não no domínio. Guardar coisa entre
requisições é problema de quem atende requisição. Ambos seguem o mesmo formato:
`List<T>` privada, `IReadOnlyList<T>` pública, `Adicionar` e `BuscarPorId`.

`Acervo` tem um método a mais — `Remover` — e é ele que carrega a **primeira regra do
sistema que não nasce no domínio**: "não remover item emprestado". Ela precisa de duas
coisas ao mesmo tempo (o estado do item e a coleção onde ele está), e `ItemAcervo` não sabe
que existe um acervo. Mesmo estando fora do domínio, lança `ExcecaoDominio` — a recusa é da
mesma natureza, e o middleware já a traduz em 409.

As duas instâncias são criadas **uma vez**, antes do `app.Run()`:

```csharp
var acervo = new Acervo();
var cadastro = new Cadastro();
```

Fora de qualquer endpoint. Se o `new` estivesse dentro de um endpoint, cada requisição criaria
uma coleção vazia e o POST anterior teria sumido — o bug do "criei o item e o GET não acha".

### O pipeline

```
requisição
   ↓
app.Use(...)        <- try/catch: ExcecaoDominio → 409
   ↓
app.MapGet/Post/... <- os endpoints
   ↓
resposta
```

O middleware está registrado **logo depois de `builder.Build()`, antes de qualquer `Map`**.
A ordem não é estilo: um middleware só enxerga o que roda dentro do seu `await next()`, e
`next()` é tudo que foi registrado **depois** dele. Registrado no fim do arquivo, ele compila,
sobe, e não captura nada — o endpoint já respondeu 500 antes de o `catch` existir.

O `catch` é de `ExcecaoDominio` e **só dela**. Um `catch (Exception)` genérico transformaria
`NullReferenceException` e falha de banco em 409 também — e 409 diz "sua regra foi recusada",
não "eu quebrei". Bug da API deve continuar dando 500, alto e feio.

### Contrato dos endpoints

**Convenções, fechadas e válidas para todos:**
- JSON em toda a API, sempre.
- Mensagem de erro é **valor** de uma chave: `{ "erro": "..." }`, nunca o corpo inteiro.
- Sem envelope: o recurso sai na raiz da resposta.
- `{id:int}` com restrição de rota — `/itens/abc` vira 404 do roteador, sem código.
- 404 para não encontrado; 409 para o domínio recusando.

| verbo | rota | corpo | sucesso | erros |
|---|---|---|---|---|
| GET | `/itens` | — | 200 + array (`[]` se vazio) | — |
| GET | `/itens/{id:int}` | — | 200 + item | 404 |
| POST | `/itens/livros` | `{titulo, autor}` | 201 + `Location: /itens/{id}` | 409 título vazio |
| POST | `/itens/revistas` | `{titulo, autor}` | 201 + `Location` | 409 |
| POST | `/itens/dvds` | `{titulo, autor, idadeMinima}` | 201 + `Location` | 409 |
| PUT | `/itens/{id:int}` | `{titulo, autor}` | 200 + item alterado | 404, 409 |
| DELETE | `/itens/{id:int}` | — | 204 sem corpo | 404, 409 se emprestado |
| GET | `/pessoas` | — | 200 + array de `PessoaResposta` | — |
| GET | `/pessoas/{id:int}` | — | 200 + `PessoaResposta` | 404 |
| POST | `/pessoas` | `{nome, dataNascimento}` | 201 + `Location: /pessoas/{id}` | 409 |
| POST | `/emprestimos` | `{pessoaId, itemId}` | 201 + `EmprestimoResposta` | 404, 409 |
| POST | `/devolucoes` | `{pessoaId, itemId}` | 200 + `EmprestimoResposta` | 404 |

**Lista vazia é 200 com `[]`, não 404.** A coleção existe, só não tem nada dentro. 404
significaria "esta URL não corresponde a recurso nenhum", o que é falso.

**Uma rota de criação por tipo**, e não uma rota só com campo `tipo` no corpo: cada corpo tem
exatamente os campos daquele tipo (`idadeMinima` só existe na de DVD), e tipo errado vira 404
do roteador antes de qualquer código rodar — sem `switch` de string.

**O `Location` do 201 aponta para `/itens/{id}`, não para `/itens/livros/{id}`.** A rota que
cria é por tipo; o recurso criado é um item do acervo, e o GET dele é um só. `Location` aponta
para onde o recurso **está**, não para onde nasceu.

**`POST /emprestimos` responde 201 sem `Location`**, porque não existe `GET /emprestimos/{id}` —
empréstimo não tem Id próprio, identifica-se pelo par pessoa+item.

**`POST /devolucoes` responde 200, não 201** — não criou recurso, alterou um existente. E o
corpo importa: é nele que sai a multa apurada.

### Requisições — os records de entrada

`NovoLivro`, `NovaRevista`, `NovoDvd`, `AlteracaoItem`, `NovaPessoa`, `MovimentacaoEmprestimo`.

Existem porque o JSON precisa virar objeto C# antes de o domínio ser chamado — e o domínio não
aceita ser construído pela metade. Um `Livro` só nasce válido; o record nasce como veio da rede.

Os campos de texto são `string?`, e **não há validação neste arquivo**. Título vazio não vira
erro aqui — vira quando o construtor de `ItemAcervo` recusar. Validar nos dois lugares
significaria duas mensagens diferentes para a mesma regra, e a do domínio é a que manda.

`MovimentacaoEmprestimo` serve para emprestar **e** devolver: os dois identificam o par
pessoa + item. E leva **Ids, não objetos** — o cliente aponta para quem já existe; a API nunca
cria pessoa ou item a partir do corpo de um empréstimo.

### Respostas — as projeções

Serializar `Pessoa` direto **lança**: `Pessoa` tem `Emprestimos`, cada `Emprestimo` tem
`Pessoa`, que tem `Emprestimos`… O serializador anda em círculo e estoura em 500.

`PessoaResposta` e `EmprestimoResposta` cortam o ciclo por não conterem a lista. Cada uma tem
um método de fábrica estático (`De(...)`) para que a tradução fique num lugar só.

Duas alternativas foram consideradas e recusadas:
- **`ReferenceHandler.IgnoreCycles`** (uma linha de configuração): a resposta sairia com
  `"pessoa": null` enterrado dentro de cada empréstimo — um campo que existe, é sempre nulo, e o
  cliente não tem como saber por quê.
- **`[JsonIgnore]` em `Emprestimo.Pessoa`**: poria um atributo de serialização **dentro do
  domínio**, que não pode saber que JSON existe.

Bônus da projeção: `Idade` e `QtdEmprestimosEmAberto` são calculadas em `Pessoa` e entram na
resposta naturalmente. O que sai é escolha da API, não sobra do formato interno do objeto.

**A armadilha:** passar `pessoa` em vez de `PessoaResposta.De(pessoa)` compila, sobe, e só
estoura em 500 quando aquela pessoa tiver o primeiro empréstimo. Até lá, parece funcionar.

### Onde os itens serializados ficam devendo

`GET /itens` serializa `ItemAcervo` direto (sem projeção), e a saída tem três peculiaridades:

1. **`prazoDevolucao`, `multaDiaAtrasado` e `idadeMinima` saem no JSON** como se fossem dados
   do item, quando são regra do tipo. O cliente não tem como saber a diferença.
2. **O tipo não sai.** `Livro` e `Revista` viram objetos indistinguíveis, separados só pelo
   prazo. Foi o que motivou uma rota de criação por tipo.
3. **A ordem das chaves muda** entre `/itens` (serializa por `ItemAcervo`, base primeiro) e
   `/itens/{id}` (serializa pelo tipo concreto, overrides primeiro). Em JSON a ordem não tem
   significado e nenhum cliente deve depender dela — mas aparece na tela e gera pergunta.

---

## 4. Como rodar

```powershell
# a solução inteira
dotnet build

# o console, para exercitar o domínio sem HTTP
dotnet run --project Biblioteca.Laboratorio

# a API
dotnet run --project Biblioteca.Api
```

A porta está em `Biblioteca.Api/Properties/launchSettings.json` — hoje `http://localhost:5249`.

**Pare a API antes de compilar.** Com ela no ar, o build falha com `MSB3027` — não consegue
sobrescrever a `.dll` que o processo está segurando. Não é erro de código.

### Testando pelo terminal

No PowerShell, use `curl.exe` **com o `.exe`**: `curl` sozinho é apelido de `Invoke-WebRequest`,
que tem outra sintaxe e ignora o `-i`. E use `--%` antes dos argumentos, para o PowerShell parar
de interpretar a linha e entregar o JSON cru:

```powershell
curl.exe --% -i -X POST http://localhost:5249/itens/livros -H "Content-Type: application/json" -d "{\"titulo\":\"Dom Casmurro\",\"autor\":\"Machado de Assis\"}"
```

Um ciclo completo, com a API recém-iniciada (contadores em 1):

```powershell
# 1. uma pessoa de 15 anos
curl.exe --% -i -X POST http://localhost:5249/pessoas -H "Content-Type: application/json" -d "{\"nome\":\"Marina\",\"dataNascimento\":\"2011-03-14\"}"

# 2. um livro e um DVD para maiores de 16
curl.exe --% -i -X POST http://localhost:5249/itens/livros -H "Content-Type: application/json" -d "{\"titulo\":\"Dom Casmurro\",\"autor\":\"Machado de Assis\"}"
curl.exe --% -i -X POST http://localhost:5249/itens/dvds -H "Content-Type: application/json" -d "{\"titulo\":\"Cidade de Deus\",\"autor\":\"Fernando Meirelles\",\"idadeMinima\":16}"

# 3. empresta o livro  -> 201
curl.exe --% -i -X POST http://localhost:5249/emprestimos -H "Content-Type: application/json" -d "{\"pessoaId\":1,\"itemId\":1}"

# 4. tenta o DVD       -> 409, idade mínima
curl.exe --% -i -X POST http://localhost:5249/emprestimos -H "Content-Type: application/json" -d "{\"pessoaId\":1,\"itemId\":2}"

# 5. devolve o livro   -> 200 com a multa apurada
curl.exe --% -i -X POST http://localhost:5249/devolucoes -H "Content-Type: application/json" -d "{\"pessoaId\":1,\"itemId\":1}"

# 6. devolve de novo   -> 404, não há empréstimo em aberto desse par
curl.exe --% -i -X POST http://localhost:5249/devolucoes -H "Content-Type: application/json" -d "{\"pessoaId\":1,\"itemId\":1}"
```

**Os Ids zeram quando a API reinicia.** Os contadores são `static` por processo, e o acervo
mora em memória. Reiniciou, começa vazio e conta do 1 de novo.

---

## 5. O que ainda não existe

Nenhum destes é bug — são limites conscientes do recorte.

| falta | consequência hoje |
|---|---|
| Persistência | tudo morre quando a API para |
| `IRepositorioAcervo` + DI | `Acervo` e `Cadastro` são variáveis no `Program.cs` |
| `GET /emprestimos` | não há como listar empréstimos fora de `/pessoas/{id}` |
| Alterar `IdadeMinima` de DVD | `Dvd` lê o parâmetro do construtor primário; não há campo |
| `PUT`/`DELETE` de pessoa | só criação e leitura |
| Regra de idade máxima | data de nascimento em 01/01/0001 é aceita (~2025 anos) |
| Tratamento de `Kind` de data | `DateTime.Today` sai com fuso; data vinda do JSON, sem |
| Testes automatizados | **não há suíte neste projeto** — "compila" ≠ "funciona" |

Fora de escopo por decisão: banco de dados, EF Core, autenticação, front-end, Docker,
deploy, async.

---

## 6. Resumo do desenho, em cinco frases

1. **O domínio não sabe que a API existe** — e é isso que permite trocar a API sem tocar nele.
2. **Toda regra recusa lançando `ExcecaoDominio`** — um tipo só, traduzido em 409 num lugar só.
3. **`Pessoa.Emprestar` é o único caminho** para criar empréstimo; o construtor pula as regras.
4. **O que a API devolve é escolha da API** — projeções, não o objeto interno serializado.
5. **O que não pôde ser escrito de fora** (`private set`) só mudou quando o domínio abriu uma
   porta declarada (`AlterarDados`) — nunca abrindo o `set`.
