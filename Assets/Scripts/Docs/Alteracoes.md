# Registro de Alterações

## Última Atualização - 18/03/2025

### Novas Funcionalidades
- Implementação completa do cliente WebSocket usando SocketIOUnity
- Sistema de diagnóstico em tempo real com informações de conexão
- Monitor de rotação visual para depuração
- Suporte para reprodução de vídeo a partir de armazenamento externo ou StreamingAssets
- Sistema de reconexão automática com o servidor

### Melhorias
- Otimização do sistema de bloqueio de rotação
- Transições mais suaves entre vídeos com fade in/out
- Compatibilidade melhorada com Meta Quest 2/3
- Feedback visual aprimorado para estados de conexão
- Sistema de filas para mensagens recebidas

### Correções
- Corrigido problema de memória com múltiplos vídeos
- Ajustado carregamento de vídeos para evitar travamentos
- Corrigido cálculo incorreto dos ângulos de rotação
- Resolvido problema de desconexão frequente
- Corrigida orientação do vídeo em determinados dispositivos

### Reestruturação
- Organização do projeto conforme padrões Unity
- Separação clara entre lógica de negócios e interface
- Documentação aprimorada com exemplos de uso
- Scripts modularizados para melhor manutenção

## Como Atualizar

Para atualizar sua versão atual:

1. Faça backup do seu projeto
2. Limpe todos os arquivos obsoletos
3. Importe a nova versão através do repositório GitHub:
   ```
   git clone https://github.com/felipebrito/socket-client.git
   ```
4. Configure a URL do servidor WebSocket no VRManager 