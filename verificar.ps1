<#
.SYNOPSIS
    Verifica a API da Biblioteca contra os criterios de uma etapa.

.DESCRIPTION
    IMPORTANTE - leia esta parte, ela evita a confusao numero um:

    Este script verifica o estado da API ao FIM de uma etapa, nao "a API em
    geral". Isso importa porque as etapas 1 e 2 tem ANDAIMES que somem depois:

      * os tres itens semeados no Program.cs  -> saem na Etapa 3
      * a rota /estouro-teste                 -> sai na Etapa 3

    Entao -Etapa 2 rodado contra a API TERMINADA falha de proposito: ele esta
    conferindo um estado que voce ja ultrapassou. Para a API completa, use
    -Etapa 5.

    Da Etapa 3 em diante o script CRIA os proprios dados por POST e le o Id da
    resposta. Ele nunca assume Id nenhum: o contador static nao reinicia entre
    rodadas, e assumir 1 daria falha falsa.

    Usa curl.exe. Sem modulo, sem dependencia, PowerShell 5.1 e 7.

.PARAMETER BaseUrl
    A raiz da API, sem barra no fim.
      local ....... http://localhost:5000
      tunel ....... https://algo-aleatorio.trycloudflare.com

.PARAMETER Etapa
    O estado que voce quer conferir.
      0  a solucao sobe e responde Hello World
      1  + leitura do acervo (usa os itens semeados)
      2  + o middleware traduzindo em 409 (usa /estouro-teste)
      3  + escrita: POST, PUT, DELETE   (cria os proprios dados)
      4  + pessoas, emprestimo e devolucao
      5  + listagem de emprestimos  =  A API COMPLETA

.EXAMPLE
    .\verificar.ps1 -BaseUrl http://localhost:5000 -Etapa 2

.EXAMPLE
    .\verificar.ps1 -BaseUrl https://algo.trycloudflare.com -Etapa 5 -Aluno "Marina" -Csv turma.csv
#>

param(
    [Parameter(Mandatory = $true)][string] $BaseUrl,
    [Parameter(Mandatory = $true)][ValidateRange(0, 5)][int] $Etapa,
    [string] $Aluno = "",
    [string] $Csv = ""
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$BaseUrl = $BaseUrl.TrimEnd('/')

# ---------------------------------------------------------------- infra

$MARCA_S = '<<<STATUS>>>'
$MARCA_H = '<<<HEADERS>>>'

function Invoke-Chamada {
    param([string] $Metodo = 'GET', [string] $Rota, $Corpo = $null)

    # NAO use $args: e variavel automatica do PowerShell.
    $argumentos = @('-s', '--max-time', '20',
                    '-w', "\n$MARCA_S\n%{http_code}\n$MARCA_H\n%{header_json}",
                    '-A', 'verificar-biblioteca/2.0', '-X', $Metodo, "$BaseUrl$Rota")
    if ($null -ne $Corpo) {
        $json = if ($Corpo -is [string]) { $Corpo } else { $Corpo | ConvertTo-Json -Compress }
        $argumentos += @('-H', 'Content-Type: application/json', '-d', $json)
    }

    $bruto = (& curl.exe @argumentos 2>$null) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        return @{ Ok = $false; Rede = $LASTEXITCODE; Status = 0; Corpo = ''; Json = $null; JsonOk = $false; Cabecalhos = $null }
    }

    $iS = $bruto.LastIndexOf($MARCA_S)
    $iH = $bruto.LastIndexOf($MARCA_H)
    if ($iS -lt 0 -or $iH -lt 0) {
        return @{ Ok = $false; Rede = -1; Status = 0; Corpo = ''; Json = $null; JsonOk = $false; Cabecalhos = $null }
    }

    $corpo = $bruto.Substring(0, $iS).Trim()
    $status = 0
    [void][int]::TryParse($bruto.Substring($iS + $MARCA_S.Length, $iH - $iS - $MARCA_S.Length).Trim(), [ref] $status)

    $cab = $null
    try { $cab = $bruto.Substring($iH + $MARCA_H.Length).Trim() | ConvertFrom-Json } catch { }

    # ATENCAO: '[]' | ConvertFrom-Json devolve colecao vazia, e o pipeline do
    # PowerShell a desenrola para $null. Sem esta bandeira, "lista vazia" seria
    # confundido com "nao e JSON" - e [] e uma resposta legitima.
    $json = $null; $jsonOk = $false
    if ($corpo) { try { $json = $corpo | ConvertFrom-Json; $jsonOk = $true } catch { } }

    return @{ Ok = $true; Rede = 0; Status = $status; Corpo = $corpo; Json = $json; JsonOk = $jsonOk; Cabecalhos = $cab }
}

