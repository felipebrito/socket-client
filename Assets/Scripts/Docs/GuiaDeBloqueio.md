# Guia de Configuração de Bloqueio de Rotação

O sistema de bloqueio de rotação permite controlar os momentos em que o usuário pode olhar livremente pelo ambiente 360° e quando a visualização é restringida a uma área específica.

## Conceitos Básicos

- **Bloqueio de Rotação**: Limita o quanto o usuário pode desviar seu olhar do ponto central.
- **Intervalos de Tempo**: Define períodos específicos durante o vídeo onde o bloqueio é aplicado.
- **Ângulo Máximo**: O limite (em graus) que o usuário pode desviar o olhar.
- **Velocidade de Retorno**: Quão rápido a visualização retorna ao centro quando o usuário ultrapassa o limite.

## Configurando o Bloqueio de Rotação

### No Inspector do Unity

1. Selecione o objeto com o componente `VRManager` na cena
2. Na seção "Configurações de Rotação":
   - Ative `Lock Rotation` para habilitar o sistema de bloqueio
   - Configure `Max Horizontal Angle` para definir o limite horizontal (padrão: 45°)
   - Configure `Max Vertical Angle` para definir o limite vertical (padrão: 30°)
   - Ajuste `Reset Rotation Speed` para controlar a velocidade de retorno (padrão: 2)

### Configurando Intervalos de Bloqueio

Na seção "Intervalos de Bloqueio" do `VRManager`:

1. Defina o tamanho da lista `Lock Time Ranges` com o número de intervalos desejados
2. Para cada intervalo configure:
   - `Start Time`: Tempo inicial (em segundos) onde o bloqueio começa
   - `End Time`: Tempo final (em segundos) onde o bloqueio termina
   - `Max Angle`: Ângulo máximo de desvio permitido neste intervalo
   - `Reset Speed`: Velocidade de retorno específica para este intervalo

## Exemplos de Uso

### Bloqueio em Momentos Específicos

Para criar um bloqueio entre 30s e 45s do vídeo:
```
Start Time: 30
End Time: 45
Max Angle: 30
Reset Speed: 1.5
```

### Bloqueio Progressivamente Mais Restritivo

Para criar uma sequência de bloqueios cada vez mais restritos:

**Intervalo 1:**
```
Start Time: 10
End Time: 20
Max Angle: 45
Reset Speed: 1
```

**Intervalo 2:**
```
Start Time: 30
End Time: 40
Max Angle: 30
Reset Speed: 1.5
```

**Intervalo 3:**
```
Start Time: 50
End Time: 60
Max Angle: 15
Reset Speed: 2
```

## Dicas de Configuração

- **Teste sempre**: Os valores ideais dependem muito do conteúdo do vídeo
- **Transições suaves**: Evite mudanças bruscas de ângulos entre intervalos próximos
- **Velocidade de retorno**: 
  - Valores baixos (0.5-1.0): Retorno lento e suave
  - Valores médios (1.5-2.5): Retorno equilibrado (recomendado)
  - Valores altos (3.0+): Retorno rápido, pode causar desconforto

## Solução de Problemas

- **Bloqueio não funciona**: Verifique se `Lock Rotation` está habilitado e se o intervalo está corretamente configurado
- **Rotação muito rígida**: Diminua a `Reset Speed` ou aumente `Max Angle`
- **Rotação muito livre**: Aumente a `Reset Speed` ou diminua `Max Angle`
- **Desconforto no usuário**: A velocidade de retorno pode estar muito alta, reduza para valores abaixo de 2 