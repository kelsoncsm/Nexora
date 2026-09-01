# QA visual do frontend

## Status em 2026-09-01

- Implementação: OK
- Build: OK
- Testes: OK
- QA visual interativo: PENDENTE
- Status global: PARCIAL exclusivamente pela ausência de navegador conectado; não bloqueia o roadmap.

## Viewports pendentes

- 1440 px
- 1280 px
- 1024 px
- 768 px
- 430 px
- 390 px

## Checklist

Validar sidebar expandida/recolhida, drawer e scrim mobile, topbar, overflow horizontal, tabelas, formulários, dialogs, cards, menus, textos longos, botões, estados vazios/loading/erro e os dados reais de tenant, usuário e plano no shell.

Correções encontradas nessa inspeção pertencem a QA visual desde que não alterem arquitetura, segurança, autorização, multi-tenancy ou regras comerciais.

## Dívida técnica não bloqueante

O bundle inicial excede o budget de warning em 65,13 kB. Não aumentar o budget apenas para ocultar o aviso. Avaliar lazy loading durante performance/hardening. Loading, skeleton, empty state, error state, confirmação e feedback devem futuramente ser componentes compartilhados, evitando implementações diferentes por módulo.

Na futura padronização de ícones, preferir PrimeIcons se for a evolução natural do stack PrimeNG existente; não adicionar Angular Material nem outra biblioteca apenas para esse ajuste.