function Get-Cabecalho {
    param($r, [string] $Nome)
    if (-not $r.Cabecalhos) { return $null }
    $prop = $r.Cabecalhos.PSObject.Properties | Where-Object { $_.Name -ieq $Nome } | Select-Object -First 1
    if (-not $prop) { return $null }
    return @($prop.Value)[0]
}

$script:Resultados = @()
$script:Abortar = $false

function Test-Cenario {
    param(
        [string] $Nome, [string] $Metodo = 'GET', [string] $Rota, $Corpo = $null,
        [int] $StatusEsperado, [scriptblock] $Conferir = $null, [string] $Dica = ''
    )
    if ($script:Abortar) { return $null }

    $r = Invoke-Chamada -Metodo $Metodo -Rota $Rota -Corpo $Corpo

    if (-not $r.Ok) {
        $motivo = switch ($r.Rede) {
            6  { 'nao resolveu o endereco - a URL do tunel esta certa?' }
            7  { 'conexao recusada - a API esta rodando? o tunel esta no ar?' }
            28 { 'tempo esgotado - a API subiu mas nao respondeu' }
            default { "curl saiu com codigo $($r.Rede)" }
        }
        $script:Resultados += @{ Nome = $Nome; Passou = $false }
        Write-Host ("  FALHOU  {0}" -f $Nome) -ForegroundColor Red
        Write-Host ("          {0}" -f $motivo) -ForegroundColor DarkGray
        if ($r.Rede -in 6, 7, 28) {
            Write-Host ''
            Write-Host '  Interrompido: sem a API no ar, o resto nao diz nada.' -ForegroundColor Yellow
            $script:Abortar = $true
        }
        return $null
    }

    $problemas = @()
    if ($r.Status -ne $StatusEsperado) { $problemas += "status $($r.Status), esperado $StatusEsperado" }
    if ($Conferir) { $msg = & $Conferir $r; if ($msg) { $problemas += $msg } }

    $passou = ($problemas.Count -eq 0)
    $script:Resultados += @{ Nome = $Nome; Passou = $passou }

    if ($passou) {
        Write-Host ("  ok      {0}" -f $Nome) -ForegroundColor Green
    } else {
        Write-Host ("  FALHOU  {0}" -f $Nome) -ForegroundColor Red
        Write-Host ("          {0}" -f ($problemas -join ' | ')) -ForegroundColor DarkGray
        if ($Dica) { Write-Host ("          {0}" -f $Dica) -ForegroundColor DarkGray }
    }
    return $r
}

function Test-TemErro { param($r)
    if (-not $r.JsonOk -or -not $r.Json) { return 'corpo nao e JSON' }
    if ($r.Json.PSObject.Properties.Name -notcontains 'erro') { return 'corpo sem a chave "erro"' }
    return $null
}
function Test-TemCampos { param($obj, [string[]] $Campos)
    if (-not $obj) { return 'corpo nao e JSON' }
    $n = $obj.PSObject.Properties.Name
    foreach ($c in $Campos) { if ($n -notcontains $c) { return "sem o campo '$c'" } }
    return $null
}

# ---------------------------------------------------------------- cabecalho

$rotulo = @{
  0='a solucao sobe'; 1='+ leitura do acervo'; 2='+ o middleware'
  3='+ escrita'; 4='+ pessoas e emprestimo'; 5='+ listagem  =  API COMPLETA'
}[$Etapa]

