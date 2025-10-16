# Sistema de Notificação por E-mail

## Visão Geral

O sistema de notificação por e-mail foi refatorado para separar as responsabilidades de execução de tarefas e formatação de e-mails. Agora utiliza um padrão Builder para gerar e-mails HTML estilizados e responsivos.

## Componentes

### 1. TaskExecutionReport
**Localização:** `Infrastructure/Email/TaskExecutionReport.cs`

Modelo que armazena os dados do relatório de execução:

```csharp
public class TaskExecutionReport
{
    public string FolderName { get; set; }
    public DateTime ExecutionDate { get; set; }
    public List<string> FilesProcessed { get; set; }
    public List<string> FilesWithError { get; set; }
    public List<TaskExecutionDetail> ExecutionDetails { get; set; }
    public bool HasProcessedFiles { get; }
}

public class TaskExecutionDetail
{
    public string TaskType { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### 2. EmailNotificationBuilder
**Localização:** `Infrastructure/Email/EmailNotificationBuilder.cs`

Responsável por construir o HTML do e-mail de forma estruturada e estilizada:

```csharp
var report = new TaskExecutionReport { ... };
var builder = new EmailNotificationBuilder(report);
string htmlBody = builder.BuildHtmlBody();
```

## Características do E-mail Gerado

### Design Responsivo
- Layout adaptável para diferentes tamanhos de tela
- Largura máxima de 600px para melhor visualização
- Estilos inline para compatibilidade com clientes de e-mail

### Estrutura Visual

1. **Cabeçalho Colorido** (Gradiente roxo)
   - Nome da tarefa
   - Nome da pasta processada
   - Data e hora da execução

2. **Seção de Arquivos Processados** (Verde)
   - Tabela listando todos os arquivos processados com sucesso
   - Numeração automática
   - Contador total de arquivos

3. **Seção de Arquivos com Erro** (Vermelho)
   - Tabela listando arquivos que falharam na verificação
   - Destaque visual para erros
   - Contador total de erros

4. **Detalhes de Execução**
   - Timeline com cada etapa do processamento
   - Ícones de sucesso (✓) ou falha (✗)
   - Timestamp de cada operação
   - Mensagens detalhadas de cada etapa

5. **Rodapé**
   - Aviso de e-mail automático
   - Identificação do sistema

### Paleta de Cores

- **Primária:** `#667eea` (Azul/Roxo claro)
- **Secundária:** `#764ba2` (Roxo escuro)
- **Sucesso:** `#28a745` (Verde)
- **Erro:** `#dc3545` (Vermelho)
- **Neutro:** `#6c757d` (Cinza)

## Uso no Worker

O método `ExecuteTasks` foi refatorado para:

1. Criar um objeto `TaskExecutionReport` no início
2. Preencher o relatório durante a execução das tarefas
3. Usar o `EmailNotificationBuilder` para gerar o HTML
4. Enviar o e-mail formatado

```csharp
private async Task ExecuteTasks(List<TaskActions> tasksActions, FolderMap folderMap, CancellationToken stoppingToken)
{
    var report = new TaskExecutionReport
    {
        FolderName = folderMap.Name ?? "N/A",
        ExecutionDate = DateTime.Now
    };

    // Durante a execução, adiciona dados ao relatório
    report.FilesProcessed.AddRange(result.FilesProcessed);
    report.ExecutionDetails.Add(new TaskExecutionDetail { ... });

    // Ao notificar, usa o builder
    if (task.Action == TypeOfTasks.notify && report.HasProcessedFiles)
    {
        var emailBuilder = new EmailNotificationBuilder(report);
        string emailHtml = emailBuilder.BuildHtmlBody();
        
        Email email = new Email(
            folderMap.EmailNotify,
            $"Notificação de tarefa concluída: {folderMap.Name}",
            emailHtml
        );
        email.Send();
    }
}
```

## Vantagens da Refatoração

1. **Separação de Responsabilidades**
   - Worker foca na execução de tarefas
   - Builder foca na formatação do e-mail

2. **Manutenibilidade**
   - Fácil modificar o layout do e-mail sem tocar na lógica de negócio
   - Código mais limpo e legível

3. **Testabilidade**
   - Componentes podem ser testados independentemente
   - Builder pode ser testado com diferentes relatórios

4. **Reutilização**
   - EmailNotificationBuilder pode ser usado em outros contextos
   - TaskExecutionReport é um modelo reutilizável

5. **Profissionalismo**
   - E-mails com design moderno e profissional
   - Melhor experiência para o usuário final

## Tamanho do HTML

O builder gera HTML compacto usando:
- Estilos inline (obrigatório para compatibilidade)
- Sem espaços desnecessários
- Estrutura otimizada
- Tamanho típico: ~3-5 KB para relatórios médios

## Compatibilidade

O HTML gerado é compatível com os principais clientes de e-mail:
- Outlook (Desktop e Web)
- Gmail
- Apple Mail
- Thunderbird
- Clientes mobile (iOS Mail, Gmail App, etc.)
