# VR Player - Sistema de Vídeo 360°

## Estrutura da Cena
```
📂 VR player*
  └─ 🔦 Directional Light
  └─ 📱 XRRig
     └─ 📷 Camera Offset
        └─ 🎥 Main Camera
  └─ 🎥 VideoSphere
  └─ 🎭 FadeSphere
  └─ 📺 VRCanvas
     └─ 📝 MessageText
     └─ 📝 Text (TMP)
     └─ 📝 DebugText
  └─ ⚙️ EventSystem
  └─ ⚙️ [MANAGER]
```

## Componentes Principais

### XRRig
- Sistema de câmera VR do Unity XR
- Contém o Camera Offset e Main Camera
- Responsável pelo tracking e movimento no VR

### VideoSphere
- Esfera onde o vídeo 360° é projetado
- Recebe a textura do vídeo via VideoPlayer
- Pode ter sua rotação controlada para efeitos de bloqueio de visualização

### FadeSphere
- Esfera usada para efeitos de fade in/out
- Controla transições suaves entre vídeos
- Ajuda a evitar desconforto em VR

### VRCanvas (World Space)
- Interface do usuário no espaço 3D
- Contém elementos de texto para feedback
- Inclui sistema de debug visual

### [MANAGER]
- Controlador principal do sistema
- Gerencia reprodução de vídeos
- Controla restrições de rotação
- Gerencia conexão com servidor
- Implementa sistema de debug

## Sistema de Debug

O sistema inclui uma janela de debug no editor Unity (Window > VR > Rotation Debug) que permite:
- Habilitar/desabilitar visualização de debug
- Controlar bloqueio de rotação
- Ajustar limites de ângulos vertical/horizontal
- Monitorar rotação da câmera e VideoSphere
- Visualizar diferença relativa entre câmera e VideoSphere

## Controles no Editor
- Setas: Rotação do VideoSphere
- Espaço: Reset da rotação
- F: Ativar/desativar foco manual
- Rotação pode ser bloqueada via interface de debug

## Notas Importantes
1. No editor, a rotação é aplicada ao VideoSphere
2. No Quest/VR, o sistema detecta automaticamente a câmera correta
3. O Canvas está em modo World Space para melhor visualização em VR
4. Sistema de bloqueio pode ser configurado por intervalos de tempo em cada vídeo

# VR Video Player com Controle de Rotação

Aplicativo de visualização de vídeos em VR com controle de rotação, pontos focais automáticos e interface de administração via WebSocket.

## Características Principais

- Reprodução de vídeos 360° em VR
- Sistema de controle de rotação com pontos focais
- Transições suaves entre ângulos de visualização
- Interface de administração via WebSocket
- Modo offline para demonstrações
- Reprodução local dos vídeos (não requer streaming)
- Sistema de bloqueio de visualização configurável
- Suporte para múltiplos vídeos

## Requisitos

- Unity 2022.3 ou superior
- Oculus Integration Package
- Android Build Support (para builds Quest)
- Meta Quest 1 ou 2

## Estrutura do Projeto

```
Assets/
├── Scripts/
│   ├── VRManager.cs           # Gerenciador principal do sistema VR
│   └── SmoothOrbitFollower.cs # Sistema de órbita suave para objetos
├── Scenes/
│   └── Main.unity            # Cena principal do aplicativo
└── StreamingAssets/         # Pasta alternativa para vídeos (opcional)
```

## Configuração de Vídeos

Os vídeos são reproduzidos diretamente do dispositivo, não necessitando de streaming ou múltiplos builds:

1. **Formato Recomendado**:
   ```bash
   ffmpeg -y -hwaccel cuda -hwaccel_output_format cuda -i "video_original.mp4" \
   -c:v hevc_nvenc -preset p1 -tune hq -rc:v vbr_hq \
   -b:v 12M -maxrate 15M -bufsize 20M -spatial-aq 1 \
   -vf "scale_cuda=3072:1536" -c:a aac -b:a 128k -ac 2 "video_convertido.mp4"
   ```

2. **Localização dos Vídeos**:
   - Pasta `Download` do Quest (recomendado)
   - StreamingAssets do aplicativo (alternativa)
   - Configurável via `useExternalStorage` no VRManager

## Comunicação WebSocket

### Conexão
- URL padrão: `ws://192.168.1.30:8181`
- Configurável via `serverUri` no VRManager
- Reconexão automática em caso de falha

### Comandos Disponíveis

| Comando | Formato | Descrição | Exemplo |
|---------|---------|-----------|---------|
| Play | `play:filename.mp4` | Inicia reprodução de vídeo | `play:rio.mp4` |
| Pause | `pause` | Pausa o vídeo atual | `pause` |
| Resume | `resume` | Retoma reprodução | `resume` |
| Stop | `stop` | Para a reprodução | `stop` |
| Seek | `seek:seconds` | Pula para tempo específico | `seek:120` |
| Mensagem | `aviso:texto` | Exibe mensagem na interface | `aviso:Iniciando tour` |

