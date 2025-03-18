# Guia de Instalação do Newtonsoft.Json no Unity

Este guia explica como instalar e configurar o Newtonsoft.Json em um projeto Unity para uso com SocketIOUnity.

## Método 1: Via Package Manager (Recomendado)

1. Abra o projeto no Unity
2. Vá para `Window > Package Manager`
3. Clique no botão `+` no canto superior esquerdo
4. Selecione `Add package by name...`
5. Digite `com.unity.nuget.newtonsoft-json`
6. Insira a versão `3.2.1` (ou mais recente)
7. Clique em `Add`

Alternativamente, você pode editar diretamente o arquivo `Packages/manifest.json` e adicionar:
```json
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

## Método 2: Instalação Manual

Se o método 1 não funcionar, você pode instalar manualmente:

1. Baixe a DLL do Newtonsoft.Json (versão 13.0.2 recomendada)
2. Crie uma pasta `Assets/Plugins/Newtonsoft.Json` no seu projeto
3. Coloque o arquivo `Newtonsoft.Json.dll` na pasta criada

## Configuração Adicional

### Assembly Definition

Para garantir que o Newtonsoft.Json seja referenciado corretamente:

1. Crie um arquivo `VRPlayerAssembly.asmdef` na pasta `Assets/Scripts`
2. Configure-o para referenciar `Newtonsoft.Json.dll` como precompiled reference

### Preservar Assemblies

Para evitar problemas de stripping durante o build IL2CPP:

1. Crie um arquivo `link.xml` na pasta `Assets`
2. Adicione as configurações para preservar os assemblies necessários

## Testando a Instalação

1. Adicione o componente `NewtonsoftJsonTest` a um objeto na cena
2. Execute a cena e verifique o Console 
3. Se não houver erros e você ver mensagens indicando serialização/deserialização bem-sucedidas, a instalação está funcionando!

## Solução de Problemas

- Se você continuar vendo erros de namespace, tente reiniciar o Unity
- Verifique se os assembly definition files estão configurados corretamente
- Para builds IL2CPP, certifique-se de que o arquivo `link.xml` está configurado corretamente 