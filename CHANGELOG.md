# CHANGELOG

## [1.0.0] - 2024-07-26

### Correções
- Corrigido problema no arquivo JsonTypeReflector.cs que causava erros de compilação relacionados a namespaces de segurança não disponíveis no Unity.
- Modificado o código para não utilizar as classes `ReflectionPermission`, `ReflectionPermissionFlag`, `SecurityPermission` e `PermissionState`.
- Substituído o comportamento de verificação de permissões por valores padrão seguros para ambientes Unity.

### Estado Atual
- Projeto compilando corretamente
- Socket.io-unity funcionando sem erros de namespace 