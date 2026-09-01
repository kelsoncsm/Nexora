# ADR-0018 — Verticais orientados por configuração

## Status

Aceito — F15.

## Contexto

O primeiro vertical precisa atender barbearias e salões sem criar produto paralelo, regras comerciais arbitrárias ou condicionais por segmento no Core.

## Decisão

- Templates são configuração versionada, indexada pelo código de `BusinessSegment`.
- A resolução parte do segmento persistido no tenant; o cliente não o informa como autoridade.
- Um template contém textos, terminologia e presets categorizados.
- Presets são opcionais; duração e preço são informados pelo tenant.
- A aplicação é idempotente por nome no tenant e reutiliza o catálogo genérico.
- Segmentos sem template continuam no fluxo genérico.
- `BARBERSHOP_SALON` é criado por migration, sem operação manual no banco.

## Consequências

Outro nicho pode ser adicionado com um segmento e uma entrada de configuração, sem duplicar Customers, Professionals, Services ou Scheduling. Comportamento de domínio próprio continuará sujeito a decisão arquitetural.
