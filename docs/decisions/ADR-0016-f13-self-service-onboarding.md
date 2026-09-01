# ADR-0016 — F13 Self-service onboarding

- Status: Aceita
- Data: 2026-09-01

## Contexto

Uma conta global autenticada precisa criar seu primeiro tenant sem intervenção da administração da plataforma, sem aceitar `TenantId` do cliente como autoridade e sem iniciar pagamento durante o cadastro.

## Decisão

O onboarding usa um `OnboardingDraft` global, vinculado exclusivamente ao `UserId` obtido do token. O rascunho é retomável, expira após 30 dias de inatividade e contém somente os dados do MVP: empresa, segmento, timezone IANA, plano, intervalo e confirmação.

A conclusão executa em uma única transação serializável e cria:

- `Tenant`, associado a um `BusinessSegment` global ativo;
- papel inicial `ADMIN`, permissões tenant-scoped e vínculo `TenantUser` do usuário autenticado;
- `Subscription` em `Trialing`, usando a política compartilhada de 14 dias da F9;
- evento inicial da assinatura e conclusão do rascunho.

A operação é idempotente pelo próprio rascunho. No PostgreSQL, a linha é bloqueada com `FOR UPDATE`. Planos elegíveis precisam estar ativos, públicos, habilitados para trial e possuir `PlanPrice` ativo para o intervalo escolhido. Preço e duração nunca são aceitos do frontend. Nenhum gateway de pagamento participa deste fluxo.

`Tenant.SegmentId` é uma FK opcional para compatibilidade com tenants anteriores, mas obrigatória no fluxo self-service. Segmento representa configuração inicial; não concede autorização nem substitui features.

## Consequências

- Usuários podem retomar o fluxo sem criar recursos parciais.
- Reenvios da confirmação retornam o mesmo tenant e assinatura.
- Planos antigos permanecem privados até configuração explícita pela plataforma.
- A migration é aditiva e preserva tenants existentes.
- Cobrança continua sendo responsabilidade do contexto Billing após o trial/onboarding.