Write-Host ''
Write-Host '  Biblioteca - verificacao da API' -ForegroundColor Cyan
Write-Host ("  {0}" -f $BaseUrl) -ForegroundColor DarkGray
if ($Aluno) { Write-Host ("  aluno: {0}" -f $Aluno) -ForegroundColor DarkGray }
Write-Host ("  conferindo o estado ao FIM da ETAPA {0}  ({1})" -f $Etapa, $rotulo) -ForegroundColor DarkGray
Write-Host ''

# ---------------------------------------------------------------- ETAPA 0

if ($Etapa -eq 0) {
    Write-Host '  ETAPA 0' -ForegroundColor White
    Test-Cenario -Nome 'GET /  o Hello World do modelo' -Rota '/' -StatusEsperado 200 `
        -Conferir { param($r) if ($r.Corpo -notmatch 'Hello World') { 'corpo sem "Hello World"' } } `
        -Dica 'esta rota e apagada na Etapa 1 - dali em diante use -Etapa 1 ou maior' | Out-Null
}

# ---------------------------------------------------------------- ETAPA 1

if ($Etapa -ge 1) {
    Write-Host '  ETAPA 1  - leitura do acervo' -ForegroundColor White

    $lista = Test-Cenario -Nome 'GET /itens  devolve a colecao' -Rota '/itens' -StatusEsperado 200 `
        -Conferir { param($r)
            if (-not $r.JsonOk) { return 'corpo nao e JSON' }
            $itens = @($r.Json)
            # a semeadura so existe nas etapas 1 e 2; da 3 em diante o acervo
            # comeca vazio de proposito, e quem cria dado e o POST
            if ($itens.Count -eq 0 -and $Etapa -le 2) {
                return 'lista vazia - o new Acervo() ficou dentro do endpoint?'
            }
            if ($itens.Count -gt 1) {
                $ids = @($itens | ForEach-Object { $_.id } | Sort-Object -Unique)
                if ($ids.Count -lt $itens.Count) { return 'Ids repetidos - o contador nao esta static' }
            }
            return $null
        }

    # da Etapa 3 em diante o acervo pode estar vazio: crie o que precisa
    $idItem = $null
    if ($lista -and $lista.Ok) {
        $existentes = @($lista.Json)
        if ($existentes.Count -gt 0) { $idItem = $existentes[0].id }
    }
    if ($null -eq $idItem -and $Etapa -ge 3) {
        $semeado = Invoke-Chamada -Metodo POST -Rota '/itens' -Corpo @{ tipo='livro'; titulo='Dom Casmurro'; autor='Machado de Assis'; idadeMinima=0 }
        if ($semeado.Ok -and $semeado.Json) { $idItem = $semeado.Json.id }
    }
    if ($null -eq $idItem) { $idItem = 1 }

    Test-Cenario -Nome ("GET /itens/{0}  devolve um item" -f $idItem) -Rota "/itens/$idItem" -StatusEsperado 200 `
        -Conferir { param($r) Test-TemCampos $r.Json @('id','titulo','autor') } | Out-Null

    Test-Cenario -Nome 'GET /itens/999999  404 do seu codigo, COM corpo' -Rota '/itens/999999' -StatusEsperado 404 `
        -Conferir { param($r) Test-TemErro $r } `
        -Dica 'a rota existe; o item e que nao. Quem responde e o seu endpoint' | Out-Null

    Test-Cenario -Nome 'GET /itens/abc  404 do roteador, SEM corpo' -Rota '/itens/abc' -StatusEsperado 404 `
        -Conferir { param($r) if ($r.Corpo) { "veio corpo: $($r.Corpo)" } } `
        -Dica 'sem a restricao {id:int} isto vira 400 do framework, e o corpo aparece' | Out-Null
}

# ---------------------------------------------------------------- ETAPA 2

