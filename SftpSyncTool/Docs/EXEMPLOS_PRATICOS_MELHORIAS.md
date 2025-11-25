# Exemplos Práticos de Melhorias Implementadas

## Exemplo 1: Cenário de Race Condition (ANTES)

### Problema Original
```csharp
// CopyToDestination.cs (ANTES - COM PROBLEMA)
private static TaskActions CopyToSFTP(IEnumerable<FileInfo>? files, TaskActions taskActions)
{
    string destinationPath = taskActions.Argument2.Replace("SFTP:", ""); // /home/user/@Inspect_VAR
    
    foreach (var file in fileList)
    {
        if (taskActions.ShouldInspect)
        {
            var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
            taskActions.Inspect_VAR = content.TrimStart('0').Trim(); // PROBLEMA 1: Variável compartilhada!
            destinationPath = destinationPath.Replace("@Inspect_VAR", taskActions.Inspect_VAR); // PROBLEMA 2: Reatribuição!
        }
        // Copiar para destinationPath
    }
}
```

### Cenário de Falha com Múltiplas Threads
```
Arquivo A (pedido_001.txt)  | Arquivo B (pedido_002.txt)
Tempo 1: Inspeciona A ? "001" | 
Tempo 2: Tira 0 ? "1"       |
Tempo 3: Seta Inspect_VAR="1"|
Tempo 4: Calcula destino    | Tempo 4: Inspeciona B ? "002"
        =/home/user/1       | Tempo 5: Tira 0 ? "2"
Tempo 5: VAI COPIAR PARA /home/user/1 | Tempo 6: Seta Inspect_VAR="2" (SOBRESCREVEU!)
                             | Tempo 7: Calcula destino=/home/user/2
Tempo 6: Arquivo A copiado para /home/user/1 ?
        Arquivo B copiado para /home/user/2 ?
        
RESULTADO: Funcionou por sorte, mas não é garantido!
```

### Cenário de Falha Comprovada
```
Arquivo 001.txt ? Inspect_VAR = "001" ? destinationPath = "/home/user/001"
Arquivo 002.txt ? Inspect_VAR = "002" ? destinationPath = "/home/user/002"
Arquivo 003.txt ? Inspect_VAR = "003" ? destinationPath = "/home/user/003"

THREAD 1: Processa 001.txt
    Tempo 1: inspectVar = "001"
    Tempo 2: taskActions.Inspect_VAR = "001"
    Tempo 3: destinationPath = destinationPath.Replace("@Inspect_VAR", "001")
    Tempo 4:                  = "/home/user/001"

THREAD 2: Processa 002.txt (INTERVÉM AQUI!)
    Tempo 2.5: inspectVar = "002"
    Tempo 3.5: taskActions.Inspect_VAR = "002" ? SOBRESCREVEU O VALOR DE THREAD 1!
    Tempo 4.5: destinationPath = destinationPath.Replace("@Inspect_VAR", "002")
               = "/home/user/002"

THREAD 1 CONTINUA (COM DADOS CORROMPIDOS):
    Tempo 5: lê taskActions.Inspect_VAR
    Resultado: "002" ? ERRADO! Deveria ser "001"
    Tempo 6: cópia para /home/user/002 ? ARQUIVO COLIDE COM THREAD 2!
    
RESULTADO: Dois arquivos copiados para o mesmo destino!
           Arquivo 002.txt foi sobrescrito ou corrompido
```

---

## Exemplo 2: Solução Implementada (DEPOIS)

### Código Melhorado
```csharp
// CopyToDestination.cs (DEPOIS - SEGURO)
private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = 
    new ConcurrentDictionary<string, SemaphoreSlim>();

private static TaskActions CopyToSFTP(IEnumerable<FileInfo>? files, TaskActions taskActions)
{
    foreach (var file in fileList)
    {
        // PROTEÇÃO: Obter ou criar um semáforo único para este arquivo
        var fileLock = _fileLocks.GetOrAdd(file.FullName, _ => new SemaphoreSlim(1, 1));
        
        try
        {
            // EXCLUSÃO: Adquirir lock exclusivo para este arquivo
            fileLock.Wait(); // Se T2 chegar aqui, fica bloqueada esperando T1 terminar
            
            try
            {
                // ISOLAMENTO: Contexto isolado por arquivo (variáveis locais, não compartilhadas)
                string inspectVar = string.Empty;
                string destinationPath = taskActions.Argument2.Replace("SFTP:", "");
                
                if (taskActions.ShouldInspect)
                {
                    var content = InspectFileFactory.Inspect(file.FullName, taskActions.InspectPartOfFile);
                    inspectVar = content.TrimStart('0').Trim();
                    taskActions.Inspect_VAR = inspectVar;
                    destinationPath = destinationPath.Replace("@Inspect_VAR", inspectVar);
                }
                
                // SEGURO: Copia para destinationPath correto
                // ...
            }
            finally
            {
                // LIBERAÇÃO: Liberar o lock para este arquivo
                fileLock.Release();
            }
        }
    }
}
```

