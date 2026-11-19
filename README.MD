# Boletim Plus — API .NET

Global Solution 2026/2 · CitizenTech · Disciplina: Advanced Business Development with .NET

Backend .NET da Global Solution 2026/2 (CitizenTech). O Boletim Plus aproxima escola e família:
o professor lança notas e chamada, e o responsável acompanha o desempenho e a frequência do aluno
pelo app. Esta API atende **sozinha o contrato do app**, conta (login, cadastro, senha, nome), 
gestão escolar, sala de aula, dependentes e boletim. No login do app, "Servidor Alternativo" 
usa só esta API. 

| Nome                      | RM     |
| ------------------------- | ------ |
| Gustavo Barrios de Araújo | 563358 |
| Phietro Solon Oliveira    | 563842 |

App mobile que consome esta API: <https://github.com/Gustavo-B-Araujo/gs-26-2-mobile-application-development>


## Rodar

1. Instale o [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Rode os scripts `01_ddl.sql` e `02_dml.sql` do repositório de banco no Oracle. A API não cria tabelas.
3. Guarde a configuração com user-secrets (fica fora do repositório):

   ```bash
   cd src/BoletimPlus.Api
   dotnet user-secrets set JWT_SECRET "uma-chave-com-32+-caracteres-igual-a-do-java"
   dotnet user-secrets set ConnectionStrings:Oracle "User Id=RM000000;Password=...;Data Source=oracle.fiap.com.br:1521/ORCL"
   ```

   ```bash
   export JWT_SECRET="<chave-com-32-ou-mais-caracteres>"
   export ConnectionStrings__Oracle="User Id=boletim;Password=boletim;Data Source=localhost:1521/FREEPDB1"
   ```

4. Suba a API: `dotnet run --project src/BoletimPlus.Api`. O Swagger abre em `http://localhost:5134/swagger`.

No celular, suba com `--urls http://0.0.0.0:5134` e use o IP da máquina no `.env` do app
(`EXPO_PUBLIC_DOTNET_API_URL=http://192.168.0.10:5134`).

### Docker

```bash
cp .env.example .env    # preencha os valores
docker build -t boletim-plus-dotnet .
docker run --env-file .env -p 5134:8080 boletim-plus-dotnet
```

### Variáveis

| Variável                      | Obrigatória | Uso                                                              |
| ----------------------------- | ----------- | ---------------------------------------------------------------- |
| `JWT_SECRET`                  | Sim         | Chave HS256, 32+ caracteres (bytes UTF-8); igual à do Java para o token valer nos 2 |
| `ConnectionStrings__Oracle`   | Sim         | Conexão Oracle                                                   |
| `JWT_ISSUER`                  | Não         | `iss` do token emitido (padrão `boletim-plus`, como no Java); validado só quando definido |
| `JWT_EXPIRATION_MINUTES`      | Não         | Validade do token emitido (padrão 480 = 8 h, como no Java)       |
| `JWT_AUDIENCE`                | Não         | `aud` do token; validado só quando definido                      |
| `RateLimit__LimitePorJanela`  | Não         | Requisições por usuário por minuto (padrão 100)                  |
| `RateLimit__LimiteEscrita`    | Não         | POST de criação por usuário por minuto (padrão 20)               |

Sem `JWT_SECRET` ou sem a conexão Oracle, a API para na subida com a mensagem do que falta.

## Testar pelo Swagger

1. Chame `POST /auth/login` com `{ "num_documento": "12345678900", "senha": "<senha>" }`
   (em Development, `POST /dev/token` também gera um token, sem conferir o status do usuário).
2. Copie o `token` da resposta.
3. Clique em **Authorize** e cole o token.
4. Nos POST de criação, preencha o header `Idempotency-Key` com um valor novo (ex.: um UUID).

`POST /dev/token` só existe em Development. Em produção, a rota não é mapeada.

Para o app usar só esta API, aponte `EXPO_PUBLIC_DOTNET_API_URL` para ela e escolha "Servidor
Alternativo" no login.

## Requisitos da disciplina

| Requisito                       | Como a API atende                                                                                       | Onde                                     |
| ------------------------------- | ------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| Minimal API RESTful             | 33 rotas em grupos, validação com mensagem, 200/201/204/400/401/403/404/409/422/429                     | `Endpoints/`, `Dominio/Regras.cs`        |
| OpenAPI e Swagger               | Resumo, descrição, corpos, respostas de erro, botão Authorize, header `Idempotency-Key`                 | `Infra/Swagger.cs`                       |
| Idempotência                    | Header `Idempotency-Key` nos POST de criação: mesma chave devolve a mesma resposta; corpo diferente, 422 | `Infra/Idempotencia.cs`                  |
| Rate limiting                   | Janela fixa por usuário + janela deslizante nos cadastros; 429 com `Retry-After`                        | `Infra/LimiteDeRequisicoes.cs`           |
| JWT                             | Emite no login/cadastro e valida (HS256, igual ao Java); papel lido no banco pelo `sub`; políticas Diretor, Professor e Guardiao | `Seguranca/Jwt.cs`, `Program.cs` |
| Health checks                   | `/health`: API e Oracle; `/health/live`: só a API                                                       | `Infra/Saude.cs`                         |
| Testes xUnit                    | Unitários (regras, mapeamento Oracle) e de integração (API inteira em memória)                          | `tests/`                                 |

### Idempotência

- POST de criação (`/professores`, `/alunos`, `/escolas/:cod/turmas`, `/turmas/:cod/avaliacoes`) exige
  `Idempotency-Key`. A primeira resposta de sucesso fica guardada 24 h, por usuário e rota.
- Mesma chave e mesmo corpo: a API devolve a resposta guardada com `Idempotent-Replayed: true`, sem gravar de novo.
- Mesma chave e corpo diferente: 422. Mesma chave com a primeira ainda em andamento: 409.
- PUT (notas, chamada, nome) e a matrícula são idempotentes por natureza: repetir deixa o mesmo estado.
- O cache é o `IDistributedCache` em memória. Para mais de uma instância, troque por Redis sem mudar o filtro.

### Segurança

- O login confere a senha com bcrypt (`$2a$`, compatível com o `BCryptPasswordEncoder` do Spring) e só
  `StatusUsuario = Ativo` entra (outro status: 403). CPF ou senha errados: 401.
- A API emite o JWT no login e no cadastro, igual ao do Java: HS256, `sub` = `Usuario.cod`, `iss`, `iat`,
  `exp` (8 h). Valida assinatura, algoritmo e validade; o logout é só do lado do app (JWT stateless).
- A cada requisição, a API lê o `Usuario` do `sub`. Só `StatusUsuario = Ativo` passa; bloqueado recebe 401.
- Os papéis (diretor, professor, guardião, aluno) vêm do banco, nunca do app.
- Além do papel, cada rota confere o dono do recurso: diretor só da própria escola, professor só da própria turma.
- Senhas novas são gravadas com bcrypt. Nenhuma credencial fica no repositório.

## Rotas

Toda rota, menos `/auth/login`, `/auth/cadastro`, `/health`, `/health/live` e `/dev/token`, exige
`Authorization: Bearer <jwt>`.

| Método | Rota                                   | Quem                         | Resposta                |
| ------ | -------------------------------------- | ---------------------------- | ----------------------- |
| POST   | `/auth/login`                          | Anônimo                      | `Sessao` (401 CPF/senha, 403 não Ativo) |
| POST   | `/auth/cadastro`                       | Anônimo (cria responsável)   | 201 `Sessao` (409 CPF repetido) |
| PUT    | `/auth/senha`                          | Logado                       | 204 (senha atual errada: 400) |
| PUT    | `/auth/nome`                           | Diretor, responsável         | `Pessoa`                |
| POST   | `/auth/logout`                         | Logado                       | 204                     |
| GET    | `/guardioes/:cod/dependentes`          | O próprio responsável        | `Dependente[]`          |
| GET    | `/alunos/:cod/boletim`                 | Aluno, responsável, professor ou diretor de uma turma dele | `BoletimAluno` |
| GET    | `/tipos/presenca`, `/nota`, `/curso`   | Logado                       | `Enumerador[]`          |
| POST   | `/tipos/curso`                         | Diretor                      | 201 `Enumerador`        |
| GET    | `/professores`                         | Diretor                      | `ProfessorResumo[]`     |
| POST   | `/professores`                         | Diretor                      | 201 `{ professorCod }`  |
| PUT    | `/professores/:cod`                    | Diretor                      | 204                     |
| GET    | `/professores/:cod/turmas`             | O próprio professor          | `TurmaResumo[]`         |
| GET    | `/alunos`                              | Diretor                      | `AlunoResumo[]`         |
| POST   | `/alunos`                              | Diretor                      | 201 `{ alunoCod }`      |
| PUT    | `/alunos/:cod`                         | Diretor                      | 204                     |
| GET    | `/diretores/:cod/escolas`              | O próprio diretor            | `EscolaResumo[]`        |
| GET    | `/escolas/:cod`                        | Diretor da escola            | `EscolaResumo`          |
| GET    | `/escolas/:cod/turmas`                 | Diretor da escola            | `TurmaResumo[]`         |
| POST   | `/escolas/:cod/turmas`                 | Diretor da escola            | 201 `{ cod }`           |
| GET    | `/escolas/:cod/professores`            | Diretor da escola            | `ProfessorDaEscola[]`   |
| GET    | `/escolas/:cod/alunos`                 | Diretor da escola            | `AlunoDaEscola[]`       |
| POST   | `/turmas/:cod/alunos`                  | Diretor da escola da turma   | 204                     |
| GET    | `/turmas/:cod`                         | Professor, diretor, aluno, responsável | `TurmaDetalhe` |
| POST   | `/turmas/:cod/avaliacoes`              | Professor da turma           | 201 `{ cod }`           |
| GET    | `/avaliacoes/:cod`                     | Professor, diretor, aluno, responsável | `AvaliacaoDetalhe` |
| PUT    | `/avaliacoes/:cod`                     | Professor da turma           | 204                     |
| DELETE | `/avaliacoes/:cod`                     | Professor da turma           | 204                     |
| PUT    | `/avaliacoes/:cod/notas`               | Professor da turma           | 204                     |
| GET    | `/turmas/:cod/chamada?data=YYYY-MM-DD` | Professor, diretor, aluno, responsável | `ItemChamada[]` |
| PUT    | `/turmas/:cod/chamada?data=YYYY-MM-DD` | Professor da turma           | 204                     |

Fora do contrato desta API: `POST /alunos/:cod/boletim/analise` (análise com IA, só no Java).

Erro: ProblemDetails (RFC 9457). O app lê o campo `detail` e mostra na tela.

## Regras de negócio

- **Banco:** a API usa o DDL de `01_ddl.sql` sem mudar nada. O EF Core mapeia classe e propriedade para
  o nome do Oracle (`CodTipoCurso` → `COD_TIPO_CURSO`). Não há migrations.
- **Cadastro de responsável (`/auth/cadastro`):** Pessoa + Usuario Ativo + Guardiao numa transação e já
  devolve a sessão. CPF com 11 dígitos (como no Java); CPF repetido: 409.
- **Senha:** a nova segue a regra do cadastro e precisa ser diferente da atual; senha atual errada: 400.
- **Boletim:** notas e média por turma (mais recente primeiro), presenças (mais recente primeiro) e
  frequência de 0 a 1 em que só `Presente` conta; média e frequência são `null` sem registros.
- **Cadastro de professor:** Pessoa + Usuario Ativo + Professor numa transação. CPF repetido: 409.
- **Cadastro de aluno:** Pessoa + Aluno, e Usuario só com senha. Sem senha, o responsável acompanha por ele.
- **Validação:** nome e sobrenome até 150; CPF com dígitos verificadores; senha com 8+ caracteres,
  letras e números; os CHECKs do DDL (semestre 1 ou 2, ano a partir de 2000, nota de 0 a 100).
- **Curso:** nome único sem considerar acento e maiúscula (`historia` = `História`).
- **Avaliação:** nome único na turma (409). Excluir apaga as notas antes (FK `cod_avaliacao`).
- **Notas:** upsert por aluno e avaliação; nota `null` apaga; 2 casas (`NUMBER(5,2)`).
- **Chamada:** upsert por aluno e data; sem data futura (mesma regra do trigger `trg_valida_data_presenca`).
- **Matrícula:** aluno já matriculado é ignorado. Lista vazia: 400; aluno inexistente: 404.

## Testes

```bash
dotnet test
```

- **Unitários** (`tests/.../Unidade`): regras de validação e o mapeamento do EF Core para os nomes do Oracle.
- **Integração** (`tests/.../Integracao`): sobem a API inteira com `WebApplicationFactory` (JWT, rate limit,
  idempotência, endpoints). Só o Oracle é trocado por SQLite em memória, com os dados da `02_dml.sql`.
  Cada teste tem o próprio banco.

## Estrutura

```
src/BoletimPlus.Api/
  Program.cs     configuração: banco, JWT, rate limit, health, Swagger, rotas
  Endpoints/     rotas Minimal API por área (conta, tipos, pessoas, escolas, sala de aula, acompanhamento)
  Servicos/      regras de negócio e acesso por recurso
  Dominio/       regras puras (validação) e RegraException
  Dados/         entidades e DbContext mapeados para o DDL
  Contratos/     corpos de entrada e saída (mesmos tipos do app)
  Seguranca/     JWT e papéis
  Infra/         idempotência, rate limit, health checks, Swagger, tratador de erros
tests/BoletimPlus.Api.Tests/
  Unidade/       testes de regra
  Integracao/    testes de endpoint
```