if ($Etapa -eq 2) {
    Write-Host ''
    Write-Host '  ETAPA 2  - a recusa do dominio vira resposta' -ForegroundColor White
    Test-Cenario -Nome 'GET /estouro-teste  409 com a mensagem do dominio' -Rota '/estouro-teste' -StatusEsperado 409 `
        -Conferir { param($r) Test-TemErro $r } `
        -Dica '500 aqui = o app.Use foi registrado DEPOIS dos app.Map' | Out-Null
}
elseif ($Etapa -ge 3) {
    Write-Host ''
    Write-Host '  ETAPA 2  - o middleware, provado pelo POST (o /estouro-teste ja saiu)' -ForegroundColor White
}

# ---------------------------------------------------------------- ETAPA 3

$idCriado = $null
if ($Etapa -ge 3) {
    Write-Host ''
    Write-Host '  ETAPA 3  - escrita' -ForegroundColor White

    $novo = Test-Cenario -Nome 'POST /itens  cria um livro  201 + Location' -Metodo POST -Rota '/itens' `
        -Corpo @{ tipo='livro'; titulo='Vidas Secas'; autor='Graciliano Ramos'; idadeMinima=0 } -StatusEsperado 201 `
        -Conferir { param($r)
            $f = Test-TemCampos $r.Json @('id','titulo'); if ($f) { return $f }
            if (-not (Get-Cabecalho $r 'Location')) { return 'sem o cabecalho Location' }
            return $null
        } -Dica 'o Location aponta para o GET do recurso criado'
    if ($novo -and $novo.Json) { $idCriado = $novo.Json.id }

    Test-Cenario -Nome 'POST /itens  tipo inexistente  409' -Metodo POST -Rota '/itens' `
        -Corpo @{ tipo='revistta'; titulo='Veja'; autor='Abril'; idadeMinima=0 } -StatusEsperado 409 `
        -Conferir { param($r) Test-TemErro $r } `
        -Dica 'sem o descarte _ no switch isto vira 500' | Out-Null

    Test-Cenario -Nome 'POST /itens  titulo vazio  409 (a regra e do dominio)' -Metodo POST -Rota '/itens' `
        -Corpo @{ tipo='livro'; titulo=''; autor='Ninguem'; idadeMinima=0 } -StatusEsperado 409 `
        -Conferir { param($r) Test-TemErro $r } | Out-Null

    if ($idCriado) {
        Test-Cenario -Nome ("PUT /itens/{0}  altera  200" -f $idCriado) -Metodo PUT -Rota "/itens/$idCriado" `
            -Corpo @{ titulo='Vidas Secas (2a edicao)'; autor='Graciliano Ramos' } -StatusEsperado 200 `
            -Conferir { param($r) Test-TemCampos $r.Json @('titulo') } | Out-Null

        Test-Cenario -Nome ("PUT /itens/{0}  titulo vazio  409" -f $idCriado) -Metodo PUT -Rota "/itens/$idCriado" `
            -Corpo @{ titulo=''; autor='Ninguem' } -StatusEsperado 409 `
            -Conferir { param($r) Test-TemErro $r } `
            -Dica 'mesma regra do POST, mesma mensagem - por isso ela vive no dominio' | Out-Null
    }

    Test-Cenario -Nome 'PUT /itens/999999  404' -Metodo PUT -Rota '/itens/999999' `
        -Corpo @{ titulo='Fantasma'; autor='Ninguem' } -StatusEsperado 404 `
        -Conferir { param($r) Test-TemErro $r } | Out-Null

    if ($idCriado) {
        Test-Cenario -Nome ("DELETE /itens/{0}  204 sem corpo" -f $idCriado) -Metodo DELETE -Rota "/itens/$idCriado" -StatusEsperado 204 `
            -Conferir { param($r) if ($r.Corpo) { "204 nao pode ter corpo, e veio: $($r.Corpo)" } } | Out-Null
    }

    Test-Cenario -Nome 'DELETE /itens/999999  404' -Metodo DELETE -Rota '/itens/999999' -StatusEsperado 404 `
        -Conferir { param($r) Test-TemErro $r } | Out-Null
}

# ---------------------------------------------------------------- ETAPA 4