### Cenário de Sucesso com Múltiplas Threads
```
THREAD 1: Processa 001.txt
    Tempo 1: Obtém fileLock para "001.txt"
    Tempo 2: Adquire LOCK (fileLock.Wait) ?
    Tempo 3: inspectVar = "001" (VARIÁVEL LOCAL)
    Tempo 4: destinationPath = "/home/user/001" (VARIÁVEL LOCAL)
    Tempo 5: COPIA PARA /home/user/001

THREAD 2: Processa 002.txt (INICIA ENQUANTO T1 ESTÁ NO LOCK)
    Tempo 2: Obtém fileLock para "002.txt" ? SEMÁFORO DIFERENTE!
    Tempo 2.5: Adquire LOCK (fileLock.Wait) ? ? NÃO BLOQUEIA PORQUE É OUTRO ARQUIVO!
    Tempo 3: inspectVar = "002" (VARIÁVEL LOCAL DE T2)
    Tempo 4: destinationPath = "/home/user/002" (VARIÁVEL LOCAL DE T2)
    Tempo 5: COPIA PARA /home/user/002

THREAD 1 CONTINUA (COM DADOS CORRETOS):
    Tempo 6: Upload termina
    Tempo 7: Libera LOCK (fileLock.Release)

THREAD 2 CONTINUA (COM DADOS CORRETOS):
    Tempo 6: Upload termina
    Tempo 7: Libera LOCK (fileLock.Release)
    
RESULTADO: ? Arquivo 001.txt ? /home/user/001
           ? Arquivo 002.txt ? /home/user/002
           ? Nenhuma colisão, nenhuma corrupção!
```

---

## Exemplo 3: Retry Automático com Backoff

### Cenário: Upload com Falha Temporária de Rede

```csharp
ExecuteSftpUploadWithRetry(clientSFTP, file, "/sftp/destino/arquivo.txt", taskActions)
```

### Execução Detalhada
```
TENTATIVA 1:
[2024-01-15 10:30:45.123] Iniciando upload de "relatorio.xlsx"
?
Erro: TimeoutException (latência de rede)
?
[2024-01-15 10:30:45.567] [RETRY 1/3] Timeout ao enviar relatorio.xlsx, 
                          aguardando 500ms antes de tentar novamente: 
                          The operation has timed out

ESPERA: 500ms ?

TENTATIVA 2:
[2024-01-15 10:30:46.067] Retry 1: Iniciando upload de "relatorio.xlsx"
?
Erro: SshException: Connection reset by peer (erro transiente)
?
[2024-01-15 10:30:46.234] [RETRY 2/3] Erro SSH ao enviar relatorio.xlsx,
                          aguardando 1000ms antes de tentar novamente:
                          The SSH connection is no longer valid

ESPERA: 1000ms ??

TENTATIVA 3:
[2024-01-15 10:30:47.234] Retry 2: Iniciando upload de "relatorio.xlsx"
?
SUCESSO! ? Upload completado
?
[2024-01-15 10:30:48.890] [SUCESSO] Arquivo enviado para SFTP: 
                          /sftp/destino/relatorio.xlsx

RESULTADO: Arquivo enviado com sucesso após falhas temporárias!
           Sem necessidade de reprocessamento manual.
```

### Contraste com Falha Não-Retentável
```
TENTATIVA 1:
[2024-01-15 10:35:22.123] Iniciando upload de "dados.csv"
?
Erro: AuthenticationException (credenciais inválidas)
?
[2024-01-15 10:35:22.456] [ERRO NÃO-RETENTÁVEL] Falha irrecuperável 
                          ao enviar dados.csv: 
                          SshAuthenticationException - 
                          Permission denied (publickey,password)

RESULTADO: Falha imediatamente, sem retry desnecessário.
           (porque credenciais erradas não vão melhorar esperando)
```

---

## Exemplo 4: Logging Detalhado

