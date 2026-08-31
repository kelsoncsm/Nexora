# ADR-0001 — Modular Monolith

## Contexto

O Nexora precisa atender vários nichos e módulos com baixo custo operacional inicial, preservando limites que permitam evolução futura.

## Decisão

Adotar um Modular Monolith como único deploy lógico no MVP, com módulos coesos, contratos explícitos, dependências acíclicas, Clean Architecture e DDD pragmático. Não introduzir microservices nem mensageria distribuída sem necessidade comprovada e novo ADR.

## Alternativas

- Monólito sem limites: menor esforço inicial, porém alto acoplamento e evolução arriscada.
- Microservices desde o início: isolamento de deploy, porém complexidade operacional e distribuída injustificada.

## Consequências

Há simplicidade de deploy, transações e diagnóstico, com disciplina obrigatória para preservar boundaries. Extrações futuras permanecem possíveis, mas não são automáticas nem gratuitas.

## Status

Accepted
