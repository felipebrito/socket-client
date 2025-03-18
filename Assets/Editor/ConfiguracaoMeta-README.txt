# 🥽 Configuração do Meta Quest 3 - Suite de Ferramentas ▸codex || aparato®

![](https://img.shields.io/badge/Meta%20Quest%203-Ready-success)
![](https://img.shields.io/badge/Unity-2021.3+-blue)
![](https://img.shields.io/badge/XR%20Plugin-Required-orange)

## 📋 Índice
- [Introdução](#introdução)
- [Requisitos](#requisitos)
- [Configuração Rápida](#configuração-rápida)
- [Ferramentas Disponíveis](#ferramentas-disponíveis)
- [Detalhes de Configuração](#detalhes-de-configuração)
- [Trabalhando com Vídeos 360°](#trabalhando-com-vídeos-360)
- [Estrutura de Arquivos](#estrutura-de-arquivos)
- [Solução de Problemas](#solução-de-problemas)
- [Changelog](#changelog)
- [Recursos Adicionais](#recursos-adicionais)

## 🚀 Introdução

Este conjunto de ferramentas **▸codex || aparato®** facilita a configuração do seu projeto Unity para desenvolvimento no Meta Quest 3, com foco especial em aplicações de vídeo 360°. Nossa suite oferece configuração automática do projeto e ferramentas opcionais para criação de players de vídeo 360°.

## ⚙️ Requisitos

- Unity 2021.3 LTS ou superior
- XR Plugin Management (pacote Unity)
- Oculus XR Plugin (pacote Unity)
- Permissões de armazenamento no Meta Quest 3

## 🏃‍♂️ Configuração Rápida

### 1️⃣ Instalação de Pacotes

Para configurar o projeto corretamente, instale os seguintes pacotes via Package Manager:

1. Abra `Window > Package Manager`
2. Clique no botão `+` e selecione "Add package by name"
3. Instale os seguintes pacotes:
   - `com.unity.xr.management` (XR Plugin Management)
   - `com.unity.xr.oculus` (Oculus XR Plugin)
   - `com.unity.textmeshpro` (Opcional, mas recomendado para UI)

### 2️⃣ Configuração Automática do Projeto

1. No menu Unity, acesse: `▸aparato®◂ > Meta Quest > Setup for Meta Quest 3 ▸codex || aparato®`
2. Na janela que abrir, verifique as configurações e ajuste conforme necessário
3. Clique em "Aplicar Configurações"

> ✅ Após a configuração, um relatório detalhado será gerado no console mostrando o status de todas as configurações importantes.

## 🛠️ Ferramentas Disponíveis

Nossa suite **▸codex || aparato®** inclui as seguintes ferramentas:

### 🔧 Meta Quest Setup Tool ▸codex || aparato®

Esta ferramenta configura automaticamente:
- XR Settings otimizados para Meta Quest 3
- Quality Settings para melhor desempenho em VR
- Player Settings para Android/Quest
- Permissões Android necessárias
- AndroidManifest.xml com configurações adequadas
- APIs gráficas (Vulkan) e arquitetura (ARM64)

### 🎬 Video 360° Player Creator ▸codex || aparato® (Opcional)

Se você precisa de um player de vídeos 360°:
1. Acesse: `▸aparato®◂ > Meta Quest > Create 360 Video Player ▸codex || aparato®`
2. Personalize o player com várias opções
3. Um prefab completo será criado em seu projeto

> 📝 **Nota:** Se você já possui seu próprio player de vídeo 360°, esta ferramenta é totalmente opcional.

## 📊 Detalhes de Configuração

A ferramenta de configuração aplica as seguintes configurações essenciais:

### ⚡ XR Settings
- Adiciona e configura o XR Plugin Management
- Configura o Oculus XR Plugin como provedor de XR ativo
- Inicializa todas as configurações necessárias do XR
- Resolve problemas de compatibilidade entre versões do Unity

### 📱 Player Settings
- Define API Level mínimo (29) e alvo (33) para compatibilidade com Quest 3
- Configura StereoRenderingPath para SinglePass (melhor performance)
- Define Vulkan como API gráfica para melhor desempenho
- Configura arquitetura ARM64
- Ajusta compressão de texturas para melhor qualidade visual

### 🎮 Quality Settings
- Ajusta configurações de qualidade para melhor desempenho em VR
- Configura antialiasing e filtros anisotrópicos para melhor qualidade visual
- Define taxa de quadros alvo (FPS) otimizada

### 📄 Android Manifest e Permissões
- Configura manifesto com categorias VR necessárias
- Adiciona permissões para acesso ao armazenamento e internet
- Configura metadados específicos para Quest, incluindo suporte a Quest 3

## 🎥 Trabalhando com Vídeos 360°

### Opção 1: Vídeos Incluídos no Build 📦

Coloque seus vídeos na pasta `Assets/StreamingAssets/`:

```csharp
string videoPath = System.IO.Path.Combine(Application.streamingAssetsPath, "meuVideo360.mp4");
videoPlayer.url = videoPath;
```

### Opção 2: Vídeos no Armazenamento do Quest 📱

Para acessar vídeos armazenados no dispositivo:

```csharp
string videoPath = "file:///sdcard/Download/meuVideo360.mp4";
videoPlayer.url = videoPath;
```

> ⚠️ **Importante:** Requer permissão de leitura de armazenamento externo.

### Opção 3: Streaming via URL 🌐

Para streaming de vídeos da internet:

```csharp
videoPlayer.url = "https://meusite.com/videos/meuVideo360.mp4";
```

> ⚠️ **Importante:** Requer permissão de acesso à internet.

## 📁 Estrutura de Arquivos

O projeto contém:

- `Assets/Editor/MetaQuestSetupTool.cs` - Ferramenta de configuração ▸codex || aparato®
- `Assets/Editor/Video360PlayerCreator.cs` - Criador de player de vídeo 360° ▸codex || aparato®
- `Assets/Scripts/Video360Controller.cs` - Script para controle do player (criado se necessário)
- `Assets/Prefabs/360VideoPlayer/` - Prefabs gerados pela ferramenta (opcional)
- `Assets/Plugins/Android/` - Configurações Android (manifest e permissões)

## ❓ Solução de Problemas

### 🚫 Problemas de Compilação

- ✅ Certifique-se de que todos os pacotes necessários estão instalados
- ✅ Verifique se a plataforma Android está selecionada nas Build Settings
- ✅ Confirme que o Oculus XR Plugin está ativado no XR Plugin Management

### 🚫 O aplicativo não inicia no Meta Quest

- ✅ Verifique se o dispositivo está em modo de desenvolvedor
- ✅ Confirme que o AndroidManifest.xml está configurado corretamente
- ✅ Verifique se as permissões do Android estão corretas

### 🚫 Baixo desempenho ou engasgos na reprodução

- ✅ Reduza a resolução do vídeo ou use vídeos com menor bitrate
- ✅ Verifique se o FPS do vídeo não é muito alto (30fps é recomendado)
- ✅ Reduza a qualidade gráfica nas configurações do projeto

### 🚫 Problemas com permissões de armazenamento

- ✅ Verifique se o AndroidManifest.xml contém as permissões necessárias
- ✅ Reinstale o aplicativo depois de adicionar as permissões
- ✅ Em dispositivos Android 10+, use o seletor de arquivos em vez do acesso direto

## 📝 Changelog

### Versão 1.1.0 - Março 2024

🔄 **Melhorias na Configuração do Meta Quest 3**
- ✅ Corrigido problemas de compatibilidade com diferentes versões do Unity
- ✅ Adicionado sistema robusto de detecção e configuração do Oculus XR Plugin
- ✅ Implementado tratamento para APIs gráficas específicas do Meta Quest 3
- ✅ Melhorado o suporte a arquitetura ARM64 e Vulkan

🔧 **Detecção e Correção de Problemas**
- ✅ Adicionado sistema de verificação que gera relatório detalhado de configuração
- ✅ Implementado tratamento de erros para diferentes versões do Unity
- ✅ Melhorada a compatibilidade com novos SDKs do Android e Meta Quest

🎛️ **Interface e Usabilidade**
- ✅ Redesenhada a interface com branding ▸codex || aparato®
- ✅ Melhorada a clareza das opções de configuração
- ✅ Adicionadas descrições detalhadas para cada configuração

📄 **Documentação**
- ✅ Adicionado wiki completo com instruções passo a passo
- ✅ Expandida a seção de solução de problemas
- ✅ Incluídos exemplos de código para casos de uso comuns

### Versão 1.0.0 - Fevereiro 2024

🚀 **Lançamento Inicial**
- ✅ Ferramenta de configuração Meta Quest 3
- ✅ Criador de player de vídeo 360°
- ✅ Configurações automáticas para Unity
- ✅ Documentação básica

## 📚 Recursos Adicionais

- [Documentação do Meta Quest](https://developer.oculus.com/documentation/)
- [Best Practices para Vídeo VR](https://creator.oculus.com/learn/360-video-best-practices-vr/)
- [Unity XR Documentation](https://docs.unity3d.com/Manual/XR.html)

---

<div align="center">

### Desenvolvido por ▸codex || aparato® 
#### Para Unity e Meta Quest 3

</div> 