$idPessoa = $null; $idMenor = $null; $idDvd = $null; $idLivro = $null
if ($Etapa -ge 4) {
    Write-Host ''
    Write-Host '  ETAPA 4  - pessoas, emprestimo e devolucao' -ForegroundColor White

    $p = Test-Cenario -Nome 'POST /pessoas  cria  201 + Location' -Metodo POST -Rota '/pessoas' `
        -Corpo @{ nome='Caio'; dataNascimento='1996-05-02' } -StatusEsperado 201 `
        -Conferir { param($r)
            $f = Test-TemCampos $r.Json @('id','nome','idade'); if ($f) { return $f }
            if (-not (Get-Cabecalho $r 'Location')) { return 'sem o cabecalho Location' }
            return $null
        }
    if ($p -and $p.Json) { $idPessoa = $p.Json.id }

    Test-Cenario -Nome 'POST /pessoas  nome vazio  409' -Metodo POST -Rota '/pessoas' `
        -Corpo @{ nome=''; dataNascimento='1990-01-01' } -StatusEsperado 409 `
        -Conferir { param($r) Test-TemErro $r } | Out-Null

    Test-Cenario -Nome 'GET /pessoas/999999  404' -Rota '/pessoas/999999' -StatusEsperado 404 `
        -Conferir { param($r) Test-TemErro $r } | Out-Null

    # material do cenario da idade
    $menor = Invoke-Chamada -Metodo POST -Rota '/pessoas' -Corpo @{ nome='Marina'; dataNascimento='2011-03-14' }
    if ($menor.Ok -and $menor.Json) { $idMenor = $menor.Json.id }
    $dvd = Invoke-Chamada -Metodo POST -Rota '/itens' -Corpo @{ tipo='dvd'; titulo='Cidade de Deus'; autor='Fernando Meirelles'; idadeMinima=16 }
    if ($dvd.Ok -and $dvd.Json) { $idDvd = $dvd.Json.id }
    $liv = Invoke-Chamada -Metodo POST -Rota '/itens' -Corpo @{ tipo='livro'; titulo='O Cortico'; autor='Aluisio Azevedo'; idadeMinima=0 }
    if ($liv.Ok -and $liv.Json) { $idLivro = $liv.Json.id }

    if ($idPessoa -and $idLivro) {
        Test-Cenario -Nome 'POST /emprestimos  empresta  201' -Metodo POST -Rota '/emprestimos' `
            -Corpo @{ pessoaId=$idPessoa; itemId=$idLivro } -StatusEsperado 201 `
            -Conferir { param($r) Test-TemCampos $r.Json @('pessoaId','itemId','estaEmAberto') } | Out-Null

        Test-Cenario -Nome 'POST /emprestimos  item ja emprestado  409' -Metodo POST -Rota '/emprestimos' `
            -Corpo @{ pessoaId=$idPessoa; itemId=$idLivro } -StatusEsperado 409 `
            -Conferir { param($r) Test-TemErro $r } | Out-Null

        Test-Cenario -Nome ("DELETE /itens/{0}  emprestado  409" -f $idLivro) -Metodo DELETE -Rota "/itens/$idLivro" -StatusEsperado 409 `
            -Conferir { param($r) Test-TemErro $r } `
            -Dica 'remover item emprestado deixaria o emprestimo apontando para o vazio' | Out-Null
    }

    if ($idMenor -and $idDvd) {
        Test-Cenario -Nome 'POST /emprestimos  menor de idade + DVD 16  409' -Metodo POST -Rota '/emprestimos' `
            -Corpo @{ pessoaId=$idMenor; itemId=$idDvd } -StatusEsperado 409 `
            -Conferir { param($r) Test-TemErro $r } `
            -Dica 'a regra de idade e do dominio, e sobe pelo middleware' | Out-Null
    }

    if ($idPessoa -and $idLivro) {
        Test-Cenario -Nome 'POST /devolucoes  devolve  200' -Metodo POST -Rota '/devolucoes' `
            -Corpo @{ pessoaId=$idPessoa; itemId=$idLivro } -StatusEsperado 200 `
            -Conferir { param($r)
                $f = Test-TemCampos $r.Json @('dataDevolucao','estaEmAberto'); if ($f) { return $f }
                if ($r.Json.estaEmAberto) { return 'estaEmAberto continua true depois da devolucao' }
                return $null
            } -Dica '200 e nao 201: a devolucao alterou, nao criou' | Out-Null

        Test-Cenario -Nome 'POST /devolucoes  de novo  404' -Metodo POST -Rota '/devolucoes' `
            -Corpo @{ pessoaId=$idPessoa; itemId=$idLivro } -StatusEsperado 404 `
            -Conferir { param($r) Test-TemErro $r } `
            -Dica 'nao ha emprestimo EM ABERTO desse par - o recurso apontado nao existe' | Out-Null
    }
}

# ---------------------------------------------------------------- ETAPA 5

if ($Etapa -ge 5) {
    Write-Host ''
    Write-Host '  ETAPA 5  - ler o que foi gravado' -ForegroundColor White

    Test-Cenario -Nome 'GET /emprestimos  o historico' -Rota '/emprestimos' -StatusEsperado 200 `
        -Conferir { param($r) if (-not $r.JsonOk) { 'corpo nao e JSON' } } | Out-Null

    Test-Cenario -Nome 'GET /emprestimos?emAberto=false  so os devolvidos' -Rota '/emprestimos?emAberto=false' -StatusEsperado 200 `
        -Conferir { param($r)
            if (-not $r.JsonOk) { return 'corpo nao e JSON' }
            foreach ($e in @($r.Json)) { if ($e.estaEmAberto) { return 'veio emprestimo em aberto no filtro de devolvidos' } }
            return $null
        } -Dica 'com bool nao-anulavel, omitir o parametro daria false e o sem-filtro viraria isto' | Out-Null

    if ($idPessoa) {
        Test-Cenario -Nome ("GET /pessoas/{0}/emprestimos  rota aninhada" -f $idPessoa) -Rota "/pessoas/$idPessoa/emprestimos" -StatusEsperado 200 `
            -Conferir { param($r) if (-not $r.JsonOk) { 'corpo nao e JSON' } } | Out-Null
    }

    Test-Cenario -Nome 'GET /pessoas/999999/emprestimos  404 da PESSOA' -Rota '/pessoas/999999/emprestimos' -StatusEsperado 404 `
        -Conferir { param($r) Test-TemErro $r } `
        -Dica 'pessoa inexistente nao e "sem emprestimos" - devolver [] responderia a pergunta errada' | Out-Null
}