### Respostas do Cliente

| Mensagem | Formato | Descrição |
|----------|---------|-----------|
| Timecode | `TIMECODE:seconds` | Tempo atual do vídeo |
| Status | `STATUS:state` | Estado atual do player |
| Info | `CLIENT_INFO:data` | Informações do dispositivo |

## Sistema de Bloqueio de Visualização

O sistema permite definir momentos específicos onde a visualização é restrita:

```csharp
public class LockTimeRange {
    public float startTime;    // Tempo inicial em segundos
    public float endTime;      // Tempo final em segundos
    public float maxAngle;     // Ângulo máximo de rotação permitido
    public float resetSpeed;   // Velocidade de retorno ao centro
}
```

### Configuração via Unity Editor
1. Selecione o VRManager na cena
2. Expanda "Lock Time Ranges"
3. Configure os intervalos para cada vídeo
4. Ajuste ângulos e velocidades conforme necessário

## Modo Offline

O aplicativo pode funcionar sem conexão ao servidor:

1. **Ativação**:
   - Automática após falhas de conexão
   - Manual via `offlineMode = true`
   - Após timeout configurável

2. **Funcionalidades**:
   - Carregamento automático do primeiro vídeo
   - Interface de diagnóstico
   - Manutenção de todas funcionalidades locais

## Features Adicionais

1. **Sistema de Diagnóstico**:
   - Logs detalhados
   - Interface de debug in-game
   - Monitoramento de conexão

2. **Gerenciamento de Memória**:
   - Liberação automática de recursos
   - Controle de carregamento de vídeos
   - Otimização para VR

3. **Transições Suaves**:
   - Fade in/out entre vídeos
   - Interpolação suave de rotação
   - Retorno suave ao ponto focal

4. **Permissões Android**:
   - Solicitação automática de acesso ao armazenamento
   - Fallback para StreamingAssets
   - Tratamento de erros de permissão

## Solução de Problemas

1. **Vídeos não carregam**:
   - Verifique o formato do vídeo
   - Confirme as permissões de armazenamento
   - Verifique o caminho configurado

2. **Problemas de Conexão**:
   - Verifique o endereço do servidor
   - Confirme a rede local
   - Verifique logs de diagnóstico

3. **Problemas de Rotação**:
   - Verifique configurações de bloqueio
   - Ajuste valores de maxAngle e resetSpeed
   - Confirme orientação inicial do vídeo

## Licença

Todos os direitos reservados.

# Atualização 18/03/2025

## Novas Funcionalidades
- Implementação completa do cliente WebSocket usando SocketIOUnity
- Sistema de diagnóstico em tempo real com informações de conexão
- Suporte para reprodução de vídeo de múltiplas fontes
- Sistema de reconexão automática

## Como Atualizar

### Opção 1: Download Direto
1. Faça backup do seu projeto atual
2. Baixe a versão mais recente em: https://github.com/felipebrito/socket-client
3. Substitua os arquivos existentes pelos novos

### Opção 2: Atualização via Git
```bash
git pull origin main
```

## Configuração do Servidor

O sistema agora suporta comunicação com servidor Socket.IO. Configure o endereço em:
1. Selecione o objeto [MANAGER] na cena
2. Localize o componente `VRManager` 
3. Configure a propriedade `Server Uri` (ex: `ws://192.168.1.30:8181`)

## Instruções Detalhadas

Consulte os arquivos de documentação em `Assets/Scripts/Docs/` para informações detalhadas sobre:
- Configuração de bloqueio de rotação (`GuiaDeBloqueio.md`)
- Registro completo de alterações (`Alteracoes.md`)

## Atualização 24/03/2025 - Correção do Erro de Referência do Newtonsoft.Json

Foi implementada uma solução para o erro de referência do Newtonsoft.Json reportado anteriormente:

```
The type or namespace name 'Newtonsoft' could not be found (are you missing a using directive or an assembly reference?)
```

### Soluções implementadas:

1. Adicionado o pacote oficial do Newtonsoft.Json via Package Manager (versão 3.2.1)
2. Criados Assembly Definition Files para garantir as referências corretas:
   - `NewtonsoftJsonAssembly.asmdef` - Referência principal para Newtonsoft.Json
   - `VRPlayerAssembly.asmdef` - Para scripts principais do projeto
   - `SocketIOUnityAssembly.asmdef` - Para o plugin SocketIOUnity
   - `SocketIONewtonsoftJsonAssembly.asmdef` - Para a integração específica com Newtonsoft

