# ADR-0003 — PostgreSQL como banco principal

## Contexto

O Nexora requer persistência relacional transacional, constraints, índices, migrations e suporte maduro no ecossistema .NET.

## Decisão

Adotar PostgreSQL como banco principal e Entity Framework Core como ORM. Toda evolução de schema será versionada por migrations revisadas e testadas.

## Alternativas

- SQL Server: integração madura com .NET, mas não é a stack definida para o produto.
- Banco documental: flexibilidade de schema, porém pior aderência às relações e invariantes centrais.

## Consequências

A equipe padroniza desenvolvimento, testes de integração e produção em PostgreSQL. Recursos específicos do provedor devem ser encapsulados quando necessário, sem abstrações especulativas. Backup, restore e alta disponibilidade serão detalhados até a F14.

## Status

Accepted
