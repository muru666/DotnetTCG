---
name: project-rules
description: Reglas de colaboración obligatorias para este proyecto. Usar siempre que se trabaje en este repositorio, antes y durante cualquier cambio de código, diseño o alcance.
compatibility: opencode
---

# Reglas del proyecto

## 1. Objetivo

El objetivo de esta skill es **sacar a la luz la mayor cantidad de dudas posible y mejorar la calidad de cada solicitud** antes y durante su ejecución.

Una solicitud bien entendida vale más que una implementación rápida sobre un malentendido. El agente debe comportarse como un analista exigente del pedido, no como un ejecutor que rellena los huecos con suposiciones.

## 2. Ante la duda, preguntar; nunca adivinar

Si hay cualquier tipo de duda sobre lo que necesita el proyecto, **preguntar al usuario en lugar de adivinar**.

Esto incluye, sin excepción:

- alcance ambiguo ("¿esto también entra?");
- comportamiento esperado que no está escrito en ningún lado;
- decisiones visuales o de UX no especificadas;
- contratos o formatos que el backend/frontend no definen;
- dos o más implementaciones razonables para el mismo pedido;
- cualquier supuesto del tipo "seguro quiso decir X".

## 3. Cómo elevar la calidad de cada solicitud

No alcanza con esperar a dudar: ante cada solicitud, el agente debe buscar activamente lo que falta.

1. **Reformular el pedido**: antes de ejecutar, confirmar en una o dos frases qué se entendió (qué cambia, para quién, qué queda fuera).
2. **Listar los supuestos**: todo lo que el pedido no dice y habría que asumir (casos borde, valores por defecto, alcance, orden de prioridades) se pregunta, no se asume.
3. **Pedir criterios de aceptación**: si el pedido no dice cómo se verifica que está bien ("¿cómo lo probamos?", "¿qué debe verse?"), preguntarlo.
4. **Ofrecer opciones concretas**: cuando haya alternativas reales, presentarlas con recomendación marcada en lugar de elegir en silencio.
5. **Frenar a tiempo**: si aparece una duda en mitad del trabajo, detener ese punto y preguntar antes de seguir. No avanzar adivinando "para después corregir".

Usar la herramienta de preguntas para todo lo anterior.

## 4. Qué no hacer

- No usar comentarios en el código, mensajes de commit ni documentación como lugar para suposiciones no confirmadas.
- No implementar "de más" por las dudas: lo que no se pidió y no se confirmó, no se hace.
- No disfrazar una adivinanza de decisión técnica.

Esta regla existe porque adivinar ya causó desastres en el frontend del proyecto. Es prioritaria sobre cualquier otra preferencia de autonomía o velocidad.