# ---------------------------------------------------------------- resumo

$total = $script:Resultados.Count
$ok = @($script:Resultados | Where-Object { $_.Passou }).Count
$falhas = $total - $ok

Write-Host ''
if ($total -eq 0) {
    Write-Host '  nenhum cenario rodou.' -ForegroundColor Yellow
} elseif ($falhas -eq 0) {
    Write-Host ("  {0}/{1} - PASSOU. Pode commitar." -f $ok, $total) -ForegroundColor Green
} else {
    Write-Host ("  {0}/{1} - FALHOU em {2} cenario(s)." -f $ok, $total, $falhas) -ForegroundColor Red
    Write-Host '  Nao commite ainda: commit e foto de estado que funciona.' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '  Se voce esta rodando contra a API COMPLETA, use -Etapa 5.' -ForegroundColor DarkGray
    Write-Host '  As etapas 1 e 2 conferem andaimes que somem na Etapa 3.' -ForegroundColor DarkGray
}
Write-Host ''

if ($Csv) {
    if (-not (Test-Path $Csv)) { 'aluno,base_url,etapa,ok,total,quando' | Out-File -FilePath $Csv -Encoding utf8 }
    ('"{0}","{1}",{2},{3},{4},"{5}"' -f $Aluno, $BaseUrl, $Etapa, $ok, $total, (Get-Date -Format 'yyyy-MM-dd HH:mm')) |
        Out-File -FilePath $Csv -Encoding utf8 -Append
    Write-Host ("  registrado em {0}" -f $Csv) -ForegroundColor DarkGray
    Write-Host ''
}

exit $falhas
