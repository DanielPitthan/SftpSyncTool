# CopyToSFTPObserver

## Descrição

CopyToSFTPObserver é um serviço Worker desenvolvido em .NET 9 para automação de transferência e processamento de arquivos entre pastas locais e servidores SFTP. O serviço executa rotinas de cópia, verificação, movimentação e exclusão de arquivos, com controle de sucesso/erro e registro detalhado de logs.

## Novas Funcionalidades

- **Mapeamento automático de tarefas via JSON**: O serviço lê o arquivo `apptasks.json` para configurar dinamicamente as ações e pastas a serem processadas.
- **Execução sequencial e controlada de tarefas**: Para cada pasta, executa as tarefas na ordem definida, interrompendo a sequência em caso de falha e movendo arquivos para a pasta de erro.
- **Notificação por e-mail**: Ao final do processamento, envia um e-mail com o log das ações realizadas, se configurado.
- **Validação robusta de configuração**: O serviço valida a estrutura e os dados do arquivo de configuração antes de iniciar o processamento, garantindo maior segurança e previsibilidade.
- **Logs detalhados e críticos**: Todas as etapas, erros e exceções são registrados para facilitar auditoria e troubleshooting.
- **Execução periódica configurável**: O intervalo entre execuções é definido nas configurações, permitindo flexibilidade operacional.

## Funcionamento do arquivo `apptasks.json`

O arquivo `apptasks.json` define toda a lógica de processamento do serviço. Sua estrutura básica é:

```json
{
  "Name": "Nome da rotina",
  "Version": "Versão",
  "FolderMaps": [
    {
      "Name": "Descrição da pasta",
      "FolderPath": "Caminho local da pasta",
      "SFTPPathDestination": "Destino SFTP",
      "ProcessedFilesOnError": "Caminho para arquivos com erro",
      "ProcessedFilesOnSuccess": "Caminho para arquivos processados",
      "EmailNotify": "E-mail para notificação",
      "TasksMaps": [
        {
          "Name": "Descrição da tarefa",
          "Order": 1,
          "Task": "Tipo e argumentos da tarefa"
        }
        // ... outras tarefas ...
      ]
    }
    // ... outras pastas ...
  ]
}
```

### Detalhamento dos campos
- **Name**: Nome da rotina de processamento.
- **Version**: Versão da configuração.
- **FolderMaps**: Lista de pastas a serem monitoradas/processadas.
  - **Name**: Descrição da pasta.
  - **FolderPath**: Caminho local da pasta a ser processada.
  - **SFTPPathDestination**: Caminho de destino no SFTP.
  - **ProcessedFilesOnError**: Pasta para onde os arquivos vão em caso de erro.
  - **ProcessedFilesOnSuccess**: Pasta para onde os arquivos vão após processamento com sucesso.
  - **EmailNotify**: E-mail para envio de notificações (opcional).
  - **TasksMaps**: Lista ordenada de tarefas a serem executadas na pasta.
    - **Name**: Descrição da tarefa (pode conter variáveis como @FolderPath, @SFTPPathDestination).
    - **Order**: Ordem de execução da tarefa.
    - **Task**: Tipo da tarefa e argumentos. Exemplos: `copy:@FolderPath:@SFTPPathDestination`, `check:@FolderPath:@SFTPPathDestination`, `move:@FolderPath:@ProcessedFilesOnSuccess`, `notify`.

### Exemplo de configuração

```json
{
  "Name": "Monitoramento de pastas VAN bancária - ITAÚ",
  "Version": "1.0",
  "FolderMaps": [
    {
      "Name": "Processando todos os arquivos da pasta \\iconnet\\out",
      "FolderPath": "C:\\Temp\\inConnet\\out",
      "SFTPPathDestination": "SFTP:/inConnect/Teste",
      "ProcessedFilesOnError": "C:\\Temp\\inConnet\\error",
      "ProcessedFilesOnSuccess": "C:\\Temp\\inConnet\\processed",
      "EmailNotify": "meuemail@meuemail.com",
      "TasksMaps": [
        {
          "Name": "Copinado arquivo para o destino | copy:@SFTPPathDestination",
          "Order": 1,
          "Task": "copy:@FolderPath:@SFTPPathDestination"
        },
        {
          "Name": "Verificando se foi copiado com sucesso | check:@SFTPPathDestination",
          "Order": 2,
          "Task": "check:@FolderPath:@SFTPPathDestination"
        },
        {
          "Name": "Movendo o arquivo para a pasta de processamento | move:@FolderPath:@ProcessedFilesOnSuccess",
          "Order": 3,
          "Task": "move:@FolderPath:@ProcessedFilesOnSuccess"
        },
        {
          "Name": "Enviando notificação | @EmailNotify",
          "Order": 4,
          "Task": "notify"
        }
      ]
    }
  ]
}
```

## Requisitos

- .NET 9
- Configuração de pastas e tarefas via arquivo `apptasks.json`

## Uso

Basta iniciar o serviço. O processamento será realizado automaticamente conforme as configurações definidas no arquivo `apptasks.json`.

## Observações
- O serviço valida o arquivo de configuração antes de iniciar o processamento. Erros de configuração impedem a execução.
- Todas as ações e erros são registrados em log para consulta posterior.
- O envio de e-mail é opcional e depende da configuração do campo `EmailNotify`.