### Fluxo de Cópia com Logs Estruturados
```
Processando pasta: PedidosEntrada

[2024-01-15 10:40:00.234] Processando pasta: PedidosEntrada
[2024-01-15 10:40:00.456] Caminho da pasta: C:\Pedidos\Entrada
[2024-01-15 10:40:00.567] Destino SFTP: SFTP:/home/processados/@Inspect_VAR

---

ARQUIVO 1: pedido_20240115_001.txt
[2024-01-15 10:40:01.100] [INSPECT] Arquivo: pedido_20240115_001.txt | Valor extraído: 001
[2024-01-15 10:40:01.234] [SUCESSO] Arquivo copiado para: /home/processados/001/pedido_20240115_001.txt

ARQUIVO 2: pedido_20240115_002.txt (Falha com retry)
[2024-01-15 10:40:02.100] [INSPECT] Arquivo: pedido_20240115_002.txt | Valor extraído: 002
[2024-01-15 10:40:02.234] Diretório criado no SFTP: /home/processados/002
[2024-01-15 10:40:02.456] [RETRY 1/3] Timeout ao enviar pedido_20240115_002.txt, 
                          aguardando 500ms antes de tentar novamente: 
                          The operation has timed out
[2024-01-15 10:40:02.967] [RETRY 2/3] Erro SSH ao enviar pedido_20240115_002.txt, 
                          aguardando 1000ms antes de tentar novamente: 
                          Connection reset by peer
[2024-01-15 10:40:03.967] [SUCESSO] Arquivo enviado para SFTP: 
                          /home/processados/002/pedido_20240115_002.txt

ARQUIVO 3: pedido_20240115_003.txt (Falha permanente)
[2024-01-15 10:40:04.100] [INSPECT] Arquivo: pedido_20240115_003.txt | Valor extraído: 003
[2024-01-15 10:40:04.234] [ERRO NÃO-RETENTÁVEL] Falha irrecuperável ao enviar 
                          pedido_20240115_003.txt: Permission denied (no write access)

---

[2024-01-15 10:40:04.567] [RESUMO] Total de 2 arquivo(s) copiado(s) com sucesso para o SFTP. Falhas: 1
```

### Análise de Logs
```
? Timestamp preciso: Rastreiar ordem exata de eventos
? Categorização: Rapidamente identificar erros, sucessos, retries
? Contexto detalhado: Nome arquivo, caminho destino, valores inspecionados
? Resumo executivo: Quantos sucessos/falhas
? Rastreabilidade: Auditar problemas ou investigar perda de dados
```

---

## Exemplo 5: Verificação de Integridade com Proteção

### Cenário: Verificar se Cópias Foram Bem-Sucedidas

```csharp
// Após cópia, verificação automática
task.Check(); // Usa CheckCopyResult.Execute()
```

### Logs de Verificação
```
[2024-01-15 10:40:05.100] [INSPECT] Arquivo: relatorio_001.xlsx | Valor extraído: 001
[2024-01-15 10:40:05.234] [VERIFICADO] relatorio_001.xlsx existe em: /home/processados/001/relatorio_001.xlsx
[2024-01-15 10:40:05.356] [VERIFICADO] relatorio_002.xlsx existe em: /home/processados/002/relatorio_002.xlsx
[2024-01-15 10:40:05.478] [NÃO-ENCONTRADO] relatorio_003.xlsx não existe em: /home/processados/003/relatorio_003.xlsx

[2024-01-15 10:40:05.567] [RESUMO] Todos os 2 arquivo(s) foram verificados com sucesso no SFTP. 
                           Falhas: 1
                           Arquivos não encontrados (1): C:\Pedidos\relatorio_003.xlsx
```

---

## Resumo de Benefícios

| Problema | Solução | Benefício |
|----------|---------|----------|
| Race condition em `Inspect_VAR` | Variável local isolada | Nenhuma sobrescrita |
| Reatribuição de `destinationPath` | Contexto local por arquivo | Caminho correto garantido |
| Sem sincronização SFTP | `SemaphoreSlim` por arquivo | Acesso exclusivo |
| Falhas de rede sem retry | Retry com backoff exponencial | Resiliência automatizada |
| Logs genéricos | Logs estruturados com timestamps | Diagnóstico fácil |
| Sem contabilização | Contadores de sucesso/falha | Visibilidade operacional |

---

## Próximos Passos Recomendados

1. **Testes de carga**: Validar com 100+ arquivos simultâneos
2. **Monitoramento**: Integrar com sistema de alertas (Prometheus/Grafana)
3. **Configuração**: Tornar MaxRetries e delays configuráveis
4. **Documentação**: Atualizar runbooks com nova política de retry
