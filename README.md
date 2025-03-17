# VR Player - Sistema de Vídeo 360°

## Atualizações Recentes

### Março 2024

**1. Reorganização da Estrutura do Projeto**
- Corrigida a estrutura do projeto para seguir os padrões do Unity
- Removida duplicação de pastas e arquivos
- Organização hierárquica mais limpa e eficiente

**2. Integração com GitHub**
- Adicionado sistema de gerenciamento de versão diretamente no Unity
- Menu "Ferramentas > GitHub" com opções para:
  - Commit de alterações (Ctrl+Shift+G)
  - Visualização de histórico
  - Atualização do repositório (pull)
  - Verificação de status

**3. Compatibilidade com Meta Quest 3**
- Melhorias de renderização para experiência 360° em VR
- Otimização para tela cheia no Meta Quest 3
- Configuração automática para melhor desempenho em VR

**4. Diagnóstico e Correção de Visualização 360°**
- Adicionada ferramenta de diagnóstico para verificação de configurações VR (Ctrl+Shift+V)
- Corrigidos problemas de visualização em janela pequena versus tela cheia 360°
- Melhorias no sistema de renderização da esfera de vídeo

**5. Compilação para Windows**
- Menu dedicado para compilação do projeto para Windows (Ctrl+Shift+B)
- Interface amigável para seleção de opções de build
- Preservação das configurações do Android após compilação

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

## Sistema de Rotação e Limitação de Visualização

O sistema agora inclui dois componentes complementares:

### CameraRotationLimiter
Este componente controla as restrições de ângulo de visualização:
- Define um ângulo máximo de rotação permitido (padrão: 75°)
- Monitora a rotação da câmera em tempo real
- Reposiciona suavemente quando o usuário ultrapassa o limite

### VideoRotationControl
Controla quando as restrições devem ser aplicadas:
- Define blocos de tempo para cada vídeo
- Ativa/desativa o limitador com base no progresso do vídeo
- Permite configurar ângulos diferentes para momentos específicos

## Instalação do Oculus Integration (Opcional)

Para obter a melhor experiência VR no Meta Quest:

1. Abra o Unity Asset Store (menu Window > Asset Store)
2. Busque por "Oculus Integration" e baixe o pacote gratuito
3. Após o download, clique em "Import" 
4. Durante a importação:
   - Responda "Yes" para atualizar o Oculus Utilities
   - Quando perguntado sobre o XR Plugin Management, selecione "Yes"
5. Após a instalação:
   - Abra o arquivo VRManager.cs
   - Descomente a linha `//#define USING_OCULUS_SDK` no início do arquivo
   - Salve o arquivo

## Ferramentas de Desenvolvimento

### Diagnóstico VR (Ctrl+Shift+V)
- Verifica configurações do projeto para compatibilidade com VR
- Analisa configurações da plataforma (Android, XR Management)
- Verifica a presença de componentes essenciais na cena
- Oferece recomendações para otimização

### Compilação Windows (Ctrl+Shift+B)
- Interface para configuração da compilação
- Opções para build de desenvolvimento
- Preservação de configurações entre plataformas

### GitHub (Ctrl+Shift+G)
- Interface para gerenciamento de versões
- Visualização de status e alterações
- Commits com mensagens explicativas (changelog)
- Push automático opcional

## Notas Importantes
1. O código possui adaptações para funcionar com ou sem o Oculus Integration
2. O modo 360° completo é otimizado para Meta Quest 3 
3. O sistema de limitação de rotação trabalha de forma integrada com o controlador de vídeos

# Características Gerais

- Reprodução de vídeos 360° em VR
- Sistema de controle de rotação com pontos focais
- Transições suaves entre ângulos de visualização
- Interface de administração via WebSocket
- Modo offline para demonstrações
- Reprodução local dos vídeos (não requer streaming)
- Sistema de bloqueio de visualização configurável
- Suporte para múltiplos vídeos
- Integração direta com GitHub para controle de versão

## Requisitos

- Unity 2022.3 ou superior
- Android Build Support (para builds Quest)
- Meta Quest 2/3 para experiência VR completa
- Oculus Integration Package (opcional, recomendado)

## Licença

Todos os direitos reservados. 