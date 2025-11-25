# SftpSyncTool

## Visão geral

`CopyToSFTPObserver` é um Worker Service em .NET 9 que automatiza transferência e processamento de arquivos entre pastas locais e destinos SFTP. O serviço lê um arquivo de mapeamento (`apptasks.json`) que descreve pastas e uma sequência ordenada de tarefas a serem executadas (copiar, checar, mover, deletar, inspecionar e notificar). Possui logger customizado (arquivo), envio de e-mail e validação robusta da configuração.

---

## Principais responsabilidades

- Ler e validar `apptasks.json`.
- Para cada `FolderMap`, gerar e executar uma sequência ordenada de `TasksMaps`.
- Copiar arquivos para SFTP, verificar presença no SFTP, mover/excluir arquivos localmente e notificar por e-mail.
- Registrar logs em arquivo com rotação básica.

---

## Arquitetura e classes principais

Os nomes de classes e caminhos abaixo seguem a estrutura da solução.

- `Program` (`Program.cs`)
  - Inicializa o host, configura `AppSettings` via `builder.Configuration`, injeta serviços e registra o `Worker`.
  - Configura logger customizado (`AddFileLogger`) e console (`AddCustomConsole`).

- `Worker` (`Worker.cs`) — herda `BackgroundService`
  - Ciclo de vida: `StartAsync`, `StopAsync` (logs).
  - `ExecuteAsync`: loop principal que obtém `AppTask` via `AppTaskMapperConfigurator.MapAppTask()` e chama `ProcessFolders(...)` repetidamente segundo `IntervaloEntreExecucoes`.
  - Métodos-chave:
    - `ProcessFolders(IEnumerable<FolderMap>, CancellationToken)`
    - `ProcessSingleFolder(FolderMap, CancellationToken)` — gera `TaskActions` a partir de `TasksMaps`.
    - `ExecuteTasks(List<TaskActions>, FolderMap, CancellationToken)` — executa ações em ordem, trata falhas e envia notificações.
    - `HandleTaskFailure(TaskActions, FolderMap)` — move arquivos para pasta de erro e registra falhas.
    - `ProximaExecucao(CancellationToken)` — delay entre execuções.

- `AppTaskMapperConfigurator` (`Infrastructure/AppTaskMapperConfigurator.cs`)
  - `MapAppTask()` — lê `apptasks.json`, valida a estrutura JSON, deserializa para `AppTask` e valida conteúdo.
  - Realiza validações de presença de `Name`, `Version`, `FolderMaps` e valida cada `FolderMap`.

- Modelos (`Models`):
  - `AppTask` (`Models/MappingTasks/AppTask.cs`) — `Name`, `Version`, `IEnumerable<FolderMap> FolderMaps`.
  - `FolderMap` (`Models/MappingTasks/FolderMap.cs`) — `Name`, `FolderPathOrigin`, `SFTPPathDestination`, `ProcessedFilesOnError`, `ProcessedFilesOnSuccess`, `EmailNotify`, `InspectLocation`, `IEnumerable<TasksMap> TasksMaps`. Método: `GetFiles()`.
  - `TasksMap` (`Models/MappingTasks/TaskMap.cs`) — `Name`, `Order`, `Task`.
  - `TaskActions` (`Models/MappingTasks/TaskActions.cs`) — representa ação pronta: `Action` (`TypeOfTasks`), `Argument1..Argument5`, `ExecuteCopy`/`Check`/`Move` (implementadas no modelo/fábricas).
  - `TypeOfTasks` (`Models/Enums/TypeOfTasks.cs`) — valores: `copy`, `check`, `move`, `delete`, `notify`, `inspect`.

- Fábricas e infraestrutura:
  - `TaskActionFactory` (`Infrastructure/Factorys/TaskActionFactory.cs`) — `CreateTaskAction(string task, FolderMap folderMap, string taskName)` interpreta a string `Task` e cria `TaskActions`, resolvendo variáveis.
  - `DeleteFileFactory` / demais fábricas para operações locais.
  - `Email` (`Infrastructure/Email/Email.cs`) — construtor e método `Send()` que usa `EmailCredentials` estático com `SmtpClient`.
  - `SFTPCredentials`, `EmailCredentials`, `AppSettings` (`Models/Configurations`) — modelos de configurações e credenciais. `AppSettings` possui lógica para decodificar valores base64 (SFTP e senha de e-mail).

- Logger customizado (`Services/CustomLogger`):
  - `FileLogger`, `FileLoggerProvider`, `FileLoggerConfiguration` — grava logs em arquivo (`processlog_yyyyMMdd_HHmmss.log`), rotação por tamanho e diretório configurável.

---

## Formato e exemplos de configuração

1) `appsettings.json` (valores podem ser fornecidos via variáveis de ambiente ou outros providers). O `Program` faz `builder.Configuration.Bind(appSettings)`; as chaves devem corresponder às propriedades de `AppSettings`.

Exemplo mínimo (valores sensíveis podem ser Base64):

