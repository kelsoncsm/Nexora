# ADR-0006 — Stack e organização física da F1

## Contexto

A fundação executável precisava fixar versões suportadas, projetos físicos e a biblioteca de UI sem antecipar módulos de negócio, autenticação ou tenancy funcional.

## Decisão

Adotar .NET 10 LTS, ASP.NET Core 10, EF Core 10, Npgsql 10, Angular 22, Node.js 24 LTS e TypeScript dentro da faixa oficialmente suportada pelo Angular 22. PrimeNG 22 será a biblioteca de UI base; Angular Material não será instalado.

Organizar o backend em `Api`, `Application`, `Domain` e `Infrastructure`, com dependências orientadas ao domínio, e manter a SPA em `frontend/nexora-web`. Componentes PrimeNG serão importados individualmente e encapsulados em `shared/ui` quando fizer sentido. Tokens e abstrações visuais próprios do Nexora evitam acoplamento disseminado à biblioteca.

## Alternativas

- .NET 8 e Angular 20/21: maduros, porém com horizonte de suporte menor para um projeto iniciado em 2026.
- Angular Material: integração oficial com Angular, mas não é a biblioteca escolhida para o produto.
- Sem biblioteca de UI: menor dependência inicial, com custo maior para os componentes comuns.
- PrimeNG e Angular Material juntos: catálogo amplo, porém bundle, UX e manutenção inconsistentes.

## Consequências

A solução usa versões estáveis das majors aprovadas, imagens Docker versionadas e gerenciamento central dos pacotes .NET. PrimeNG não pode conter regras de negócio; substituição futura fica concentrada na camada visual e nos design tokens. Atualizações de major exigem nova avaliação, enquanto patches compatíveis podem avançar normalmente.

## Status

Accepted
