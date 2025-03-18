# ▸aparato◂ Tools Suite para Unity

![Aparato Tools Logo](Resources/Logo/aparatoLogo.png)

## Visão Geral

O ▸aparato◂ Tools Suite é um conjunto abrangente de ferramentas para Unity, projetado para acelerar o desenvolvimento de projetos, especialmente para aplicações VR no Meta Quest 3. O pacote inclui ferramentas para:

- **Configuração de projetos para Meta Quest 3**: Configure facilmente seu projeto com as configurações otimizadas para o Meta Quest 3.
- **Criação de players de vídeo 360° otimizados**: Crie prefabs de player de vídeo 360° com um clique.
- **Gerenciamento de controle de versão com Git e GitHub**: Integração direta com Git e GitHub dentro do Unity Editor.
- **Build simplificado para Windows**: Crie builds para Windows sem mudar a plataforma do projeto.

## Índice

1. [Instalação](#instalação)
2. [Personalização da Interface](#personalização-da-interface)
3. [Meta Quest 3 Setup Tool](#meta-quest-3-setup-tool)
4. [Video 360° Player Creator](#video-360-player-creator)
5. [GitHub Integration Tools](#github-integration-tools)
    - [Simple Git Commit](#simple-git-commit)
    - [GitHub Manager](#github-manager)
6. [Windows Build Tool](#windows-build-tool)
7. [Resolução de Problemas](#resolução-de-problemas)
8. [Contribuições e Licença](#contribuições-e-licença)

## Instalação

### Requisitos

- Unity 2020.3 LTS ou superior
- Git instalado no sistema (para ferramentas de controle de versão)
- Pacotes recomendados:
  - XR Plugin Management (com.unity.xr.management)
  - Oculus XR Plugin (com.unity.xr.oculus)
  - TextMeshPro (opcional, para UI aprimorada)

### Passos para Instalação

1. Clone este repositório ou baixe o pacote
2. Importe os arquivos para seu projeto Unity (você pode usar um dos métodos abaixo):
   - Arraste a pasta do pacote para a janela do Project no Unity
   - Use `Assets > Import Package > Custom Package` e selecione o arquivo `.unitypackage`
3. As ferramentas estarão disponíveis no menu `▸aparato◂` no Editor Unity

## Personalização da Interface

Todas as janelas do ▸aparato◂ Tools Suite podem ser personalizadas para exibir seu próprio logo, substituindo o logo padrão da Unity.

### Como Adicionar seu Logo Personalizado

1. Navegue até `Assets/Resources/Logo/` em seu projeto
2. Substitua o arquivo `aparatoLogo.png` pelo seu próprio logo:
   - Formato recomendado: PNG com transparência
   - Tamanho ideal: entre 128x128 e 256x256 pixels
   - Formatos suportados: .png, .jpg, .tga
3. O logo será automaticamente exibido em todas as janelas do ▸aparato◂ Tools Suite

![Exemplo da Janela com Logo Personalizado](screenshots/logo_customizado_exemplo.png)

> **Nota**: Se você não adicionar seu próprio logo, um aviso será exibido no console, mas as ferramentas continuarão funcionando normalmente.

## Meta Quest 3 Setup Tool

A Meta Quest 3 Setup Tool configura automaticamente seu projeto Unity para desenvolvimento no Meta Quest 3, aplicando as melhores práticas de configuração para VR, especialmente para aplicações de vídeo 360°.

### Funcionalidades

- Configuração automática do XR Plugin Management
- Otimização das configurações de qualidade para o Meta Quest 3
- Configuração das permissões necessárias para Android
- Criação e configuração do AndroidManifest.xml
- Otimizações específicas para vídeo 360°

### Como Usar

1. No menu Unity, selecione `▸aparato◂ > Meta Quest 3 > Configurar Meta Quest 3`
2. Na janela que abrir, você pode ajustar as seguintes configurações:
   - **Configurar XR Settings**: Habilita e configura o XR Plugin Management
   - **Configurar Quality Settings**: Otimiza as configurações de qualidade
   - **Configurar Player Settings**: Configura os Player Settings para Android/Quest
   - **Configurar Permissões**: Adiciona as permissões necessárias
   - **Configurar Manifesto Android**: Cria/atualiza o AndroidManifest.xml
3. As opções de permissão permitem escolher:
   - Leitura de Armazenamento Externo
   - Escrita em Armazenamento Externo
   - Acesso à Internet
4. Para vídeo 360°, você pode ajustar:
   - Target FPS (60-90)
   - Resolução Dinâmica
5. Clique em "Aplicar Configurações" para configurar seu projeto

### O que é Configurado

- **XR Settings**:
  - Instalação e configuração do XR Plugin Management
  - Configuração do Oculus XR Plugin como loader padrão
- **Quality Settings**:
  - Configurações de VSync e Anti-aliasing otimizadas
  - Filtragem anisotrópica para melhor qualidade de texturas em 360°
- **Player Settings**:
  - SDK mínimo (API Level 29, Mínimo para Quest 3)
  - SDK alvo (API Level 33)
  - Configuração do Rendering Path para Single Pass
  - API gráfica definida para Vulkan
  - Arquitetura ARM64
- **Permissões**:
  - Geração de script de requisição de permissões em tempo de execução
- **Android Manifest**:
  - Configurações específicas para Quest (handtracking, VR intent)
  - Meta-dados para suporte a Quest, Quest 2 e Quest 3

### Verificação de Configuração

Após aplicar as configurações, o sistema executa uma verificação e apresenta um relatório detalhado, mostrando:
- Status das configurações de XR
- Status da API gráfica (Vulkan)
- Verificação de arquitetura 64-bit
- Existência do AndroidManifest.xml
- Existência de scripts de permissão

## Video 360° Player Creator

A ferramenta Video 360° Player Creator permite a criação rápida de players de vídeo 360° otimizados para o Meta Quest 3, gerando prefabs prontos para uso.

### Funcionalidades

- Criação de prefabs completos de player de vídeo 360°
- Configurações flexíveis para renderização de vídeo
- UI de controle opcional
- Suporte a vários formatos de vídeo
- Otimizações para performance em VR

### Como Usar

1. No menu Unity, selecione `▸aparato◂ > Meta Quest 3 > Criar videoplayer 360`
2. Na janela que abrir, configure as opções:
   - **Nome do Player**: Nome do prefab a ser criado
   - **Caminho do Vídeo (opcional)**: Caminho para um vídeo que será carregado automaticamente
   - **Auto Play**: Se o vídeo deve iniciar automaticamente
   - **Loop**: Se o vídeo deve repetir automaticamente
   - **Render Mode**: Método de renderização (RenderTexture recomendado)
   - **Raio da Esfera**: Tamanho da esfera de projeção
   - **Criar UI de Controle**: Gera UI básica para controlar o vídeo
3. Clique em "Criar Player de Vídeo 360°"

### O que é Criado

- **Prefab do Player**: Prefab completo com todas as configurações
- **Esfera de Projeção**: Com normais invertidas para projeção interna
- **Video Player**: Componente configurado conforme opções selecionadas
- **Material**: Material otimizado para vídeo
- **RenderTexture**: Para renderização de vídeo de alta qualidade
- **UI de Controle**: Interface com botões para play, pause e stop
- **Script Controller**: Para controlar o vídeo via código

### Estrutura do Prefab

```
Video360Player (Prefab Root)
├── VideoSphere
│   └── [MeshRenderer with Video Material]
├── [VideoPlayer Component]
├── [AudioSource Component]
├── [Video360Controller Script]
└── ControlUI (optional)
    └── Panel
        ├── PlayButton
        ├── PauseButton
        └── StopButton
```

### Script Controller

O script `Video360Controller` fornece métodos para:
- Iniciar, pausar e parar a reprodução
- Carregar novos vídeos em tempo de execução
- Responder a entrada do usuário (teclado/VR)
- Manipular eventos de fim de vídeo

## GitHub Integration Tools

As ferramentas de integração com GitHub simplificam o gerenciamento de versão diretamente dentro do Unity Editor, divididas em:

### Simple Git Commit

Ferramenta simplificada para operações básicas de Git, focada em commits rápidos.

#### Funcionalidades

- Visualização colorida do status do Git
- Commit simples com um clique
- Opção de adicionar arquivos automaticamente
- Push automático após commit
- Interface visual para mensagens de commit

#### Como Usar

1. No menu Unity, selecione `▸aparato◂ > Github > Commit simples`
2. Use as opções:
   - **Adicionar arquivos automaticamente**: Adiciona todos os arquivos modificados
   - **Push automático após commit**: Realiza push após o commit
3. Digite uma mensagem de commit descritiva
4. Clique em "Atualizar Status do Git" para ver o estado atual
5. Clique em "Realizar Commit" para efetuar o commit

### GitHub Manager

Ferramenta avançada com recursos completos de gerenciamento Git e GitHub.

#### Funcionalidades

- Inicialização de repositório
- Gerenciamento de credenciais GitHub
- Criação de repositórios GitHub
- Staging seletivo de arquivos
- Gerenciamento de branches
- Operações de pull e push
- Visualização detalhada do status

#### Como Usar

1. No menu Unity, selecione `▸aparato◂ > Github > Avançado (completo)`
2. A ferramenta detectará se Git está instalado e se o repositório está inicializado
3. Use as seções:
   - **Repository Status**: Mostra o status atual do repositório
   - **GitHub Credentials**: Configure credenciais para operações GitHub
   - **Changes and Commits**: Gerencie alterações e realize commits
   - **Branch Management**: Crie e gerencie branches
   - **Remote Repository**: Configure repositórios remotos
   - **Command Output**: Exibe saída detalhada dos comandos

#### Gerenciamento de Credenciais

1. Expanda a seção "GitHub Credentials"
2. Insira seu nome de usuário e token do GitHub
   > **Nota**: O token deve ter escopo "repo" para todas as operações
3. Teste suas credenciais com o botão "Test Credentials"

#### Criação de Repositório GitHub

1. Com as credenciais configuradas, clique em "Create New GitHub Repository"
2. Configure:
   - Nome do repositório
   - Descrição
   - Se é privado ou público
3. Clique em "Create Repository"

## Windows Build Tool

A Windows Build Tool permite criar builds para Windows sem precisar mudar a plataforma atual do projeto, economizando tempo e evitando erros durante o desenvolvimento multiplataforma.

### Funcionalidades

- Build para Windows sem trocar plataforma
- Opções de build configuráveis
- Criação automatizada de arquivo ZIP após a build
- Suporte a builds de desenvolvimento

### Como Usar

1. No menu Unity, selecione `▸aparato◂ > Build > Windows`
2. Configure as opções:
   - **Build Path**: Diretório onde a build será salva
   - **Executable Name**: Nome do arquivo executável
   - **Development Build**: Gera build com recursos de desenvolvimento
   - **Include All Enabled Scenes**: Inclui todas as cenas habilitadas
   - **Zip After Build**: Compacta a build em arquivo ZIP
3. Clique em "Build Windows Standalone"

### Processo de Build

1. A ferramenta salva as configurações atuais de plataforma
2. Configura as opções de build para Windows (sem alterar a configuração do projeto)
3. Executa o processo de build
4. Cria o arquivo ZIP se a opção estiver habilitada
5. Abre o explorador de arquivos no local da build

## Resolução de Problemas

### Problemas Comuns

#### Logo Personalizado Não Aparece

- Verifique se o arquivo `aparatoLogo.png` existe em `Assets/Resources/Logo/`
- Certifique-se de que a imagem está em um formato suportado (.png, .jpg, .tga)
- Confira se a pasta `Resources` está no caminho correto
- Verifique o console do Unity para mensagens de erro

#### Erros nas Ferramentas de Git

- Certifique-se de que o Git está instalado no sistema
- Verifique se o Git está adicionado ao PATH do sistema
- Teste se o Git funciona no terminal do sistema
- Verifique permissões de acesso ao diretório do projeto

#### Erro na Configuração do Meta Quest 3

- Verifique se os pacotes XR Plugin Management e Oculus XR Plugin estão instalados
- Consulte o Unity Package Manager para resolver dependências
- Verifique as configurações de Android Developer no dispositivo Meta Quest 3
- Verifique se o SDK do Android está atualizado

#### Erros no Video Player 360°

- Verifique a compatibilidade do formato de vídeo
- Certifique-se de que a esfera tenha suas normais invertidas
- Verifique se o Unity está utilizando a API gráfica correta (Vulkan)
- Teste diferentes modos de renderização

## Contribuições e Licença

### Contribuindo com o Projeto

Contribuições são bem-vindas! Para contribuir:

1. Faça um fork do repositório
2. Crie um branch para sua feature (`git checkout -b feature/NovaFeature`)
3. Faça commit das alterações (`git commit -m 'Adiciona nova feature'`)
4. Faça push para o branch (`git push origin feature/NovaFeature`)
5. Abra um Pull Request

### Padrões de Código

- Siga as convenções de nomenclatura C# da Microsoft
- Documente seu código com comentários XML
- Siga as convenções de estilo do projeto existente
- Adicione testes para novas funcionalidades quando possível

### Licença

Este projeto está licenciado sob [sua licença aqui]. Veja o arquivo LICENSE para mais detalhes.

---

## Contato e Suporte

Para suporte ou para reportar problemas, por favor abra uma issue no GitHub ou entre em contato através de [seu email/contato aqui].

---

Desenvolvido por ▸codex || aparato® 