---
name: project-rules
description: Reglas de colaboración obligatorias para este proyecto. Usar siempre que se trabaje en este repositorio, antes y durante cualquier cambio de código, diseño o alcance.
compatibility: opencode
---

# Reglas del proyecto

## 1. Ante la duda, preguntar; nunca adivinar

Si hay cualquier tipo de duda sobre lo que necesita el proyecto, **preguntar al usuario en lugar de adivinar**.

Esto incluye, sin excepción:

- alcance ambiguo ("¿esto también entra?");
- comportamiento esperado que no está escrito en ningún lado;
- decisiones visuales o de UX no especificadas;
- contratos o formatos que el backend/frontend no definen;
- dos o más implementaciones razonables para el mismo pedido;
- cualquier supuesto del tipo "seguro quiso decir X".

Procedimiento:

1. Detener el trabajo en ese punto (no avanzar adivinando).
2. Preguntar con la herramienta de preguntas, ofreciendo opciones concretas cuando existan.
3. Recién después de la respuesta, implementar.

No usar comentarios en el código, mensajes de commit ni documentación como lugar para suposiciones no confirmadas.

Esta regla existe porque adivinar ya causó desastres en el frontend del proyecto. Es prioritaria sobre cualquier otra preferencia de autonomía o velocidad.