```json
{
  "SFTPUrl": "sftp://exemplo.com",
  "UsuarioSFTP": "usuario",
  "Senha": "senha_ou_base64",
  "EMAIL_FROM": "seu@dominio.com",
  "EMAIL_SENHA": "senha_email_ou_base64",
  "EMAIL_PORT": 587,
  "EMAIL_HOST": "smtp.exemplo.com",
  "IntervaloEntreExecucoes": 60000,
  "LogFile": "logs",
  "Port": 22
}
```

- `AppSettings` tentará decodificar `SFTPUrl`, `UsuarioSFTP`, `Senha` e `EMAIL_SENHA` como base64; se falhar, usa o valor original.
- `IntervaloEntreExecucoes` em milissegundos.
- `LogFile` é o diretório relativo para logs (padrão `copyToSFTPObserverLogger`).

2) `apptasks.json` (arquivo obrigatório na pasta de execução do serviço). Exemplo atualizado para propriedades reais:

```json
{
  "Name": "Monitoramento de pastas - Exemplo",
  "Version": "1.0",
  "FolderMaps": [
    {
      "Name": "Processando pasta de entrada",
      "FolderPathOrigin": "C:\\Temp\\inConnect\\out",
      "SFTPPathDestination": "SFTP:/inConnect/Teste",
      "ProcessedFilesOnError": "C:\\Temp\\inConnect\\error",
      "ProcessedFilesOnSuccess": "C:\\Temp\\inConnect\\processed",
      "EmailNotify": "ops@exemplo.com",
      "InspectLocation": "",
      "TasksMaps": [
        {
          "Name": "Copiando para SFTP | copy:@SFTPPathDestination",
          "Order": 1,
          "Task": "copy:@FolderPathOrigin:@SFTPPathDestination"
        },
        {
          "Name": "Verificando no SFTP | check:@SFTPPathDestination",
          "Order": 2,
          "Task": "check:@FolderPathOrigin:@SFTPPathDestination"
        },
        {
          "Name": "Movendo para processed | move:@FolderPathOrigin:@ProcessedFilesOnSuccess",
          "Order": 3,
          "Task": "move:@FolderPathOrigin:@ProcessedFilesOnSuccess"
        },
        {
          "Name": "Notificar | notify",
          "Order": 4,
          "Task": "notify"
        }
      ]
    }
  ]
}
```

- Nas strings `Task` use `:` para separar tipo e argumentos; a fábrica suporta até 5 argumentos.
- Em `TasksMaps.Name` pode-se usar `@Variavel` para gerar textos dinâmicos (resolvidos via `ExtractVariable`/`GetValue`).

---

## Execução local (desenvolvimento)

- Abra a solução no Visual Studio ou use CLI:
  - Build: `dotnet build`
  - Run: `dotnet run --project .\SftpSyncTool\SftpSyncTool.csproj`

- Coloque `apptasks.json` na pasta de execução (`bin\Debug\net9.0` ou pasta de publicação).
- Configure `appsettings.json` ou variáveis de ambiente conforme exemplo.

---

## Publicação e instalação

Opção A — Executável (Windows):
1. `dotnet publish -c Release -r win-x64` ou usar Publish no Visual Studio.
2. Copie a pasta publicada para o servidor.
3. Registre como serviço Windows (ex.: `sc create CopyToSFTPObserver binPath= "C:\\Caminho\\CopyToSFTPObserver.exe" start= auto`).

Opção B — Container:
- `dotnet publish -c Release -r linux-x64` e crie imagem Docker; monte `apptasks.json` e `appsettings.json` via volumes.

---

## Logs e troubleshooting

- Logs em arquivos gerenciados por `FileLoggerProvider` no diretório de `AppSettings.LogFile`.
- Em caso de erro:
  - `apptasks.json` não encontrado: verifique se o arquivo foi copiado para a pasta do executável.
  - Erros de SFTP: verifique `SFTPUrl`, `UsuarioSFTP`, `Senha` e `Port` em `appsettings.json`.
  - Erros de e-mail: verifique `EMAIL_HOST`, `EMAIL_PORT`, `EMAIL_FROM` e `EMAIL_SENHA`.
- `AppTaskMapperConfigurator` valida o JSON e não permite execução se o arquivo estiver inválido.

---

## Segurança

- Valores sensíveis podem ser codificados em base64; `AppSettings` tentará decodificá-los automaticamente.
- Em produção, prefira armazenar segredos em provedores seguros (Azure Key Vault, AWS Secrets Manager, variáveis de ambiente ou User Secrets em desenvolvimento).

---

## Dicas de desenvolvimento

- Coloque breakpoints em `Worker.ExecuteAsync`, `AppTaskMapperConfigurator.MapAppTask()` e `TaskActionFactory.CreateTaskAction()` para investigar fluxo e resolução de variáveis.
- Valide `apptasks.json` com um linter JSON antes de executar o serviço.

---

## Estrutura resumida do projeto

- `Program.cs` — bootstrap e DI.
- `Worker.cs` — execução das tarefas.
- `Infrastructure/` — `AppTaskMapperConfigurator`, fábricas, `Email`.
- `Models/` — `AppSettings`, `AppTask`, `FolderMap`, `TasksMap`, `TaskActions`, enums.
- `Services/CustomLogger` — logger de arquivo.

---

## Contribuição

- Rode localmente e valide logs antes de abrir PR.
- Siga padrões existentes para novos tipos de tarefas e fábricas.

---