3. Implementado `link.xml` para evitar stripping de código durante build IL2CPP
4. Criada cena de teste `NewtonsoftTest` para verificar a funcionalidade

### Instruções de uso:

Se você ainda encontrar problemas com o Newtonsoft.Json, consulte o documento de instalação detalhado em `Assets/Scripts/Docs/InstalacaoNewtonsoft.md`.

# Controle de Rotação para Vídeos 360°

Este sistema permite controlar a rotação da câmera em momentos específicos durante a reprodução de vídeos 360°. O controle é feito através do componente `VideoRotationControl`, que gerencia blocos de tempo onde a rotação da câmera é limitada.

## Componentes Principais

### VideoRotationControl
- Gerencia os blocos de tempo e o controle de rotação
- Configura os limites de rotação da câmera
- Monitora o tempo do vídeo e aplica restrições
- Interface amigável no Inspector para configuração

### VRManager (Camada de Compatibilidade)
- Mantém compatibilidade com scripts existentes
- Delega o controle de rotação para o VideoRotationControl
- Gerencia o estado de reprodução do vídeo
- Fornece interface de debug e diagnóstico

### CameraRotationLimiter
- Aplica os limites de rotação na câmera
- Controla a velocidade de retorno ao centro
- Gerencia ângulos máximos de rotação

### VideoPlayer
- Reproduz os vídeos 360°
- Fornece eventos de controle de reprodução
- Gerencia o estado de reprodução

## Configuração

### 1. Configurando Blocos de Tempo

1. Selecione o objeto que contém o componente `VideoRotationControl` no Inspector
2. Na seção "Configuração de Vídeos", você verá a lista de blocos de vídeo
3. Para cada vídeo que precisa de controle de rotação:
   - Clique em "Adicionar Novo Vídeo"
   - Digite o nome do arquivo de vídeo (ex: "rio.mp4")
   - Defina o ângulo máximo de rotação (padrão: 75°)
   - Adicione os blocos de tempo clicando em "Adicionar Bloco de Tempo"
   - Para cada bloco, defina:
     - Tempo inicial (formato: mm:ss)
     - Tempo final (formato: mm:ss)

### 2. Configurando o VRManager

1. Adicione o componente `VRManager` ao objeto que gerencia o sistema VR
2. Configure as referências necessárias:
   - Main Camera: câmera principal do VR
   - Video Sphere: objeto onde o vídeo é projetado
   - Video Player: componente que reproduz o vídeo
3. Ajuste as configurações de debug se necessário:
   - Enable Rotation Debug: ativa visualização de debug
   - Is Rotation Locked: força bloqueio de rotação
   - Max Vertical/Horizontal Angle: ângulos máximos

### 3. Exemplo de Configuração

Para o vídeo "rio.mp4", os seguintes blocos de tempo são configurados:
- 00:00 até 00:05 (primeiros 5 segundos)
- 00:20 até 00:44 (entre 20s e 44s)
- 02:39 até 03:48 (entre 159s e 228s)

## Funcionamento

1. Quando um vídeo começa a ser reproduzido:
   - O VRManager atualiza o estado de reprodução
   - O VideoRotationControl verifica se há configurações para o vídeo
   - O sistema de bloqueio é ativado se necessário

2. Durante a reprodução:
   - O VideoRotationControl monitora o tempo atual
   - Quando entra em um bloco de tempo:
     - A rotação da câmera é limitada ao ângulo definido
     - Uma mensagem é exibida na tela
   - Ao sair do bloco, a rotação volta ao normal

3. Sistema de Debug:
   - Interface visual para monitoramento
   - Logs detalhados no console
   - Ferramentas de teste no Inspector

## Ferramentas de Teste

O editor inclui ferramentas para testar a configuração:
1. Expanda a seção "Ferramentas de Teste" no Inspector do VideoRotationControl
2. Digite o nome do vídeo que deseja testar
3. Use os botões "Testar Vídeo" e "Parar Teste" para verificar o funcionamento

## Logs e Depuração

O sistema gera logs detalhados para ajudar na depuração:
- ✅ Quando uma configuração é carregada
- 🔒 Quando um bloqueio é ativado
- 🔓 Quando um bloqueio é desativado
- ❌ Quando há problemas ou erros
- ⏱️ Status atual do vídeo (a cada segundo)

## Notas Importantes

1. Os blocos de tempo são ordenados automaticamente por tempo inicial
2. O nome do arquivo de vídeo deve corresponder exatamente ao arquivo em StreamingAssets
3. Certifique-se de que todos os componentes necessários estão presentes na cena
4. O ângulo de rotação é aplicado igualmente para todos os blocos de tempo de um mesmo vídeo
5. A camada de compatibilidade (VRManager) é temporária e será removida em versões futuras